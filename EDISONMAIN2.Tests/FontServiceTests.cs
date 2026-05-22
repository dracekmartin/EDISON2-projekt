namespace EDISONMAIN2.Tests;

public class FontServiceTests
{
    [Fact]
    public void ComputeFromPrefs_Applied_WithFamily_ReturnsSettings()
    {
        var prefs = new LocalPrefs { FontApplied = true, FontFamily = "Arial", FontSize = 14 };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Equal("Arial", settings.FontFamily);
        Assert.Equal(14, settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_NotApplied_ReturnsNullSettings()
    {
        // FontApplied=false → clear, i kdyby family bylo set (user vypnul "Použít font" v menu).
        var prefs = new LocalPrefs { FontApplied = false, FontFamily = "Arial", FontSize = 14 };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Null(settings.FontFamily);
        Assert.Null(settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_Applied_NoFamily_ReturnsNullSettings()
    {
        // FontApplied=true ale FontFamily prázdné/null → clear (neaplikovat nedefinovaný font).
        var prefs = new LocalPrefs { FontApplied = true, FontFamily = null };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Null(settings.FontFamily);
        Assert.Null(settings.FontSize);
    }

    [Fact]
    public void ComputeFromPrefs_Applied_NoSize_ReturnsFamilyOnly()
    {
        // Family bez size → settings má family, size null (Apply nechá window.FontSize na defaultu).
        var prefs = new LocalPrefs { FontApplied = true, FontFamily = "Calibri", FontSize = null };

        var settings = FontService.ComputeFromPrefs(prefs);

        Assert.Equal("Calibri", settings.FontFamily);
        Assert.Null(settings.FontSize);
    }
}
