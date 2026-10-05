using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Tisk linkového JŘ „Stránka pro každou zastávku": zastávka výlepu je v tabulce vždy inverzní,
/// bez volby prvku. Bez toho parametru se vzhled zvýrazněné zastávky nemění.
/// </summary>
public class VylepZastavkyTests
{
    private static DataContext Hodnoty(params (string Klic, string Hodnota)[] pole) =>
        new(pole.ToDictionary(p => p.Klic, p => (object?)p.Hodnota));

    [Fact]
    public void ZastavkaVylepu_SePozna()
    {
        var stranka = Hodnoty(("VYLEP_ZASTAVKY", "true"));
        Assert.True(LineTableElement.JeZastavkaVylepu(stranka, Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
        Assert.False(LineTableElement.JeZastavkaVylepu(stranka, Hodnoty(("AKTUALNI_ZASTAVKA", "false"))));
    }

    [Fact]
    public void ZastavkaVylepu_JeInverzni_IPresBarvuAPodbarveni()
    {
        // jako starý modul (podbarvení 4 = inverzní řádek): bílé na černém, přebije podbarvení lichých TČ
        var podbarvena = new ResolvedTextStyle() with { TextColor = "#FF112233", BackgroundColor = "#FFEEEEEE" };
        var inverzni = LineTableElement.Inverze(podbarvena, true);
        Assert.Equal("#FFFFFFFF", inverzni.TextColor);
        Assert.Equal("#FF000000", inverzni.BackgroundColor);
        Assert.Equal(podbarvena, LineTableElement.Inverze(podbarvena, false));
    }

    [Fact]
    public void ZvyrazneniBezVylepu_InverzeNeni()
    {
        // „Zvýraznit zastávku" bez výlepů: vzhled dál určuje jen pravidlo zvýraznění šablony
        Assert.False(LineTableElement.JeZastavkaVylepu(Hodnoty(), Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
        Assert.False(LineTableElement.JeZastavkaVylepu(null, Hodnoty(("AKTUALNI_ZASTAVKA", "true"))));
    }
}
