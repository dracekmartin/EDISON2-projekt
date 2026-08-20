using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.ErrorReporting;

/// <summary>
/// Vstupní bod hlášení chyb: složí kontext, projde deduplikací, přiloží snímek okna a odešle.
/// Volá se z <see cref="PopupDisplayer"/>, ne přímo z aplikací. Hybridní režim: technické jádro
/// odejde automaticky hned při chybě, dobrovolný popis okolností posílá uživatel z chybového
/// dialogu jako navazující záznam se stejným referenčním číslem.
/// </summary>
public static class ErrorReports
{
    /// <summary>Deduplikace je per-běh aplikace; settable jen kvůli testům (statický stav mezi nimi prosakuje).</summary>
    internal static ErrorThrottle Throttle { get; set; } = new();

    /// <summary>Vypínač pro testy a pro případ, že by hlášení dělalo problém v provozu.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Nahlásí chybu. Vrací referenční číslo pro dialog, nebo <c>null</c>, když se
    /// neposílalo (deduplikace, strop, vypnuto) - dialog pak číslo neukáže, protože
    /// číslo, které nikam nedorazilo, by podporu jen zmátlo.
    /// **Volat na UI vlákně** (kvůli snímku okna). Nikdy nehází.
    /// </summary>
    public static string? Report(Exception ex, Window? owner = null)
    {
        try
        {
            if (!Enabled || ex is null) return null;

            var ctx = BuildContext(owner);
            var decision = Throttle.Register(ErrorThrottle.BuildKey(ex, ctx.WindowNamespace), DateTime.UtcNow);
            if (decision.Action == ThrottleAction.Suppress) return null;

            var report = ErrorReportBuilder.Build(ex, ctx, WindowSnapshot.Capture(), decision.RepeatCount);
            ErrorReporter.Send(report);
            return report.RefId;
        }
        catch
        {
            // Hlášení chyby nesmí být druhá chyba nad tou první.
            return null;
        }
    }

    /// <summary>
    /// Chyba mimo UI vlákno (neodchycená task výjimka). Bez dialogu a bez snímku okna -
    /// běží na cizím vlákně, kde by <see cref="WindowSnapshot"/> stejně nefungoval.
    /// </summary>
    public static void ReportBackground(Exception ex)
    {
        try
        {
            if (!Enabled || ex is null) return;

            var ctx = BuildContext(null);
            var decision = Throttle.Register(ErrorThrottle.BuildKey(ex, null), DateTime.UtcNow);
            if (decision.Action == ThrottleAction.Suppress) return;

            ErrorReporter.Send(ErrorReportBuilder.Build(ex, ctx, null, decision.RepeatCount));
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>
    /// Fatální chyba, po které proces končí (<c>AppDomain.UnhandledException</c>). Na rozdíl
    /// od ostatních cest se odesílá **synchronně** s krátkým timeoutem - fire-and-forget by
    /// se nestihl, protože runtime proces vzápětí ukončí.
    /// </summary>
    public static void ReportFatal(Exception ex)
    {
        try
        {
            if (!Enabled || ex is null) return;

            var report = ErrorReportBuilder.Build(ex, BuildContext(null));
            report.Data["fatal"] = true;
            ErrorReporter.SendAsync([report]).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Padáme tak jako tak - hlášení nesmí přidat druhou výjimku.
        }
    }

    /// <summary>Odešle dobrovolné doplnění od uživatele k už nahlášené chybě.</summary>
    public static void ReportFollowUp(string refId, string userText, Window? owner = null)
    {
        try
        {
            if (!Enabled || string.IsNullOrWhiteSpace(refId) || string.IsNullOrWhiteSpace(userText)) return;
            ErrorReporter.Send(ErrorReportBuilder.BuildFollowUp(refId, userText.Trim(), BuildContext(owner)));
        }
        catch
        {
            // best-effort
        }
    }

    private static ErrorContext BuildContext(Window? owner)
    {
        var services = EDServices.CurrentOrNull;
        var mlRest = services?.MLRest;

        return new ErrorContext
        {
            AppId = services?.FormIdPrefix ?? "EDISON2",
            IsInternal = MlineEnvironment.IsInternal,
            Idfa = mlRest?.Idfa,
            LoginName = mlRest?.LoginName,
            FullName = mlRest?.FullName,
            Mlp = mlRest?.Mlp,
            AppVersion = services?.AppVersion,
            WindowNamespace = owner?.GetType().FullName,
            WindowTitle = owner?.Title,
            MachineName = SafeMachineName(),
        };
    }

    private static string? SafeMachineName()
    {
        try { return Environment.MachineName; }
        catch { return null; }
    }
}
