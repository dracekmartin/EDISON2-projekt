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
}
