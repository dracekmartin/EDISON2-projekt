namespace EDISONMAIN2.Tests;

public class WinFormsColorPickerTests
{
    [Theory]
    [InlineData("#FFAABBCC")] // alfa se zahazuje
    [InlineData("#AABBCC")]
    [InlineData("AABBCC")]    // bez '#'
    public void TryParseHex_ValidForms_ReturnsRgb(string hex)
    {
        bool ok = WinFormsColorPicker.TryParseHex(hex, out byte r, out byte g, out byte b);

        Assert.True(ok);
        Assert.Equal((0xAA, 0xBB, 0xCC), (r, g, b));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#ABC")]      // krátký (3 znaky nejsou podporované)
    [InlineData("#AABBCCD")]  // 7 znaků
    [InlineData("xxyyzz")]    // ne-hex znaky
    public void TryParseHex_Invalid_ReturnsFalse(string? hex)
    {
        Assert.False(WinFormsColorPicker.TryParseHex(hex, out _, out _, out _));
    }
}
