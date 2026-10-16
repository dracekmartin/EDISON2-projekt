using System.IO;
using EDControls.EDTheme;
using EDISONMAIN2.SupportStrip;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.Tests;

public class FontZoomWebViewTests
{
    [Fact]
    public void DerivesFromHwndWebView2_NotComposition()
    {
        // HWND varianta - composition nepřežije reparent do plovoucího okna AvalonDocku
        // (drag tabu) a tahá 23 MB WinRT projekci. Airspace řeší owned okno EDActionInProgress
        // (karta nad widgety) - widgety se při dimu neschovávají, viz FontZoomWebView remarks.
        Assert.True(typeof(WebView2).IsAssignableFrom(typeof(FontZoomWebView)));
        Assert.False(typeof(WebView2CompositionControl).IsAssignableFrom(typeof(FontZoomWebView)));
    }

    [Fact]
    public void ReimplementsIDisposable_InterfaceDispatchHitsDerivedDispose()
    {
        // Base Dispose() není virtuální - bez re-implementace IDisposable by
        // SimpleMiniAppViewModel.OnClosed (interface dispatch) volal base
        // a odhlášení z FontService.Applied by se tiše přeskočilo.
        var map = typeof(FontZoomWebView).GetInterfaceMap(typeof(IDisposable));
        var target = Assert.Single(map.TargetMethods);
        Assert.Equal(typeof(FontZoomWebView), target.DeclaringType);
    }

    [Fact]
    public void UserDataFolder_PerWindowsUser_NotNextToExe()
    {
        // Vedle exe na TS/share nejde zapisovat a na TS by cookies widgetu sdíleli všichni uživatelé.
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "m-line", "EDISONMAIN2", "WebView2");
        Assert.Equal(expected, FontZoomWebView.UserDataFolder);
        Assert.False(FontZoomWebView.UserDataFolder.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Constructor_SetsPerUserDataFolder()
    {
        // Login okno i pruh shellu vznikají přes new FontZoomWebView() - jiná inicializace WebView2 v shellu není.
        EDTesting.Sta.RunOnAppThread(() =>
        {
            var widget = new FontZoomWebView();
            try
            {
                Assert.Equal(FontZoomWebView.UserDataFolder, widget.CreationProperties?.UserDataFolder);
            }
            finally
            {
                widget.Dispose();
            }
        });
    }

    [Fact]
    public void BackgroundFor_DarkIsThemeBase_LightIsWhite()
    {
        // Bílé DefaultBackgroundColor by v dark modu probleskovalo při načítání widgetu.
        Assert.Equal(unchecked((int)0xFF1E1E1E), FontZoomWebView.BackgroundFor(AppTheme.Dark).ToArgb());
        Assert.Equal(unchecked((int)0xFFFFFFFF), FontZoomWebView.BackgroundFor(AppTheme.Light).ToArgb());
    }

    [Theory]
    [InlineData("https://podpora.m-line.cz/support/wiki/x", true)]
    [InlineData("http://www.m-line.cz/", true)]
    [InlineData("mailto:podpora@m-line.cz", true)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("/relativni/cesta", false)]
    [InlineData("", false)]
    public void IsBrowsableScheme_AllowsOnlyWebAndMail(string url, bool expected)
    {
        // URL sem chodí ze vzdáleného HTML a míří do ShellExecute - bez allowlistu by libovolný
        // registrovaný protokol (file:, ms-settings:) udělal z widgetu spouštěč.
        Assert.Equal(expected, FontZoomWebView.IsBrowsableScheme(url));
    }

    [Fact]
    public void ShouldOpenExternally_KeepsHostRequestedUrl_SendsRestToBrowser()
    {
        const string widget = "https://podpora.m-line.cz/api/widgets/edison2?key=k&idfa=CB_DP";
        // Odkaz z obsahu widgetu.
        Assert.True(FontZoomWebView.ShouldOpenExternally(widget, isRedirected: false,
            "https://podpora.m-line.cz/support/wiki/x"));
        // Navigate() hostitele (první načtení, re-navigace po přepnutí firmy). Rozlišovat podle
        // IsUserInitiated nejde - WebView2 ho nastaví i tady a pruh by zůstal prázdný.
        Assert.False(FontZoomWebView.ShouldOpenExternally(widget, isRedirected: false, widget));
        // Přesměrování uvnitř už povolené navigace.
        Assert.False(FontZoomWebView.ShouldOpenExternally(widget, isRedirected: true,
            "https://podpora.m-line.cz/api/widgets/edison2/v2"));
        Assert.False(FontZoomWebView.ShouldOpenExternally(widget, isRedirected: false, "file:///C:/x"));
    }
}
