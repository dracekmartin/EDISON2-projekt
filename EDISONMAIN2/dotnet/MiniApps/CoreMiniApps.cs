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
        // Pinned karusel = garance, že IT novinky uživatel nikdy trvale nezavře (dřív IT
        // nemělo kanál, jak novinky sdělovat). Samostatné widgety níže zůstávají pro
        // uživatele, kteří chtějí stránky vedle sebe.
        new("Core.Podpora", "Podpora M-line", () => new SupportCarouselMiniAppViewModel(CarouselPages()),
            Pinned: true),
        new("Core.Novinky", "Novinky", () => new SimpleMiniAppViewModel(
            "Novinky", () => Widget(WidgetUrls.Novinky))),
        new("Core.Wiki", "Wiki", () => new SimpleMiniAppViewModel(
            "Wiki", () => Widget(WidgetUrls.Wiki))),
        new("Core.Skoleni", "Školení", () => new SimpleMiniAppViewModel(
            "Školení", TrainingsWidget)),
        new("Core.AutoRefresh", "Automatický refresh", () => new PeriodicRefreshMiniAppViewModel()),
    ];

    private static IReadOnlyList<CarouselPage> CarouselPages() =>
    [
        new("Novinky", () => Widget(WidgetUrls.Novinky)),
        new("Wiki", () => Widget(WidgetUrls.Wiki)),
        new("Školení", TrainingsWidget),
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
