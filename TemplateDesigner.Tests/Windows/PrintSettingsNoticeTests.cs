namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Signál ve stavovém řádku o nastavení, které tiše mění obsah vývěsky. Vznikl z konkrétního případu:
/// uložené „Rozdělit vývěsku pro každý označník" bylo zapnuté, UI ho ukazovalo jako vypnuté a ze
/// stránky tím zmizely odjezdy jiného označníku. Uživatel to nemá jak poznat, pokud mu to nikdo neřekne.
/// </summary>
public class PrintSettingsNoticeTests
{
    [Fact]
    public void NicAktivni_PrazdnyText()
    {
        Assert.Equal("", TemplateDesignerViewModel.BuildPrintSettingsNotice(false, false, false));
    }

    [Fact]
    public void SkrytePoznamky_Hlasi()
    {
        var t = TemplateDesignerViewModel.BuildPrintSettingsNotice(true, false, false);
        Assert.Equal("Sestava: poznámky skryté nastavením", t);
    }

    [Fact]
    public void RozdeleniPoStanovistich_Hlasi()
    {
        var t = TemplateDesignerViewModel.BuildPrintSettingsNotice(false, false, true);
        // „Stanoviště" je zastřešující pojem - dělit se dá podle označníku i sloupku (ZDROJ_STANOVISTE).
        Assert.Contains("vývěska rozdělená po stanovištích", t);
    }

    [Fact]
    public void PismenaVariant_Hlasi()
    {
        var t = TemplateDesignerViewModel.BuildPrintSettingsNotice(false, true, false);
        Assert.Contains("rozdíly tras nahrazené písmeny variant", t);
    }

    [Fact]
    public void VseNaraz_SpojiOddelovacem()
    {
        var t = TemplateDesignerViewModel.BuildPrintSettingsNotice(true, true, true);
        Assert.Equal(
            "Sestava: poznámky skryté nastavením · rozdíly tras nahrazené písmeny variant · vývěska rozdělená po stanovištích",
            t);
    }
}
