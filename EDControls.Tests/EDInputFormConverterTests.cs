using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EDControls.EDInputForm;
using EDTesting;

namespace EDControls.Tests;

public class EDInputFormConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static object ConvertColor(object? value) =>
        new ColorPreviewHexToBrushConverter().Convert(value!, typeof(Brush), null!, Culture);

    [Fact]
    public void ColorPreview_ArgbHex_ParsesAlpha()
    {
        var brush = Assert.IsType<SolidColorBrush>(ConvertColor("#80FF0000"));

        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x00, 0x00), brush.Color);
    }

    [Theory]
    [InlineData("#AABBCC")]
    [InlineData("AABBCC")]
    public void ColorPreview_RgbHex_FullAlpha(string hex)
    {
        var brush = Assert.IsType<SolidColorBrush>(ConvertColor(hex));

        Assert.Equal(Color.FromArgb(0xFF, 0xAA, 0xBB, 0xCC), brush.Color);
    }

    [Fact]
    public void ColorPreview_DataPlaceholder_HatchedBrushNotTransparent()
    {
        // "{BARVA_LINKY}" = barva přijde z dat - šrafování, ne bílá/průhledná (nález #12997)
        var brush = ConvertColor("{BARVA_LINKY}");

        Assert.IsType<DrawingBrush>(brush);
        Assert.NotSame(Brushes.Transparent, brush);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#ABC")]
    [InlineData("xxyyzz")]
    [InlineData("255;128;0")]   // legacy EDList formát - validace pole ho odmítne, náhled taky
    [InlineData(42)]
    public void ColorPreview_Invalid_Transparent(object? value)
    {
        Assert.Same(Brushes.Transparent, ConvertColor(value));
    }

    [Fact]
    public void ColorPreview_ConvertBack_Throws()
    {
        Assert.Throws<NotImplementedException>(() =>
            new ColorPreviewHexToBrushConverter().ConvertBack("x", typeof(string), null!, Culture));
    }

    // 1×1 průhledný PNG.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void Base64Image_ValidPng_FrozenBitmap()
    {
        Sta.Run(() =>
        {
            var result = new Base64ToImageSourceConverter().Convert(OnePixelPng, typeof(object), null!, Culture);

            var bmp = Assert.IsType<BitmapImage>(result);
            Assert.True(bmp.IsFrozen);   // frozen → použitelný napříč vlákny, stream se neuzamkl
            Assert.Equal(1, bmp.PixelWidth);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tohle není base64!")]
    [InlineData(42)]
    public void Base64Image_Invalid_ReturnsNullInsteadOfThrowing(object? value)
    {
        Assert.Null(new Base64ToImageSourceConverter().Convert(value!, typeof(object), null!, Culture));
    }

    [Fact]
    public void Base64Image_ConvertBack_Throws()
    {
        Assert.Throws<NotImplementedException>(() =>
            new Base64ToImageSourceConverter().ConvertBack("x", typeof(string), null!, Culture));
    }
}
