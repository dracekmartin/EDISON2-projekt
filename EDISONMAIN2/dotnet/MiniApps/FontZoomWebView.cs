using System.Windows;
using EDControls;
using EDControls.EDTheme;
using EDControls.UserFonts;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// WebView2 respektující user velikost fontu přes <see cref="WebView2.ZoomFactor"/> —
/// HTML obsah je mimo dosah WPF font tokenů. Rodina fontu se nepropaguje
/// (CSS injektáž do cizí stránky je křehká).
/// V dark modu injektuje inverzní CSS filtr — widgety podpora.m-line.cz jsou natvrdo světlé
/// (bg-white, žádný prefers-color-scheme), takže tmavou variantu musí vyrobit klient.
/// </summary>
/// <remarks>
/// HWND <see cref="WebView2"/>, ne <see cref="WebView2CompositionControl"/> — composition
/// nepřežije reparent do plovoucího okna AvalonDocku (drag/reorder tabu → crash na
/// Direct3D11CaptureFramePool) a tahá 23 MB WinRT projekci (Microsoft.Windows.SDK.NET).
/// Cena HWND: kreslí přes WPF (airspace), takže po dobu dimu (overlay „Akce probíhá",
/// modální/legacy dialog) se sám schová (<see cref="WindowDimmer.DimStateChanged"/>) —
/// jinak přes ztmavený shell zůstane vidět i klikatelný. Rám docku zůstává.
/// </remarks>
internal sealed class FontZoomWebView : WebView2, IDisposable
{
    // Základní text widgetů podpora.m-line.cz je 14 CSS px (Tailwind `text-sm` na <body>) —
    // zoom ho přepočítá na aktuální WPF Body velikost, aby text seděl se zbytkem aplikace.
    private const double WidgetBaseFontPx = 14;

    // Inverze celé stránky; obrázky/video druhá inverze vrátí do originálu. body se před
    // inverzí přebarví na světle šedou, aby výsledné pozadí nebylo tvrdě černé (~#161616).
    private const string DarkCss =
        "html{filter:invert(1) hue-rotate(180deg)}" +
        "body{background:#e9e9e9 !important}" +
        "img,video,canvas{filter:invert(1) hue-rotate(180deg)}";

    private const string ApplyDarkJs =
        "(function(){if(document.getElementById('ed-dark'))return;" +
        "var s=document.createElement('style');s.id='ed-dark';" +
        "s.textContent='" + DarkCss + "';" +
        "(document.head||document.documentElement).appendChild(s);})();";

    private const string RemoveDarkJs =
        "(function(){var s=document.getElementById('ed-dark');if(s)s.remove();})();";

    // Id pre-create skriptu (AddScriptToExecuteOnDocumentCreated) — null = neregistrován.
    private string? _darkScriptId;

    public FontZoomWebView()
    {
        // Hodnota nastavená před inicializací CoreWebView2 se aplikuje po ní (WPF wrapper).
        ZoomFactor = FontService.BodySize / WidgetBaseFontPx;
        DefaultBackgroundColor = BackgroundFor(ThemeService.Current);
        FontService.Applied += OnFontApplied;
        ThemeService.Applied += OnThemeApplied;
        WindowDimmer.DimStateChanged += OnDimStateChanged;
        CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
    }

    // Airspace: HWND WebView2 nelze překrýt WPF veilem. Po dobu dimu vlastního okna se schovej
    // (Hidden = drží layout, rám tabů zůstává), jinak přes ztmavený shell svítíš a jsi klikatelný.
    private void OnDimStateChanged(Window window, bool dimmed)
    {
        if (ReferenceEquals(window, Window.GetWindow(this)))
            Visibility = dimmed ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>Pozadí pod HTML obsahem — v dark tmavé (EDTheme.Bg.Base #1E1E1E),
    /// jinak by při načítání widgetu probleskovala bílá.</summary>
    internal static System.Drawing.Color BackgroundFor(AppTheme theme) =>
        theme == AppTheme.Dark ? System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E) : System.Drawing.Color.White;

    private void OnFontApplied() => ZoomFactor = FontService.BodySize / WidgetBaseFontPx;

    private async void OnCoreWebView2Ready(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        // NavigationCompleted je pojistka — první dokument může vzniknout dřív, než se
        // pre-create skript stihne zaregistrovat (JS je idempotentní, dvojí běh nevadí).
        CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        await SyncDarkScriptAsync();
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (ThemeService.Current == AppTheme.Dark)
            await TryExecuteAsync(ApplyDarkJs);
    }

    private async void OnThemeApplied()
    {
        DefaultBackgroundColor = BackgroundFor(ThemeService.Current);
        await SyncDarkScriptAsync();
        // Přepnutí za běhu — už načtený dokument se přestyluje hned, bez reloadu.
        await TryExecuteAsync(ThemeService.Current == AppTheme.Dark ? ApplyDarkJs : RemoveDarkJs);
    }

    /// <summary>Drží pre-create skript v souladu s theme — nové navigace se vykreslí rovnou tmavě.</summary>
    private async Task SyncDarkScriptAsync()
    {
        var core = CoreWebView2;
        if (core is null) return;
        try
        {
            if (ThemeService.Current == AppTheme.Dark)
                _darkScriptId ??= await core.AddScriptToExecuteOnDocumentCreatedAsync(ApplyDarkJs);
            else if (_darkScriptId is not null)
            {
                core.RemoveScriptToExecuteOnDocumentCreated(_darkScriptId);
                _darkScriptId = null;
            }
        }
        catch (Exception)
        {
            // Zavíraný widget / ukončený browser proces — příští theme toggle to dožene.
        }
    }

    private async Task TryExecuteAsync(string script)
    {
        var core = CoreWebView2;
        if (core is null) return;
        try { await core.ExecuteScriptAsync(script); }
        catch (Exception)
        {
            // Zavíraný widget / ukončený browser proces.
        }
    }

    // Base Dispose() není virtuální — re-deklarace IDisposable přesměruje i interface
    // dispatch (SimpleMiniAppViewModel.OnClosed) na tuhle verzi.
    public new void Dispose()
    {
        // Statické eventy by jinak držely zavřené widgety naživu.
        FontService.Applied -= OnFontApplied;
        ThemeService.Applied -= OnThemeApplied;
        WindowDimmer.DimStateChanged -= OnDimStateChanged;
        base.Dispose();
    }
}
