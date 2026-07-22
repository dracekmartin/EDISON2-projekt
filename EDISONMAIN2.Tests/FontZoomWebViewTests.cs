using EDControls.EDTheme;
using EDISONMAIN2.MiniApps;
using Microsoft.Web.WebView2.Wpf;

namespace EDISONMAIN2.Tests;

public class FontZoomWebViewTests
{
    [Fact]
    public void DerivesFromHwndWebView2_NotComposition()
    {
        // HWND varianta — composition nepřežije reparent do plovoucího okna AvalonDocku
        // (drag tabu) a tahá 23 MB WinRT projekci. Airspace řeší owned okno EDActionInProgress
        // (karta nad widgety) — widgety se při dimu neschovávají, viz FontZoomWebView remarks.
        Assert.True(typeof(WebView2).IsAssignableFrom(typeof(FontZoomWebView)));
        Assert.False(typeof(WebView2CompositionControl).IsAssignableFrom(typeof(FontZoomWebView)));
    }

    [Fact]
    public void ReimplementsIDisposable_InterfaceDispatchHitsDerivedDispose()
    {
        // Base Dispose() není virtuální — bez re-implementace IDisposable by
        // SimpleMiniAppViewModel.OnClosed (interface dispatch) volal base
        // a odhlášení z FontService.Applied by se tiše přeskočilo.
        var map = typeof(FontZoomWebView).GetInterfaceMap(typeof(IDisposable));
        var target = Assert.Single(map.TargetMethods);
        Assert.Equal(typeof(FontZoomWebView), target.DeclaringType);
    }

    [Fact]
    public void BackgroundFor_DarkIsThemeBase_LightIsWhite()
    {
        // Bílé DefaultBackgroundColor by v dark modu probleskovalo při načítání widgetu.
        Assert.Equal(unchecked((int)0xFF1E1E1E), FontZoomWebView.BackgroundFor(AppTheme.Dark).ToArgb());
        Assert.Equal(unchecked((int)0xFFFFFFFF), FontZoomWebView.BackgroundFor(AppTheme.Light).ToArgb());
    }
}
