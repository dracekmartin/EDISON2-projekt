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
    public void LoadAndSave_GoThroughInjectedSession()
    {
        var mlRest = EDTesting.MLRestMocks.Create();
        mlRest.Delimiter.Returns(D);
        mlRest.QueryOrThrow("ALVA.LIBRARY.Form", "LoadParams", Arg.Any<object[]>())
            .Returns(EDTesting.MLRestMocks.Output($"param{D}size{D}400;300"));
        var store = new CacheWindowStateStore("EDISON2.TestWindow", () => mlRest);

        var geometry = store.Load();
        store.Save(new Rect(1, 2, 3, 4), maximized: false);

        Assert.Equal((400d, 300d), (geometry.Width, geometry.Height));
        mlRest.Received(1).QueryOrThrow("ALVA.LIBRARY.Form", "SaveParams",
            Arg.Is<object[]>(a => (string)a[0] == $"formID{D}EDISON2.TestWindow"));
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
