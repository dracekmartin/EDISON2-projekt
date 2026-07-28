using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Režimy sloupce „Označník" (<see cref="StopListElement.GetCellText"/>, KOVED #13048 bod 4):
/// Z trasy linky / Nejčetnější dle spojů / Výčet všech - včetně fallbacku na hodnotu z trasy,
/// když per-spoj data chybí.
/// </summary>
public class StopListCellTextTests
{
    private static ZastavkaData Stop(string zTrasy, string dleSpoju) => new()
    {
        Popis = "Zastávka",
        Nastupiste = zTrasy,
        NastupisteSpoju = dleSpoju,
    };

    private static string Text(MultiPlatformDisplay mode, ZastavkaData z) =>
        new StopListElement { MultiPlatformMode = mode }.GetCellText(z, StopFields.Nastupiste);

    [Fact]
    public void ZTrasyLinky_IgnorujeDataSpoju()
    {
        Assert.Equal("11", Text(MultiPlatformDisplay.FromRoute, Stop("11", "22,11")));
    }

    [Fact]
    public void Nejcetnejsi_BereProvniPolozkuCsv()
    {
        Assert.Equal("22", Text(MultiPlatformDisplay.MostFrequent, Stop("11", "22,11")));
    }

    [Fact]
    public void VycetVsech_ZobraziCsvSMezerami()
    {
        Assert.Equal("3, 2, 4", Text(MultiPlatformDisplay.All, Stop("1", "3,2,4")));
    }

    [Fact]
    public void BezDatSpoju_PadaNaHodnotuZTrasy()
    {
        Assert.Equal("5", Text(MultiPlatformDisplay.MostFrequent, Stop("5", "")));
        Assert.Equal("5", Text(MultiPlatformDisplay.All, Stop("5", "")));
    }
}
