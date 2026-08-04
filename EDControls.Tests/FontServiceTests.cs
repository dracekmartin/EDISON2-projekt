using EDControls.UserFonts;

namespace EDControls.Tests;

public class FontServiceTests
{
    // Testovací host nevytváří WPF Application → pack:// loading (ThemeDefaults ve
    // FontService) vyžaduje ručně triggernout Application type initializer.
    static FontServiceTests() => _ = System.Windows.Application.ResourceAssembly;

    [Fact]
    public void ComputeFromPrefs_Applied_WithFamily_ReturnsSettings()
    {
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Arial", FontSize = 12 };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Equal("Arial", settings.FontFamily);
        // Prefs jsou v pt, tokeny v px — 12 pt = 16 px.
        Assert.Equal(16, settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_ConvertsPtToPx()
    {
        // Past, kterou to řeší: 10 pt aplikovaných jako 10 px = 7,5 pt (drobné písmo).
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Arial", FontSize = 10 };

        Assert.Equal(13.333, FontService.ComputeFromPrefs(prefs).FontSize!.Value, 3);
    }

    [Theory]
    [InlineData(0.5, FontService.MinBodyPt)]
    [InlineData(200, FontService.MaxBodyPt)]
    public void ComputeFromPrefs_ClampsOutOfRangeSize(double sizePt, double expectedPt)
    {
        // Ručně editovaný font.json nesmí udělat UI, ze kterého se uživatel nedostane zpět.
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Arial", FontSize = sizePt };

        Assert.Equal(expectedPt * FontService.PtToPx, FontService.ComputeFromPrefs(prefs).FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_NotApplied_ReturnsNullSettings()
    {
        // FontApplied=false → clear, i kdyby family bylo set (user vypnul "Použít font" v menu).
        var prefs = new UserFontPreference { FontApplied = false, FontFamily = "Arial", FontSize = 14 };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Null(settings.FontFamily);
        Assert.Null(settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_Applied_NoFamily_ReturnsNullSettings()
    {
        // FontApplied=true ale FontFamily prázdné/null → clear (neaplikovat nedefinovaný font).
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = null };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Null(settings.FontFamily);
        Assert.Null(settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_Applied_NoSize_ReturnsFamilyOnly()
    {
        // Family bez size → settings má family, size null (Apply nechá Body token na defaultu z EDTheme.xaml).
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = null };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Equal("Calibri", settings.FontFamily);
        Assert.Null(settings.FontSize);
    }

    [Fact]
    public void ComputeBodySize_Applied_ReturnsUserSizeInPx()
    {
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Arial", FontSize = 18 };

        Assert.Equal(24, FontService.ComputeBodySize(prefs));
    }

    [Fact]
    public void ComputeBodySize_NotApplied_ReturnsThemeDefault()
    {
        // Bez overridu vrací default Body z EDTheme.xaml (14 px = legacy standard s rezervou pro WPF).
        var prefs = new UserFontPreference { FontApplied = false, FontFamily = "Arial", FontSize = 18 };

        Assert.Equal(14, FontService.ComputeBodySize(prefs));
    }

    [Fact]
    public void ComputeBodySize_Applied_NoSize_ReturnsThemeDefault()
    {
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Calibri", FontSize = null };

        Assert.Equal(14, FontService.ComputeBodySize(prefs));
    }

    [Fact]
    public void OverrideBodyPt_NotApplied_ReturnsNull()
    {
        // Dialog pak předvolí výchozí velikost, ne osiřelou hodnotu z font.json.
        var prefs = new UserFontPreference { FontApplied = false, FontFamily = "Arial", FontSize = 9 };

        Assert.Null(FontService.OverrideBodyPt(prefs));
    }

    [Fact]
    public void OverrideBodyPt_ReturnsClampedValue()
    {
        // Ručně editovaná velikost: dialog musí ukázat, co se aplikuje, ne surové číslo.
        var prefs = new UserFontPreference { FontApplied = true, FontFamily = "Arial", FontSize = 200 };

        Assert.Equal(FontService.MaxBodyPt, FontService.OverrideBodyPt(prefs));
    }

    [Fact]
    public void DefaultBodyPt_MatchesThemeTokenInPoints()
    {
        // Dialog z toho staví popisek „(výchozí)" — nesmí se rozejít s tokenem.
        Assert.Equal(10.5, FontService.DefaultBodyPt);
    }
}
