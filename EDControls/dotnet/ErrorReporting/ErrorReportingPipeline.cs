using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.ErrorReporting;

/// <summary>
/// Stav a logika hlášení chyb: kontext, deduplikace, snímek okna, odeslání na ingest a záložní log.
/// Proces má jednu výchozí instanci za fasádou <see cref="ErrorReports"/>; testy si skládají vlastní,
/// takže nesahají na statiku a mohou běžet paralelně.
/// </summary>
internal sealed class ErrorReportingPipeline
{
    private const int TimeoutSeconds = 15;

    private readonly Lazy<HttpClient> _client;
    private readonly ErrorFallbackLog _fallbackLog;
    private readonly Func<bool> _isInternal;
    private readonly Func<EDServices?> _services;
    private readonly Func<IReadOnlyList<ErrorAttachment>> _captureWindows;
    private readonly Func<Assembly?> _entryAssembly;

    /// <param name="client">Volá se až při prvním odeslání, uvnitř ochrany.</param>
    /// <param name="entryAssembly">Jméno a verze aplikace, dokud je služby nenesou. Default entry assembly procesu.</param>
    internal ErrorReportingPipeline(Func<HttpClient> client, ErrorFallbackLog fallbackLog, Func<bool> isInternal,
        Func<EDServices?> services, Func<IReadOnlyList<ErrorAttachment>> captureWindows,
        Func<Assembly?>? entryAssembly = null)
    {
        _client = new Lazy<HttpClient>(client);
        _fallbackLog = fallbackLog;
        _isInternal = isInternal;
        _services = services;
        _captureWindows = captureWindows;
        _entryAssembly = entryAssembly ?? Assembly.GetEntryAssembly;
    }

    /// <summary>
    /// Vzniká ve statickém inicializátoru <see cref="ErrorReports"/>: výjimka tady by z každého
    /// hlášení (i z crash handlerů) udělala TypeInitializationException, proto jen delegáty.
    /// </summary>
    internal static ErrorReportingPipeline CreateDefault() => new(
        () => new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) },
        new ErrorFallbackLog(() => MlineCommonPaths.File("error-reports.log")),
        () => MlineEnvironment.IsInternal,
        () => EDServices.CurrentOrNull,
        WindowSnapshot.Capture);

    /// <summary>Deduplikace je per-běh aplikace.</summary>
    internal ErrorThrottle Throttle { get; } = new();

    internal bool Enabled { get; set; } = true;

    /// <inheritdoc cref="ErrorReports.Report"/>
    internal string? Report(Exception ex, Window? owner = null)
    {
        try
        {
            if (!Enabled || ex is null) return null;

            var ctx = BuildContext(owner);
            var decision = Throttle.Register(ErrorThrottle.BuildKey(ex, ctx.WindowNamespace), DateTime.UtcNow);
            if (decision.Action == ThrottleAction.Suppress) return null;

            var report = ErrorReportBuilder.Build(ex, ctx, _captureWindows(), decision.RepeatCount);
            Send(report);
            return report.RefId;
        }
        catch
        {
            // Hlášení chyby nesmí být druhá chyba nad tou první.
            return null;
        }
    }

    /// <inheritdoc cref="ErrorReports.ReportBackground"/>
    internal void ReportBackground(Exception ex)
    {
        try
        {
            if (!Enabled || ex is null) return;

            var ctx = BuildContext(null);
            var decision = Throttle.Register(ErrorThrottle.BuildKey(ex, null), DateTime.UtcNow);
            if (decision.Action == ThrottleAction.Suppress) return;

            Send(ErrorReportBuilder.Build(ex, ctx, null, decision.RepeatCount));
        }
        catch
        {
            // best-effort
        }
    }

    /// <inheritdoc cref="ErrorReports.ReportFatal"/>
    internal void ReportFatal(Exception ex)
    {
        try
        {
            if (!Enabled || ex is null) return;

            var report = ErrorReportBuilder.Build(ex, BuildContext(null));
            report.Data["fatal"] = true;
            SendAsync([report]).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Padáme tak jako tak - hlášení nesmí přidat druhou výjimku.
        }
    }

    /// <inheritdoc cref="ErrorReports.ReportLegacyExit"/>
    internal void ReportLegacyExit(string exeName, string formFqn, int exitCode, string? hostDetail = null)
    {
        try
        {
            if (!Enabled) return;

            var decision = Throttle.Register($"LegacyExit|{exeName}|{formFqn}|{exitCode}", DateTime.UtcNow);
            if (decision.Action == ThrottleAction.Suppress) return;

            var ctx = BuildContext(null) with { WindowNamespace = formFqn, WindowTitle = exeName };
            var cause = string.IsNullOrWhiteSpace(hostDetail) ? null : FirstLine(hostDetail);
            var report = ErrorReportBuilder.Build(
                new LegacyHostCrashedException(exeName, formFqn, exitCode, cause), ctx, null, decision.RepeatCount);
            report.Data["legacyHost"] = true;
            report.Data["exitCode"] = exitCode;
            report.Data["exitCodeHex"] = $"0x{exitCode:X8}";
            // Stack syntetické výjimky by ukazoval do shellu, skutečný je ve výpisu hosta.
            if (cause is not null) report.Data["stack"] = hostDetail;
            Send(report);
        }
        catch
        {
            // best-effort
        }
    }

    /// <inheritdoc cref="ErrorReports.ReportFollowUp"/>
    internal void ReportFollowUp(string refId, string userText, Window? owner = null)
    {
        try
        {
            if (!Enabled || string.IsNullOrWhiteSpace(refId) || string.IsNullOrWhiteSpace(userText)) return;
            Send(ErrorReportBuilder.BuildFollowUp(refId, userText.Trim(), BuildContext(owner)));
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>Odešle hlášení na pozadí. Vrací se okamžitě, výsledek nikoho nezajímá.</summary>
    internal void Send(ErrorReport report)
    {
        if (report is null) return;
        _ = Task.Run(() => SendAsync([report]));
    }

    /// <summary>Odešle dávku na pozadí. Delší než limit se rozdělí na víc requestů.</summary>
    internal void Send(IReadOnlyList<ErrorReport> reports)
    {
        if (reports is null || reports.Count == 0) return;
        _ = Task.Run(() => SendAsync(reports));
    }

    /// <summary>
    /// Vlastní odeslání; produkční kód jde přes <see cref="Send(ErrorReport)"/>, aby nikdo
    /// omylem nečekal na síť na UI vlákně. Neodeslané dávky jdou do záložního logu.
    /// </summary>
    internal async Task<bool> SendAsync(IReadOnlyList<ErrorReport> reports)
    {
        var allOk = true;

        for (var offset = 0; offset < reports.Count; offset += ErrorReporter.MaxBatchSize)
        {
            var chunk = reports.Skip(offset).Take(ErrorReporter.MaxBatchSize).ToList();
            var chunkOk = false;
            try
            {
                // Jeden záznam se posílá jako objekt, víc jako pole - obojí ingest umí.
                var payload = chunk.Count == 1
                    ? chunk[0].ToJson().ToJsonString()
                    : new System.Text.Json.Nodes.JsonArray(
                        [.. chunk.Select(r => (System.Text.Json.Nodes.JsonNode)r.ToJson())]).ToJsonString();

                using var request = new HttpRequestMessage(HttpMethod.Post, ErrorReporter.IngestUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-API-Key", ErrorReporter.ApiKey);

                using var response = await _client.Value.SendAsync(request).ConfigureAwait(false);
                chunkOk = response.IsSuccessStatusCode;
            }
            catch
            {
                // Best-effort - výpadek sítě, VPN ani 500 nesmí nic shodit.
            }

            if (!chunkOk)
            {
                allOk = false;
                _fallbackLog.Append(chunk);
            }
        }

        return allOk;
    }

    private ErrorContext BuildContext(Window? owner)
    {
        var services = _services();
        var mlRest = services?.MLRest;
        var app = AppIdentity.Resolve(services, _entryAssembly());

        return new ErrorContext
        {
            AppId = app.Prefix,
            IsInternal = _isInternal(),
            Idfa = mlRest?.Idfa,
            LoginName = mlRest?.LoginName,
            FullName = mlRest?.FullName,
            Mlp = mlRest?.Mlp,
            AppVersion = app.Version,
            WindowNamespace = owner?.GetType().FullName,
            WindowTitle = owner?.Title,
            MachineName = SafeMachineName(),
        };
    }

    private static string FirstLine(string text) =>
        text.TrimStart().Split('\n', 2)[0].Trim();

    private static string? SafeMachineName()
    {
        try { return Environment.MachineName; }
        catch { return null; }
    }
}
