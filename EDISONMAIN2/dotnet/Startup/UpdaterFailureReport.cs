using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using EDControls.ErrorReporting;

namespace EDISONMAIN2;

/// <summary>
/// Nahlásí do telemetrie neúspěšný běh legacy updateru stanice. Updater sám nic nehlásí a shell na jeho exit
/// kód nečeká, zbývá log v adresáři instalace. Čte se před spuštěním dalšího updatu, který log přepíše.
/// </summary>
internal sealed class UpdaterFailureReport(
    ILocalPrefs prefs,
    string installDir,
    Func<string, string?> readFile,
    Action<ErrorReportType, string, string, string, JsonObject> send,
    Func<DateTime> now)
{
    internal const string Module = "EDUPDATE";
    internal const string Name = "Aktualizace stanice";

    /// <summary>Log z doby před nasazením hlášení už nikoho nezajímá.</summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    internal static UpdaterFailureReport ForInstallation(ILocalPrefs prefs) => new(
        prefs, AppContext.BaseDirectory, ReadShared,
        (type, module, name, message, details) => ErrorReports.ReportEvent(type, module, name, message, details),
        () => DateTime.Now);

    /// <summary>True = hlášení odešlo. Nikdy nehází, start shellu na něm nesmí stát.</summary>
    internal bool ReportIfNew()
    {
        try
        {
            var text = readFile(Path.Combine(installDir, UpdaterLog.FileName));
            if (text is null) return false;
            var run = UpdaterLog.Parse(text);
            if (run.Outcome != UpdaterRunOutcome.Failed) return false;
            if ((run.FinishedAt ?? run.StartedAt) is { } at && now() - at > MaxAge) return false;

            var key = InstallKey(installDir);
            var reported = prefs.ReportedUpdaterFailures;
            if (reported is not null && reported.TryGetValue(key, out var last) && last == run.RunKey) return false;

            var previous = readFile(Path.Combine(installDir, UpdaterLog.PreviousFileName)) is { } previousText
                ? UpdaterLog.Parse(previousText)
                : null;
            var previousFailed = previous?.Outcome == UpdaterRunOutcome.Failed;
            // Jeden pokus s obnovou bývá přechodný (zámek, výpadek), opakované selhání nebo neobnovená stanice ne.
            var type = run.Restored == false || previousFailed ? ErrorReportType.Error : ErrorReportType.Warning;
            send(type, Module, Name, $"Aktualizace stanice selhala: {run.FailureMessage}", Details(run, previousFailed));

            prefs.ReportedUpdaterFailures = new Dictionary<string, string>(
                reported ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase) { [key] = run.RunKey };
            prefs.Save();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JsonObject Details(UpdaterRun run, bool previousFailed) => new()
    {
        ["updater"] = Module,
        ["failedAt"] = run.FinishedAt?.ToString("s"),
        ["failure"] = run.FailureMessage,
        ["restored"] = run.Restored,
        ["exitCode"] = run.ExitCode,
        ["sourcePath"] = run.SourcePath,
        ["targetPath"] = run.TargetPath,
        ["targetVersion"] = run.Version,
        ["forced"] = run.Forced,
        ["previousRunFailed"] = previousFailed,
        ["logTail"] = string.Join("\n", run.Tail),
    };

    private static string InstallKey(string dir) =>
        Path.GetFullPath(dir).TrimEnd('\\', '/').ToUpperInvariant();

    private static string? ReadShared(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            // Updater log zakládá a maže, čtení mu v tom nesmí bránit.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
}
