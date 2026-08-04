using System.Threading;
using System.Windows;
using AvalonDock.Controls;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// Kdo okno vlastní a co s ním shell smí dělat: <see cref="FrameworkWindows"/> + jeho konzument
/// <see cref="WindowNavigator.ShouldClose"/>. Regrese: `CloseAll` zavíral skryté overlay okno
/// AvalonDocku, `DockingManager` si ho ale cachuje → každý další drag přes dock skončil výjimkou
/// „po zavření okna nelze nastavit Visibility".
/// </summary>
public class WindowOwnershipTests
{
    // ---------- IsFrameworkNamespace ----------

    [Theory]
    [InlineData("MS.Win32")]
    [InlineData("System.Windows")]
    [InlineData("Microsoft.Web.WebView2.Wpf")]   // WebView2 + jiné Microsoft tooling pluginy
    [InlineData("AvalonDock.Controls")]
    public void IsFrameworkNamespace_FrameworkPrefix_True(string ns)
    {
        Assert.True(FrameworkWindows.IsFrameworkNamespace(ns));
    }

    [Fact]
    public void IsFrameworkNamespace_UserApp_False()
    {
        Assert.False(FrameworkWindows.IsFrameworkNamespace("EDISONMAIN2.Windows.EdisonMainWindow"));
    }

    [Fact]
    public void IsFrameworkNamespace_Null_False()
    {
        // Typ v globálním namespace (Namespace == null) → uživatelské okno, ne framework.
        Assert.False(FrameworkWindows.IsFrameworkNamespace(null));
    }

    // ---------- IsFrameworkOwned / IsInternalWindow ----------

    [Fact]
    public void IsFrameworkOwned_DockWindow_True()
    {
        RunSta(() => Assert.True(FrameworkWindows.IsFrameworkOwned(new FakeDockWindow())));
    }

    [Fact]
    public void IsFrameworkOwned_PlainWindow_False()
    {
        // Holé `Window` leží v System.Windows, ale patří aplikaci — panel Podpora ho dostat má
        // a `CloseAll` ho zavřít smí. Okna frameworku jsou vždy podtypy.
        RunSta(() => Assert.False(FrameworkWindows.IsFrameworkOwned(new Window())));
    }

    [Fact]
    public void IsInternalWindow_DockWindow_True()
    {
        // Marker IOverlayWindow EDControls tohle nepokryje — AvalonDock má vlastní typ téhož
        // krátkého názvu, takže `w is not IOverlayWindow` na okně docku projde.
        RunSta(() => Assert.True(WindowNavigator.IsInternalWindow(new FakeDockWindow())));
    }

    // ---------- ShouldClose ----------

    [Fact]
    public void ShouldClose_DockWindow_False()
    {
        RunSta(() => Assert.False(WindowNavigator.ShouldClose(new FakeDockWindow(), new Window())));
    }

    [Fact]
    public void ShouldClose_EDControlsOverlay_False()
    {
        RunSta(() => Assert.False(WindowNavigator.ShouldClose(new FakeOverlayWindow(), new Window())));
    }

    [Fact]
    public void ShouldClose_MainWindow_False()
    {
        RunSta(() =>
        {
            var main = new Window();

            Assert.False(WindowNavigator.ShouldClose(main, main));
        });
    }

    [Fact]
    public void ShouldClose_ModuleWindow_True()
    {
        RunSta(() => Assert.True(WindowNavigator.ShouldClose(new Window(), new Window())));
    }

    private sealed class FakeOverlayWindow : Window, IOverlayWindow;

    // Spustí akci na dedikovaném STA vlákně (Window to vyžaduje) a propaguje výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
