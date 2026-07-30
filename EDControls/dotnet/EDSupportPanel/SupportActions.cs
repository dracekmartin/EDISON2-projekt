using System.Diagnostics;
using System.Reflection;

namespace EDControls.EDSupportPanel;

/// <summary>
/// Akce panelu Podpora bez závislosti na WPF — skládání URL widgetů podpora.m-line.cz
/// a textu „Informace o verzi". Testuje se přímo, panel jen volá.
/// </summary>
public static class SupportActions
{
    // Záměrně static readonly, ne const: const se inlinuje do konzumenta při buildu, takže
    // samostatně nasazená EDControls.dll (viz root CLAUDE.md) by klíč nezměnila shellu, který
    // se nepřebuildil — a „jedna kopie" by platila jen ve zdrojáku.

    /// <summary>Base widgetů podpora.m-line.cz. Sdílí ho i shell (<c>WidgetUrls</c>).</summary>
    public static readonly string WidgetBaseUrl = "https://podpora.m-line.cz/api/widgets/";

    /// <summary>Statický klíč widget API (není to tajemství, widgety ho mají v URL).</summary>
    public static readonly string WidgetKey = "80716cc86270f1daff6a18ad0b2ba07e9e30011eea22072c7a840604b18423c6";

    /// <summary>Portál uživatelské podpory (helpdesk, wiki).</summary>
    public static readonly string PortalUrl = "https://podpora.m-line.cz";

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
    /// jen aplikaci a verzi. Prázdné <paramref name="appVersion"/> → verze assembly,
    /// což je fallback pro aplikace bez VERZE.INF (TemplateDesigner).
    /// </summary>
    public static string BuildVersionInfo(IMLRest? mlRest, string? appName, string? appVersion)
    {
        var name = string.IsNullOrWhiteSpace(appName) ? DefaultAppName() : appName;
        var version = string.IsNullOrWhiteSpace(appVersion) ? DefaultAppVersion() : appVersion;

        var info = $"{name}\n\nVerze: {version}";
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

    private static string DefaultAppName() =>
        Assembly.GetEntryAssembly()?.GetName().Name ?? "M-line";

    private static string DefaultAppVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "nezjištěna";
}
