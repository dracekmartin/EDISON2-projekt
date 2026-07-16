using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Vestavěné shell mini-apps (na rozdíl od dev placeholderů v <see cref="DevSampleMiniApps"/>).
/// Novinky / Wiki / Školení jsou hostované HTML widgety z podpora.m-line.cz — stejné, jaké starý
/// shell renderoval přes CefSharp (frmMain.vb) v centrální ploše. Tady přes WebView2 jako
/// dockovatelné lightweight mini-apps.
/// </summary>
public sealed class CoreMiniApps : IMiniAppProvider
{
    public string GroupTitle => "Podpora M-line";

    private const string WidgetBase = "https://podpora.m-line.cz/api/widgets/";
    private const string WidgetKey = "80716cc86270f1daff6a18ad0b2ba07e9e30011eea22072c7a840604b18423c6";

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Core.Novinky", "Novinky", () => new SimpleMiniAppViewModel(
            "Novinky", () => Widget($"{WidgetBase}info?key={WidgetKey}"))),
        new("Core.Wiki", "Wiki", () => new SimpleMiniAppViewModel(
            "Wiki", () => Widget($"{WidgetBase}wiki?key={WidgetKey}"))),
        new("Core.Skoleni", "Školení", () => new SimpleMiniAppViewModel(
            "Školení", TrainingsWidget)),
        new("Core.AutoRefresh", "Automatický refresh", () => new PeriodicRefreshMiniAppViewModel()),
    ];

    private static FontZoomWebView Widget(string url) => new() { Source = new Uri(url) };

    /// <summary>Školení widget dostává IdFa + přihlášeného uživatele (kvůli zpětné vazbě) — mirror frmMain.vb.</summary>
    private static FontZoomWebView TrainingsWidget()
    {
        var mlRest = EDServices.Current.MLRest;
        var url = $"{WidgetBase}trainings?key={WidgetKey}"
            + $"&idfa={Uri.EscapeDataString(mlRest?.Idfa ?? "")}"
            + $"&user={Uri.EscapeDataString(mlRest?.LoginName ?? "")}";
        return Widget(url);
    }
}
