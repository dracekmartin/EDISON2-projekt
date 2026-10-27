using System.Text.Json.Nodes;
using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.ErrorReporting;

/// <summary>
/// Vstupní bod hlášení chyb: složí kontext, projde deduplikací, přiloží snímek okna a odešle.
/// Volá se z <see cref="PopupDisplayer"/>, ne přímo z aplikací. Hybridní režim: technické jádro
/// odejde automaticky hned při chybě, dobrovolný popis okolností posílá uživatel z chybového
/// dialogu jako navazující záznam se stejným referenčním číslem.
/// Statická fasáda nad výchozí <see cref="ErrorReportingPipeline"/>, stav drží instance.
/// </summary>
public static class ErrorReports
{
    internal static ErrorReportingPipeline Pipeline { get; } = ErrorReportingPipeline.CreateDefault();

    /// <summary>Vypínač pro testy a pro případ, že by hlášení dělalo problém v provozu.</summary>
    public static bool Enabled
    {
        get => Pipeline.Enabled;
        set => Pipeline.Enabled = value;
    }

    /// <summary>
    /// Nahlásí chybu. Vrací referenční číslo pro dialog, nebo <c>null</c>, když se
    /// neposílalo (deduplikace, strop, vypnuto) - dialog pak číslo neukáže, protože
    /// číslo, které nikam nedorazilo, by podporu jen zmátlo.
    /// **Volat na UI vlákně** (kvůli snímku okna). Nikdy nehází.
    /// </summary>
    public static string? Report(Exception ex, Window? owner = null) => Pipeline.Report(ex, owner);

    /// <summary>
    /// Chyba mimo UI vlákno (neodchycená task výjimka). Bez dialogu a bez snímku okna -
    /// běží na cizím vlákně, kde by <see cref="WindowSnapshot"/> stejně nefungoval.
    /// </summary>
    public static void ReportBackground(Exception ex) => Pipeline.ReportBackground(ex);

    /// <summary>
    /// Fatální chyba, po které proces končí (<c>AppDomain.UnhandledException</c>). Na rozdíl
    /// od ostatních cest se odesílá **synchronně** s krátkým timeoutem - fire-and-forget by
    /// se nestihl, protože runtime proces vzápětí ukončí.
    /// </summary>
    public static void ReportFatal(Exception ex) => Pipeline.ReportFatal(ex);

    /// <summary>
    /// Hostovaný legacy modul skončil nenulovým kódem (pád před jakýmkoli handlerem, nativní
    /// crash, host po fatal chybě). Kontext okna nese forma a exe modulu, ne shell - Přehled
    /// telemetrie tak seskupí podle modulu. Throttle klíč je stabilní (výjimka se nikdy nehodila,
    /// takže <see cref="ErrorThrottle.BuildKey"/> by bez stacku slepil všechny moduly do jednoho).
    /// <paramref name="hostDetail"/> je výjimka, kterou host zapsal před pádem (<see cref="LegacyCrashDetail"/>).
    /// Volatelné z libovolného vlákna. Nikdy nehází.
    /// </summary>
    public static void ReportLegacyExit(string exeName, string formFqn, int exitCode, string? hostDetail) =>
        Pipeline.ReportLegacyExit(exeName, formFqn, exitCode, hostDetail);

    /// <summary>Původní signatura pro exe přeložené proti starší samostatně nasazené EDControls.dll.</summary>
    public static void ReportLegacyExit(string exeName, string formFqn, int exitCode) =>
        Pipeline.ReportLegacyExit(exeName, formFqn, exitCode);

    /// <summary>
    /// Nahlásí událost bez výjimky (selhaný update stanice, chybějící host modulů) s vlastním typem,
    /// modulem a jménem. <paramref name="details"/> jde do <c>data</c>, základní údaje nepřepíše.
    /// Na odeslání čeká i ukončení procesu (<c>BackgroundWrites</c>). Volatelné z libovolného vlákna. Nikdy nehází.
    /// </summary>
    public static void ReportEvent(ErrorReportType type, string module, string name, string message, JsonObject? details) =>
        Pipeline.ReportEvent(type, module, name, message, details);

    /// <summary>Odešle dobrovolné doplnění od uživatele k už nahlášené chybě.</summary>
    public static void ReportFollowUp(string refId, string userText, Window? owner = null) =>
        Pipeline.ReportFollowUp(refId, userText, owner);
}
