using System.Threading;
using System.Windows;
using EDControls;

namespace EDControls.Tests;

/// <summary>
/// Testy čisté wire logiky persistence stavu okna (ParseOutput / TryParsePair / BuildSaveParams).
/// ApplyToWindow se netestuje - vyžaduje Window (STA).
/// </summary>
public class WindowStatePersistenceTests
{
    private const char D = '~';

    [Fact]
    public void ParseOutput_ExtractsParamLines_IgnoresNonParamAndShort()
    {
        object[] output =
        [
            "STATUS",                       // index 0 - přeskočí se
            $"param{D}location{D}100;200",
            $"param{D}size{D}800;600",
            $"param{D}state{D}1",
            $"noise{D}x{D}y",               // ne "param" → ignorováno
            $"param{D}bad",                 // < 3 částí → ignorováno
        ];

        var dict = WindowStatePersistence.ParseOutput(output, D);

        Assert.Equal(3, dict.Count);
        Assert.Equal("100;200", dict["location"]);
        Assert.Equal("800;600", dict["size"]);
        Assert.Equal("1", dict["state"]);
    }

    [Fact]
    public void ParseOutput_SkipsIndexZero()
    {
        // Index 0 je status slot - i kdyby vypadal jako param, nesmí se číst.
        object[] output = [$"param{D}location{D}999;999"];

        var dict = WindowStatePersistence.ParseOutput(output, D);

        Assert.Empty(dict);
    }

    [Theory]
    [InlineData("100;200", true)]
    [InlineData("10.5;20.5", true)]
    [InlineData("abc", false)]
    [InlineData("1;2;3", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void TryParsePair_ValidatesFormat(string input, bool expected)
    {
        Assert.Equal(expected, WindowStatePersistence.TryParsePair(input, out _, out _));
    }

    [Fact]
    public void TryParsePair_ParsesInvariantCulture()
    {
        // Desetinná tečka musí projít bez ohledu na cs-CZ (čárka) locale.
        Assert.True(WindowStatePersistence.TryParsePair("10.5;20.25", out var a, out var b));
        Assert.Equal(10.5, a);
        Assert.Equal(20.25, b);
    }

    [Fact]
    public void BuildSaveParams_ProducesWireFormat()
    {
        var input = WindowStatePersistence.BuildSaveParams("EDISON2.Foo", new Rect(10, 20, 800, 600), 0, D);

        Assert.Equal(
        [
            $"formID{D}EDISON2.Foo",
            $"clear{D}1",
            $"param{D}location{D}10;20",
            $"param{D}size{D}800;600",
            $"param{D}state{D}0",
        ], input.Select(o => o.ToString()));
    }

    [Fact]
    public void BuildSaveParams_MaximizedStateIsOne()
    {
        var input = WindowStatePersistence.BuildSaveParams("F", new Rect(0, 0, 100, 100), 1, D);

        Assert.Equal($"param{D}state{D}1", input[^1].ToString());
    }

    [Fact]
    public void BuildSaveParams_NegativeLocationSurvives()
    {
        // Monitor vlevo od primárního má negativní X - nesmí se cestou ztratit.
        var input = WindowStatePersistence.BuildSaveParams("F", new Rect(-1800, -120, 800, 600), 0, D);

        Assert.Equal($"param{D}location{D}-1800;-120", input[2].ToString());
    }

    [Fact]
    public void SaveLoadRoundTrip_KeepsNegativeLocation()
    {
        var saved = WindowStatePersistence.BuildSaveParams("F", new Rect(-1800, -120, 800, 600), 0, D);

        // ParseOutput přeskakuje index 0 (status slot), takže formID zastoupí status.
        var dict = WindowStatePersistence.ParseOutput(saved, D);

        Assert.True(WindowStatePersistence.TryParsePair(dict["location"], out var x, out var y));
        Assert.Equal(-1800, x);
        Assert.Equal(-120, y);
    }

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
        // "NaN" projde TryParsePair (NumberStyles.Float), zastavit ho musí až tahle kontrola.
        Assert.False(WindowStatePersistence.IsLocationReachable(double.NaN, 100, SingleScreen));
        Assert.False(WindowStatePersistence.IsLocationReachable(100, double.NaN, SingleScreen));
        Assert.False(WindowStatePersistence.IsLocationReachable(double.PositiveInfinity, 0, SingleScreen));
    }

    // Testy níž se záměrně nedotýkají klíče "location" - ten prochází IsLocationReachable proti
    // skutečné ploše stroje a test by pak závisel na tom, kde běží.

    [Fact]
    public void Attach_BeforeShow_NormalizesMaximizedWindow()
    {
        // Jádro opravy: HWND vytvořený s WS_MAXIMIZE Win32 zvětšit ani zmenšit nenechá, takže
        // uložená velikost by skončila jen v restore bounds. Attach proto stav sráží dřív,
        // než HWND vůbec vznikne. Okno se nezobrazuje, takže se na MLRest nesáhne.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { WindowState = WindowState.Maximized };

            WindowStatePersistence.Attach(window, "TEST.Window");

            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

    [Fact]
    public void ApplyToWindow_EmptyPrefs_RestoresXamlState()
    {
        // Nic uloženého - stav sražený v Attach se musí vrátit, jinak by shell přestal
        // startovat maximalizovaný.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, [], WindowState.Maximized);

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(1200, window.Width);
            Assert.Equal(800, window.Height);
        });
    }

    [Fact]
    public void ApplyToWindow_WithoutStateKey_RestoresXamlState()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, new() { ["size"] = "800;600" }, WindowState.Maximized);

            Assert.Equal(WindowState.Maximized, window.WindowState);
            Assert.Equal(800, window.Width);
            Assert.Equal(600, window.Height);
        });
    }

    [Fact]
    public void ApplyToWindow_StateZero_WinsOverXamlMaximized()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 1200, Height = 800, WindowState = WindowState.Normal };

            WindowStatePersistence.ApplyToWindow(window, new() { ["state"] = "0" }, WindowState.Maximized);

            Assert.Equal(WindowState.Normal, window.WindowState);
        });
    }

}
