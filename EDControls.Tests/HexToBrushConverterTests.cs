using System.Globalization;
using System.Windows;
using System.Windows.Media;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>Testy HexToBrushConverter (modul tree / row foreground): hex → brush, jinak UnsetValue.</summary>
public class HexToBrushConverterTests
{
    private readonly HexToBrushConverter _conv = new();

    private object Convert(object? value) =>
        _conv.Convert(value, typeof(Brush), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_ValidHex_ReturnsBrush()
    {
        var brush = Assert.IsType<SolidColorBrush>(Convert("#FF0000"));
        Assert.Equal(Colors.Red, brush.Color);
    }

    /// <summary>Sdílený formát barvy (ColorValue) i zbytek syntaxe WPF, kterou převodník bral i dřív.</summary>
    [Theory]
    [InlineData("255;0;0")]
    [InlineData("Red")]
    [InlineData("#F00")]
    public void Convert_RgbTripletAndWpfSyntax_ReturnsBrush(string value)
    {
        var brush = Assert.IsType<SolidColorBrush>(Convert(value));
        Assert.Equal(Colors.Red, brush.Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData(null)]
    [InlineData(42)]   // ne-string
    public void Convert_EmptyOrInvalid_ReturnsUnsetValue(object? value)
    {
        Assert.Equal(DependencyProperty.UnsetValue, Convert(value));
    }
}
