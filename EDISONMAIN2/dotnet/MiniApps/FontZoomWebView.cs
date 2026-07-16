using EDControls.UserFonts;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// WebView2 respektující user velikost fontu přes <see cref="WebView2CompositionControl.ZoomFactor"/> —
/// HTML obsah je mimo dosah WPF font tokenů. Rodina fontu se nepropaguje
/// (CSS injektáž do cizí stránky je křehká).
/// </summary>
/// <remarks>
/// Dědí z <see cref="WebView2CompositionControl"/>, ne <see cref="WebView2"/> — HWND varianta
/// kreslí nad veškerým WPF obsahem (airspace) a překrývala overlay „Akce probíhá".
/// </remarks>
internal sealed class FontZoomWebView : WebView2CompositionControl, IDisposable
{
    // Základní text widgetů podpora.m-line.cz je 14 CSS px (Tailwind `text-sm` na <body>) —
    // zoom ho přepočítá na aktuální WPF Body velikost, aby text seděl se zbytkem aplikace.
    private const double WidgetBaseFontPx = 14;

    public FontZoomWebView()
    {
        // Hodnota nastavená před inicializací CoreWebView2 se aplikuje po ní (WPF wrapper).
        ZoomFactor = FontService.BodySize / WidgetBaseFontPx;
        FontService.Applied += OnFontApplied;
    }

    private void OnFontApplied() => ZoomFactor = FontService.BodySize / WidgetBaseFontPx;

    // Base Dispose() není virtuální — re-deklarace IDisposable přesměruje i interface
    // dispatch (SimpleMiniAppViewModel.OnClosed) na tuhle verzi.
    public new void Dispose()
    {
        // Statický event by jinak držel zavřené widgety naživu.
        FontService.Applied -= OnFontApplied;
        base.Dispose();
    }
}
