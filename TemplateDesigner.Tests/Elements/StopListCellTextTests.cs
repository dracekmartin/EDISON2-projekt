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

    // ── Zóny (KOVED #13091 bod 4) ─────────────────────────────────────────────

    /// <summary>Hraniční zastávka leží ve dvou zónách. Starý modul je odděloval čárkou, takže
    /// stejně musí vypadat i vývěska z návrháře - jinak se zákazníkovi změní tvar údaje.</summary>
    [Fact]
    public void ViceZonJeOddelenoCarkou()
    {
        var z = new ZastavkaData { Popis = "Zastávka", Zony = ["11", "12"] };

        Assert.Equal("11,12", new StopListElement().GetCellText(z, StopFields.Zone));
    }

    [Fact]
    public void JednaZonaSeVypiseSama()
    {
        var z = new ZastavkaData { Popis = "Zastávka", Zony = ["101"] };

        Assert.Equal("101", new StopListElement().GetCellText(z, StopFields.Zone));
    }

    // ── Čas jízdy u projeté zastávky (KOVED #13091) ───────────────────────────
    // Časy jsou kumulativní od vývěskové zastávky, takže u projetých vycházejí záporné.

    private static ZastavkaData StopSCasy(int casJizdy, params string[] varianty) => new()
    {
        Popis = "Zastávka",
        CasJizdy = casJizdy,
        TrasaMin = [.. varianty],
    };

    private static string VariantniCas(PassedStopsTimeDisplay mode, ZastavkaData z, bool projeta) =>
        new StopListElement { PassedStopsTime = mode }
            .GetCellText(z, StopFields.TravelTimeVariantPrefix + "0", projeta);

    [Fact]
    public void VychozeSeCasProjeteZastavkyNetiskne()
    {
        // Data dávají u projetých záporný čas (kumulativní od vývěskové zastávky).
        Assert.Equal("", VariantniCas(PassedStopsTimeDisplay.Hide, StopSCasy(0, "-7"), projeta: true));
        Assert.Equal("", VariantniCas(PassedStopsTimeDisplay.Hide, StopSCasy(0, "4"), projeta: true));
    }

    [Fact]
    public void NeprojetaZastavkaCasySiDrzi()
    {
        var z = StopSCasy(12, "12");
        Assert.Equal("12", VariantniCas(PassedStopsTimeDisplay.Hide, z, projeta: false));
        Assert.Equal("12", VariantniCas(PassedStopsTimeDisplay.Show, z, projeta: false));
    }

    [Fact]
    public void ZobrazitZachovaDosavadniChovani()
    {
        var z = StopSCasy(0, "-7");
        Assert.Equal("-7", VariantniCas(PassedStopsTimeDisplay.Show, z, projeta: true));
    }

    [Fact]
    public void ZnackaProjizdiZustava_NeniToCas()
    {
        var z = StopSCasy(0, "<");
        Assert.Equal("<", VariantniCas(PassedStopsTimeDisplay.Hide, z, projeta: true));
    }

    [Fact]
    public void ZakladniSloupecCasu_SeChovaStejneJakoVariantni()
    {
        var el = new StopListElement { PassedStopsTime = PassedStopsTimeDisplay.Hide };
        Assert.Equal("", el.GetCellText(StopSCasy(9), StopFields.TravelTime, isPassed: true));
        Assert.Equal("9", el.GetCellText(StopSCasy(9), StopFields.TravelTime, isPassed: false));
    }
}
