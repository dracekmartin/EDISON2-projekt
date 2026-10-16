using System.IO;
using EDControls.EDSupportPanel;
using EDControls.EDTheme;
using EDControls.EDViewModel;
using EDControls.UserFonts;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.SupportStrip;

/// <summary>
/// WebView2 respektující user velikost fontu přes <see cref="WebView2.ZoomFactor"/> -
/// HTML obsah je mimo dosah WPF font tokenů. Rodina fontu se nepropaguje
/// (CSS injektáž do cizí stránky je křehká).
/// V dark modu injektuje inverzní CSS filtr - widgety podpora.m-line.cz jsou natvrdo světlé
/// (bg-white, žádný prefers-color-scheme), takže tmavou variantu musí vyrobit klient.
/// </summary>
/// <remarks>HWND <see cref="WebView2"/>, ne <see cref="WebView2CompositionControl"/> - composition
/// nepřežije reparent do plovoucího okna AvalonDocku (crash na Direct3D11CaptureFramePool).
/// Cena airspace: WPF závoj dimu HWND nepokryje, kartu „Akce probíhá" nad ním kreslí owned okno.</remarks>
internal sealed class FontZoomWebView : WebView2, IDisposable
{
    // Základní text widgetů podpora.m-line.cz je 14 CSS px (Tailwind `text-sm` na <body>) -
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

    // Id pre-create skriptu (AddScriptToExecuteOnDocumentCreated) - null = neregistrován.
    private string? _darkScriptId;

    // URL k navigaci až po inicializaci Core + registraci dark skriptu (viz Navigate).
    private string? _pendingUrl;

    // Poslední URL, kterou si vyžádal hostitel - jen ta smí zůstat v pruhu (viz OnNavigationStarting).
    private string? _widgetUrl;

    // Default je složka vedle exe: na TS, share a v Program Files do ní uživatel nezapíše (pruh zůstane
    // prázdný) a na TS by v ní všichni uživatelé sdíleli cookies a sezení widgetu.
    internal static string UserDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "m-line", "EDISONMAIN2", "WebView2");

    public FontZoomWebView()
    {
        // Jen před EnsureCoreWebView2Async - wrapper z nich volá CoreWebView2Environment.CreateAsync, později je ignoruje.
        CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = UserDataFolder };
        // Hodnota nastavená před inicializací CoreWebView2 se aplikuje po ní (WPF wrapper).
        ZoomFactor = FontService.BodySize / WidgetBaseFontPx;
        DefaultBackgroundColor = BackgroundFor(ThemeService.Current);
        // V dark modu skryj plochu, dokud nedoběhne první navigace: bílý HWND WebView2 probleskne
        // dřív, než se aplikuje DefaultBackgroundColor / invert filtr. Skrytý → prosvítá tmavý host.
        if (ThemeService.Current == AppTheme.Dark)
            Visibility = System.Windows.Visibility.Hidden;
        FontService.Applied += OnFontApplied;
        ThemeService.Applied += OnThemeApplied;
        CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
    }

    /// <summary>Pozadí pod HTML obsahem - v dark tmavé (EDTheme.Bg.Base), jinak by při
    /// načítání widgetu probleskovala bílá. Čte živý token pro aktuálně aplikované téma;
    /// fallback literály drží per-téma význam i bez WPF Application (testy).</summary>
    internal static System.Drawing.Color BackgroundFor(AppTheme theme)
    {
        if (theme == ThemeService.Current
            && System.Windows.Application.Current?.TryFindResource("EDTheme.Bg.Base.Color") is System.Windows.Media.Color c)
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        return theme == AppTheme.Dark
            ? System.Drawing.Color.FromArgb(0x1E, 0x1E, 0x1E)
            : System.Drawing.Color.White;
    }

    /// <summary>Naviguje na URL až po inicializaci Core a registraci pre-create dark skriptu. Přímé
    /// <c>Source =</c> spustí navigaci dřív, než se dark skript stihne zaregistrovat → první dokument
    /// probleskne světlý, teprve pak ho NavigationCompleted ztmaví (bílo-černé bliknutí v dark modu).</summary>
    public void Navigate(string url)
    {
        _widgetUrl = url;
        if (CoreWebView2 is not null)
        {
            CoreWebView2.Navigate(url);
            return;
        }
        _pendingUrl = url;
        _ = EnsureCoreWebView2Async(); // navigaci provede OnCoreWebView2Ready až po SyncDarkScriptAsync
    }

    private void OnFontApplied() => ZoomFactor = FontService.BodySize / WidgetBaseFontPx;

    private async void OnCoreWebView2Ready(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        // NavigationCompleted je pojistka - první dokument může vzniknout dřív, než se
        // pre-create skript stihne zaregistrovat (JS je idempotentní, dvojí běh nevadí).
        CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        // Odkazy widgetu jsou target="_blank" -> NewWindowRequested. Bez obsluhy je WebView2
        // otevře ve vlastním popup okně: bez adresního řádku, historie i session uživatele.
        CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        CoreWebView2.NavigationStarting += OnNavigationStarting;
        ApplyPreferredColorScheme();
        await SyncDarkScriptAsync();
        if (_pendingUrl is not null)
        {
            CoreWebView2.Navigate(_pendingUrl);
            _pendingUrl = null;
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (ThemeService.Current == AppTheme.Dark)
            await TryExecuteAsync(ApplyDarkJs);
        // Odkryj až teď - obsah je vykreslený a (v dark) invertovaný, takže žádné bílé bliknutí.
        Visibility = System.Windows.Visibility.Visible;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true; // popup okno WebView2 vůbec nevznikne
        OpenExternally(e.Uri);
    }

    // Pojistka pro odkaz bez target="_blank" (obsah widgetu se mění na serveru) - klik by jinak
    // přenavigoval pruh na portál.
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!ShouldOpenExternally(_widgetUrl, e.IsRedirected, e.Uri)) return;
        e.Cancel = true;
        OpenExternally(e.Uri);
    }

    /// <summary>V pruhu smí zůstat jen URL, kterou si vyžádal hostitel (<see cref="Navigate"/>),
    /// a její přesměrování; všechno ostatní je odkaz z obsahu. Rozlišovat podle
    /// <c>IsUserInitiated</c> nejde - WebView2 ho nastaví i na hostitelskou navigaci,
    /// takže by se zrušilo první načtení a pruh by zůstal prázdný.</summary>
    internal static bool ShouldOpenExternally(string? widgetUrl, bool isRedirected, string uri) =>
        !isRedirected
        && !string.Equals(uri, widgetUrl, StringComparison.OrdinalIgnoreCase)
        && IsBrowsableScheme(uri);

    /// <summary>Allowlist schémat pro ShellExecute - vzdálené HTML nesmí spustit libovolný
    /// registrovaný protokol (file:, ms-settings:, ...).</summary>
    internal static bool IsBrowsableScheme(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeMailto);

    private static void OpenExternally(string url)
    {
        if (!IsBrowsableScheme(url)) return;
        try
        {
            SupportActions.OpenUrl(url);
        }
        catch (Exception)
        {
            // Chybějící asociace prohlížeče není chyba aplikace (nehlásit do telemetrie), ale
            // klik musí dát zpětnou vazbu - jinak vypadá odkaz jako mrtvý.
            EDServices.Current.PopupDisplayer.Info("Nepodařilo se otevřít prohlížeč.");
        }
    }

    private async void OnThemeApplied()
    {
        DefaultBackgroundColor = BackgroundFor(ThemeService.Current);
        ApplyPreferredColorScheme();
        await SyncDarkScriptAsync();
        // Přepnutí za běhu - už načtený dokument se přestyluje hned, bez reloadu.
        await TryExecuteAsync(ThemeService.Current == AppTheme.Dark ? ApplyDarkJs : RemoveDarkJs);
    }

    /// <summary>Naváže nativní prvky Chromia (scrollbar, form controls, prefers-color-scheme) na
    /// naše téma, ne na téma Windows OS. Default profilu je Auto = řídí se OS → scrollbar by u
    /// uživatelů s různým systémovým tématem vycházel různě (a nezávisle na našem dark modu).</summary>
    private void ApplyPreferredColorScheme()
    {
        var core = CoreWebView2;
        if (core is null) return;
        core.Profile.PreferredColorScheme = ThemeService.Current == AppTheme.Dark
            ? CoreWebView2PreferredColorScheme.Dark
            : CoreWebView2PreferredColorScheme.Light;
    }

    /// <summary>Drží pre-create skript v souladu s theme - nové navigace se vykreslí rovnou tmavě.</summary>
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
            // Zavíraný widget / ukončený browser proces - příští theme toggle to dožene.
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

    // Base Dispose() není virtuální - re-deklarace IDisposable přesměruje i interface
    // dispatch (SimpleMiniAppViewModel.OnClosed) na tuhle verzi.
    public new void Dispose()
    {
        // Statické eventy by jinak držely zavřené widgety naživu.
        FontService.Applied -= OnFontApplied;
        ThemeService.Applied -= OnThemeApplied;
        base.Dispose();
    }
}
