using EDControls.UserFonts;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// WebView2 respektující user velikost fontu přes <see cref="WebView2.ZoomFactor"/> —
/// HTML obsah je mimo dosah WPF font tokenů. Rodina fontu se nepropaguje
/// (CSS injektáž do cizí stránky je křehká).
/// </summary>
internal sealed class FontZoomWebView : WebView2
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

    protected override void Dispose(bool disposing)
    {
        // Statický event by jinak držel zavřené widgety naživu.
        FontService.Applied -= OnFontApplied;
        base.Dispose(disposing);
    }
}
