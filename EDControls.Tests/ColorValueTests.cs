using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>Jediná definice barvy na drátě - hex (formulář, nové seznamy) i legacy R;G;B (starší LoadList).</summary>
public class ColorValueTests
{
    [Theory]
    [InlineData("#80FF0000", 0x80, 0xFF, 0x00, 0x00)]
    [InlineData("#FF8000", 0xFF, 0xFF, 0x80, 0x00)]
    [InlineData("ff8000", 0xFF, 0xFF, 0x80, 0x00)]
    [InlineData(" #FF8000 ", 0xFF, 0xFF, 0x80, 0x00)]
    [InlineData("255;128;0", 0xFF, 255, 128, 0)]
    [InlineData("255; 128 ;0", 0xFF, 255, 128, 0)]
    public void TryParse_HexAndLegacyRgb(string value, byte a, byte r, byte g, byte b)
    {
        Assert.True(ColorValue.TryParse(value, out var color));
        Assert.Equal(Color.FromArgb(a, r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#GG0000")]
    [InlineData("#FFF")]
    [InlineData("#1FF8000")]
    [InlineData("FF80\0\0")]
    [InlineData("FF8000\0\0")]
    [InlineData("255;128")]
    [InlineData("256;0;0")]
    [InlineData("255;128;0;0")]
    [InlineData("Red")]
    [InlineData("{BARVA_LINKY}")]
    public void TryParse_Invalid_ReturnsFalse(string? value)
    {
        Assert.False(ColorValue.TryParse(value, out _));
        Assert.False(ColorValue.TryParseHex(value, out _));
    }

    /// <summary>Formulář bere jen hex - hostitel (TD) by legacy R;G;B neuměl.</summary>
    [Fact]
    public void TryParseHex_RejectsLegacyRgb_AcceptsHex()
    {
        Assert.False(ColorValue.TryParseHex("255;128;0", out _));
        Assert.True(ColorValue.TryParseHex("#80FF8000", out var color));
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x80, 0x00), color);
    }
}
