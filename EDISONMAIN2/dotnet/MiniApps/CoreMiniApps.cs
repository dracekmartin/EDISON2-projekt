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

    public IEnumerable<MiniAppDescriptor> GetMiniApps() =>
    [
        new("Core.Novinky", "Novinky", () => new SimpleMiniAppViewModel(
            "Novinky", () => Widget(WidgetUrls.Novinky))),
        new("Core.Wiki", "Wiki", () => new SimpleMiniAppViewModel(
            "Wiki", () => Widget(WidgetUrls.Wiki))),
        new("Core.Skoleni", "Školení", () => new SimpleMiniAppViewModel(
            "Školení", TrainingsWidget)),
        new("Core.AutoRefresh", "Automatický refresh", () => new PeriodicRefreshMiniAppViewModel()),
    ];

    private static FontZoomWebView Widget(string url)
    {
        var wv = new FontZoomWebView();
        wv.Navigate(url);
        return wv;
    }

    /// <summary>Školení widget dostává IdFa + přihlášeného uživatele (kvůli zpětné vazbě) — mirror frmMain.vb.</summary>
    private static FontZoomWebView TrainingsWidget()
    {
        var mlRest = EDServices.Current.MLRest;
        return Widget(WidgetUrls.Trainings(mlRest?.Idfa ?? "", mlRest?.LoginName ?? ""));
    }
}
