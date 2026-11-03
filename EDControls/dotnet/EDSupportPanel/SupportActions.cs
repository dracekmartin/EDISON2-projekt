using System.Diagnostics;
using System.Reflection;
using EDControls.EDViewModel;

namespace EDControls.EDSupportPanel;

/// <summary>
/// Akce panelu Podpora bez závislosti na WPF - skládání URL widgetů podpora.m-line.cz
/// a textu „Informace o verzi" (menu Nápověda shellu). Testuje se přímo, panel jen volá.
/// </summary>
public static class SupportActions
{
    // Záměrně static readonly, ne const: const se inlinuje do konzumenta při buildu, takže
    // samostatně nasazená EDControls.dll (viz root CLAUDE.md) by klíč nezměnila shellu, který
    // se nepřebuildil - a „jedna kopie" by platila jen ve zdrojáku.

    /// <summary>Base widgetů podpora.m-line.cz. Sdílí ho i shell (<c>WidgetUrls</c>).</summary>
    public static readonly string WidgetBaseUrl = "https://podpora.m-line.cz/api/widgets/";

    /// <summary>Statický klíč widget API (není to tajemství, widgety ho mají v URL).</summary>
    public static readonly string WidgetKey = "80716cc86270f1daff6a18ad0b2ba07e9e30011eea22072c7a840604b18423c6";

    /// <summary>Portál uživatelské podpory (helpdesk, wiki).</summary>
    public static readonly string PortalUrl = "https://podpora.m-line.cz";

    /// <summary>Firemní web M-line.</summary>
    public static readonly string WebUrl = "https://www.m-line.cz";

    // Widget feedback API zahazuje context delší než 255 znaků, proto tvrdé zkrácení.
    private const int ContextMaxLength = 255;

    /// <summary>URL feedback widgetu. Prázdné parametry se vynechávají (mirror legacy SideMenu).</summary>
    public static string BuildFeedbackUrl(string? idfa, string? user, string? agenda, string? context)
    {
        var url = $"{WidgetBaseUrl}feedback?key={WidgetKey}";
        url += Param("idfa", idfa);
        url += Param("user", user);
        url += Param("agenda", agenda);
        url += Param("context", context);
        return url;
    }

    /// <summary>
    /// URL feedbacku z okna <paramref name="ownerType"/>: uživatel ze služeb, <c>agenda</c> = assembly
    /// okna, <c>v=</c> = verze aplikace z <see cref="AppIdentity"/>. Verze assembly okna je všude 1.0.0.0.
    /// </summary>
    internal static string BuildWindowFeedbackUrl(EDServices services, Type ownerType, string? ownerTitle, Assembly? entry) =>
        BuildFeedbackUrl(
            services.MLRest?.Idfa,
            services.MLRest?.LoginName,
            ownerType.Assembly.GetName().Name,
            BuildContext(ownerType.Name, ownerTitle, ownerType.Namespace, AppIdentity.Resolve(services, entry).Version));

    /// <summary>
    /// URL tlačítek Nápověda a Novinky: widget podpora.m-line.cz přesměruje do Nápovědy agendy, kterou
    /// najde podle <paramref name="agenda"/> (kód modulu = assembly okna) v Kódech modulů číselníku agend,
    /// stejně jako zpětnou vazbu; neznámý nebo prázdný kód vede na úvod Nápovědy.
    /// </summary>
    internal static string BuildHelpUrl(string? agenda, SupportHelpSection section) =>
        $"{WidgetBaseUrl}help/open?key={WidgetKey}" + Param("agenda", agenda) +
        "&section=" + (section == SupportHelpSection.Articles ? "articles" : "changes");

    /// <summary>URL Nápovědy či Novinek z okna <paramref name="ownerType"/> - agenda jako u zpětné vazby.</summary>
    internal static string BuildWindowHelpUrl(Type ownerType, SupportHelpSection section) =>
        BuildHelpUrl(ownerType.Assembly.GetName().Name, section);

    /// <summary>
    /// Kontext okna pro feedback widget: <c>form=…|title=…|ns=…|v=…</c>. Svislítko je
    /// oddělovač, proto se z titulku nahrazuje lomítkem.
    /// </summary>
    public static string BuildContext(string formName, string? title, string? ns, string? version)
    {
        var parts = new List<string> { "form=" + formName };
        var cleanTitle = (title ?? "").Trim().Replace("|", "/");
        if (cleanTitle.Length > 0) parts.Add("title=" + cleanTitle);
        if (!string.IsNullOrEmpty(ns)) parts.Add("ns=" + ns);
        if (!string.IsNullOrEmpty(version)) parts.Add("v=" + version);

        var ctx = string.Join("|", parts);
        return ctx.Length > ContextMaxLength ? ctx[..ContextMaxLength] : ctx;
    }

    /// <summary>
    /// Text dialogu „Informace o verzi". Bez <paramref name="mlRest"/> (pre-login) vypíše
    /// jen aplikaci a verzi. Prázdné <paramref name="appName"/> / <paramref name="appVersion"/>
    /// doplní <see cref="AppIdentity"/> z entry assembly, což je fallback pro aplikace bez
    /// VERZE.INF (TemplateDesigner).
    /// </summary>
    public static string BuildVersionInfo(IMLRest? mlRest, string? appName, string? appVersion) =>
        BuildVersionInfo(mlRest, appName, appVersion, Assembly.GetEntryAssembly());

    internal static string BuildVersionInfo(IMLRest? mlRest, string? appName, string? appVersion, Assembly? entry)
    {
        var app = AppIdentity.Resolve(null, appName, appVersion, entry);

        var info = $"{app.DisplayName}\n\nVerze: {app.Version}";
        if (mlRest is null) return info;

        return info +
               $"\nServer: {mlRest.Server}:{mlRest.Port}" +
               $"\nNamespace: {mlRest.DataNamespace}" +
               $"\nFirma: {mlRest.Idfa}" +
               $"\nUživatel: {mlRest.FullName} ({mlRest.LoginName})";
    }

    /// <summary>Otevře URL v systémovém prohlížeči.</summary>
    public static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static string Param(string name, string? value) =>
        string.IsNullOrEmpty(value) ? "" : $"&{name}={Uri.EscapeDataString(value)}";
}

/// <summary>Sekce agendy v Nápovědě, kterou otevírá tlačítko panelu.</summary>
internal enum SupportHelpSection
{
    /// <summary>Články (postupy, popis oken) - tlačítko Nápověda.</summary>
    Articles,

    /// <summary>Novinky (vydání) - tlačítko Novinky.</summary>
    Changes,
}
