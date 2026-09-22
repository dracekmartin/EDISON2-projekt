using System.Windows;
using EDControls;

namespace EDControls.Tests;

/// <summary>
/// Clamp na dnešní plochu a aplikace uložené geometrie na okno. Wire formát legacy úložiště
/// testuje <see cref="CacheWindowStateStoreTests"/>.
/// </summary>
public class WindowStatePersistenceTests
{
    /// <summary>Plocha jednoho monitoru 1920x1080 v počátku (0;0).</summary>
    private static readonly Rect SingleScreen = new(0, 0, 1920, 1080);

    /// <summary>Dva monitory 1920x1080, sekundární vlevo od primárního (negativní origin).</summary>
    private static readonly Rect SecondaryOnLeft = new(-1920, 0, 3840, 1080);

    [Theory]
    [InlineData(100, 100, true)]        // uvnitř plochy
    [InlineData(0, 0, true)]            // levý horní roh
    [InlineData(1800, 1048, true)]      // pruh titulku končí přesně na hraně
    [InlineData(1801, 1048, false)]     // o pixel za pravou hranou
    [InlineData(1800, 1049, false)]     // o pixel za spodní hranou
    [InlineData(2100, 200, false)]      // odpojený monitor vpravo
    [InlineData(-1800, 100, false)]     // odpojený monitor vlevo
    [InlineData(100, -50, false)]       // nad plochou
    public void IsLocationReachable_SingleScreen(double left, double top, bool expected)
    {
        Assert.Equal(expected, WindowStatePersistence.IsLocationReachable(left, top, SingleScreen));
    }

    /// <summary>Dva monitory 1920x1080, sekundární vpravo od primárního (hlášený případ).</summary>
    private static readonly Rect SecondaryOnRight = new(0, 0, 3840, 1080);

    [Theory]
    [InlineData(-1800, 100, true)]      // okno na sekundárním monitoru vlevo
    [InlineData(-1920, 0, true)]        // levý horní roh plochy
    [InlineData(1000, 100, true)]       // přes hranu obou monitorů
    [InlineData(-2000, 100, false)]     // vlevo mimo plochu
    [InlineData(1900, 100, false)]      // za pravou hranou plochy
    public void IsLocationReachable_SecondaryOnLeft(double left, double top, bool expected)
    {
        Assert.Equal(expected, WindowStatePersistence.IsLocationReachable(left, top, SecondaryOnLeft));
    }

    [Theory]
    [InlineData(2000, 100, true)]       // pravá polovina plochy - přesně to, co legacy zahazoval
    [InlineData(3700, 1000, true)]      // u pravého dolního okraje sekundárního monitoru
    [InlineData(3800, 100, false)]      // pruh titulku už nemá kam
    public void IsLocationReachable_SecondaryOnRight(double left, double top, bool expected)
    {
        Assert.Equal(expected, WindowStatePersistence.IsLocationReachable(left, top, SecondaryOnRight));
    }

    [Fact]
    public void IsLocationReachable_RejectsNonFinite()
    {
        // "NaN" projde parsováním (NumberStyles.Float), zastavit ho musí až tahle kontrola.
        Assert.False(WindowStatePersistence.IsLocationReachable(double.NaN, 100, SingleScreen));
        Assert.False(WindowStatePersistence.IsLocationReachable(100, double.NaN, SingleScreen));
        Assert.False(WindowStatePersistence.IsLocationReachable(double.PositiveInfinity, 0, SingleScreen));
    }

    // Testy níž se záměrně nedotýkají pozice - ta prochází IsLocationReachable proti skutečné
    // ploše stroje a test by pak závisel na tom, kde běží.

    [Fact]
    public void Attach_BeforeShow_NormalizesMaximizedWindow()
    {
        // Jádro opravy: HWND vytvořený s WS_MAXIMIZE Win32 zvětšit ani zmenšit nenechá, takže
        // uložená velikost by skončila jen v restore bounds. Attach proto stav sráží dřív,
        // než HWND vůbec vznikne. Okno se nezobrazuje, takže se na úložiště nesáhne.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { WindowState = WindowState.Maximized };

            WindowStatePersistence.Attach(window, "TEST.Window");

            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

    [Fact]
    public void ApplyToWindow_NothingStored_RestoresXamlState()
    {
        // Nic uloženého - stav sražený v Attach se musí vrátit, jinak by shell přestal
        // startovat maximalizovaný.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, WindowGeometry.None, WindowState.Maximized);

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(1200, window.Width);
            Assert.Equal(800, window.Height);
        });
    }

    [Fact]
    public void ApplyToWindow_WithoutStoredState_RestoresXamlState()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, new(null, null, 800, 600, null), WindowState.Maximized);

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(800, window.Width);
            Assert.Equal(600, window.Height);
        });
    }

    [Fact]
    public void ApplyToWindow_StoredNormalState_WinsOverXamlMaximized()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, new(null, null, null, null, false), WindowState.Maximized);

            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }
}
