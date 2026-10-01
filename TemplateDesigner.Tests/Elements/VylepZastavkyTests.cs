using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Tisk linkového JŘ „Stránka pro každou zastávku": zastávka výlepu je v tabulce vždy tučně,
/// bez volby prvku. Bez toho parametru se vzhled zvýrazněné zastávky nemění.
/// </summary>
public class VylepZastavkyTests
{
    private static DataContext Hodnoty(params (string Klic, string Hodnota)[] pole) =>
        new(pole.ToDictionary(p => p.Klic, p => (object?)p.Hodnota));

    [Fact]
    public void ZastavkaVylepu_JeTucne()
    {
        var stranka = Hodnoty(("VYLEP_ZASTAVKY", "true"));
        Assert.True(LineTableElement.JeZastavkaVylepu(stranka, Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
        Assert.False(LineTableElement.JeZastavkaVylepu(stranka, Hodnoty(("AKTUALNI_ZASTAVKA", "false"))));
    }

    [Fact]
    public void ZvyrazneniBezVylepu_TucneNeni()
    {
        // „Zvýraznit zastávku" bez výlepů: vzhled dál určuje jen pravidlo zvýraznění šablony
        Assert.False(LineTableElement.JeZastavkaVylepu(Hodnoty(), Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
        Assert.False(LineTableElement.JeZastavkaVylepu(null, Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
    }
}
