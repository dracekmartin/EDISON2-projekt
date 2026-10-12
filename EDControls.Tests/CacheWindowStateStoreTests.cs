using System.Windows;
using EDControls;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Wire logika legacy Caché úložiště geometrie okna (ParseOutput / TryParsePair / BuildSaveParams)
/// a její překlad na <see cref="WindowGeometry"/>. Bez Window, čistá funkce.
/// </summary>
public class CacheWindowStateStoreTests
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

        var dict = CacheWindowStateStore.ParseOutput(output, D);

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

        var dict = CacheWindowStateStore.ParseOutput(output, D);

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
        Assert.Equal(expected, CacheWindowStateStore.TryParsePair(input, out _, out _));
    }

    [Fact]
    public void TryParsePair_ParsesInvariantCulture()
    {
        // Desetinná tečka musí projít bez ohledu na cs-CZ (čárka) locale.
        Assert.True(CacheWindowStateStore.TryParsePair("10.5;20.25", out var a, out var b));
        Assert.Equal(10.5, a);
        Assert.Equal(20.25, b);
    }

    [Fact]
    public void BuildSaveParams_ProducesWireFormat()
    {
        var input = CacheWindowStateStore.BuildSaveParams("EDISON2.Foo", new Rect(10, 20, 800, 600), 0, D);

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
        var input = CacheWindowStateStore.BuildSaveParams("F", new Rect(0, 0, 100, 100), 1, D);

        Assert.Equal($"param{D}state{D}1", input[^1].ToString());
    }

    [Fact]
    public void BuildSaveParams_NegativeLocationSurvives()
    {
        // Monitor vlevo od primárního má negativní X - nesmí se cestou ztratit.
        var input = CacheWindowStateStore.BuildSaveParams("F", new Rect(-1800, -120, 800, 600), 0, D);

        Assert.Equal($"param{D}location{D}-1800;-120", input[2].ToString());
    }

    [Fact]
    public void SaveLoadRoundTrip_KeepsNegativeLocation()
    {
        var saved = CacheWindowStateStore.BuildSaveParams("F", new Rect(-1800, -120, 800, 600), 0, D);

        // ParseOutput přeskakuje index 0 (status slot), takže formID zastoupí status.
        var geometry = CacheWindowStateStore.ToGeometry(CacheWindowStateStore.ParseOutput(saved, D));

        Assert.Equal(-1800, geometry.Left);
        Assert.Equal(-120, geometry.Top);
        Assert.Equal(800, geometry.Width);
        Assert.Equal(600, geometry.Height);
        Assert.False(geometry.Maximized);
    }

    [Fact]
    public void ToGeometry_NoRows_IsEmpty()
    {
        Assert.True(CacheWindowStateStore.ToGeometry([]).IsEmpty);
    }

    [Fact]
    public void ToGeometry_MissingStateKeyLeavesMaximizedNull()
    {
        // Null (ne false) - ApplyToWindow na něm pozná, že má vrátit stav z XAML.
        var geometry = CacheWindowStateStore.ToGeometry(new() { ["size"] = "800;600" });

        Assert.Null(geometry.Maximized);
        Assert.Equal(800, geometry.Width);
    }

    [Fact]
    public void ToGeometry_UnparsableLocationIsDropped()
    {
        var geometry = CacheWindowStateStore.ToGeometry(new() { ["location"] = "blbost", ["state"] = "1" });

        Assert.Null(geometry.Left);
        Assert.Null(geometry.Top);
        Assert.True(geometry.Maximized);
    }

    [Fact]
    public void LoadAndSave_GoThroughCloneOfInjectedSession()
    {
        // Klon: Save jde na pozadí souběžně s UI threadem a RestStatus sdílené instance je per request.
        var shared = new GatedMLRest { SavedState = [$"param{D}size{D}400;300"] };
        var store = new CacheWindowStateStore("EDISON2.TestWindow", () => shared);

        var geometry = store.Load();
        store.Save(new Rect(1, 2, 3, 4), maximized: false);

        Assert.Equal((400d, 300d), (geometry.Width, geometry.Height));
        Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
        Assert.True(shared.Received("LoadParams", onClone: true));
        Assert.True(shared.Received("SaveParams", onClone: true));
        Assert.False(shared.Received("SaveParams", onClone: false));
    }

    [Fact]
    public void Load_ServerHangs_ReturnsNothingSavedWithinLimit()
    {
        // Otevření okna při výpadku VPN nečeká na TCP timeout, okno dostane XAML default.
        var shared = new GatedMLRest("LoadParams") { SavedState = [$"param{D}size{D}400;300"] };
        try
        {
            var store = new CacheWindowStateStore("EDISON2.TestWindow", () => shared) { LoadTimeout = TimeSpan.FromMilliseconds(100) };
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var geometry = store.Load();

            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Load čekal {watch.Elapsed}.");
            Assert.True(geometry.IsEmpty);
        }
        finally
        {
            shared.Gate.Set();
        }
    }

    [Fact]
    public void Save_AfterLoadPastLimit_KeepsSavedGeometry()
    {
        // Pomalé otevření dalo oknu XAML default, zavření ho nesmí uložit přes geometrii uživatele.
        var shared = new GatedMLRest("LoadParams");
        try
        {
            var store = new CacheWindowStateStore("EDISON2.TestWindow", () => shared) { LoadTimeout = TimeSpan.FromMilliseconds(100) };
            store.Load();

            store.Save(new Rect(0, 0, 222, 111), maximized: false);
            shared.Gate.Set();

            Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
            Assert.False(shared.Received("SaveParams", onClone: true));
        }
        finally
        {
            shared.Gate.Set();
        }
    }

    [Fact]
    public void Save_AfterFailedLoad_KeepsSavedGeometry()
    {
        var mlRest = EDTesting.MLRestMocks.Create();
        mlRest.With().Returns(mlRest);
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(_ => throw new InvalidOperationException("server down"));
        var store = new CacheWindowStateStore("EDISON2.TestWindow", () => mlRest);
        Assert.ThrowsAny<Exception>(() => store.Load());   // TryRestore ji spolkne a okno dostane XAML default

        store.Save(new Rect(0, 0, 222, 111), maximized: false);

        Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
        mlRest.DidNotReceive().QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams", Arg.Any<object[]>());
    }

    [Fact]
    public void Load_NoSession_ReturnsNothingSaved()
    {
        // Okno otevřené před loginem: žádná session = XAML default, ne výjimka.
        var asked = 0;
        var store = new CacheWindowStateStore("EDISON2.TestWindow", () =>
        {
            asked++;
            return null;
        });

        Assert.True(store.Load().IsEmpty);
        Assert.Equal(1, asked);
    }
}
