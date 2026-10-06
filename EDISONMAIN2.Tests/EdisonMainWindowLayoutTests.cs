using System.Windows;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Rozměry hlavního okna: pevná šířka přepínače firem (psaný filtr ani šipky neposouvají menu,
/// strop drží menu na jednom řádku) a minimum okna, které se musí vejít na pracovní plochu.
/// </summary>
public class EdisonMainWindowLayoutTests
{
    private static double Measure(string text) => text.Length * 7;

    [Fact]
    public void CompanyComboWidth_FollowsLongestCompanyPlusChrome()
    {
        double width = EdisonMainWindow.CompanyComboWidth(["1 - DP", "22 - Dopravní podnik"], Measure, chrome: 40);

        Assert.Equal("22 - Dopravní podnik".Length * 7 + 40, width);
    }

    [Fact]
    public void CompanyComboWidth_LongCompany_StopsAtCap()
    {
        double width = EdisonMainWindow.CompanyComboWidth([new string('x', 200)], Measure, chrome: 40);

        Assert.Equal(EdisonMainWindow.CompanyComboMaxWidth, width);
    }

    [Fact]
    public void CompanyComboWidth_NoCompanies_IsJustChrome()
    {
        // Spodní mez drží MinWidth v XAML, výpočet ji neduplikuje.
        Assert.Equal(40, EdisonMainWindow.CompanyComboWidth([], Measure, chrome: 40));
    }

    [Fact]
    public void MinSize_FitsOnScreen_StaysAsDesigned()
    {
        var min = EdisonMainWindow.MinSizeWithinWorkArea(new Size(1280, 720), new Size(2560, 1400));

        Assert.Equal(new Size(1280, 720), min);
    }

    [Fact]
    public void MinSize_FhdAt150Percent_ShrinksToWorkArea()
    {
        // 1920 × 1080 px při 150 % = 1280 × 720 DIP, hlavní panel Win11 ubere 32 DIP výšky.
        var min = EdisonMainWindow.MinSizeWithinWorkArea(new Size(1280, 720), new Size(1280, 688));

        Assert.Equal(new Size(1280, 688), min);
    }
}
