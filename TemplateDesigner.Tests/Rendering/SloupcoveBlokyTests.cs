using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rozdělení sloupců spojů do stránkových bloků (`L5-1`) - to, čemu starý modul říká „střih".
///
/// Řez je <b>čistě geometrický</b>: podle šířek a písma, ne podle skupin dnů. Změřeno na starém
/// modulu (analýza §16), kde na lince s 30 pracovními, 5 sobotními a 6 nedělními spoji padl řez
/// na 21+20 a 39+2 - tedy nikdy na hranici skupiny.
/// </summary>
public class SloupcoveBlokyTests
{
    /// <summary>Vejde-li se všechno, je blok jeden - volající pak nemusí řešit zvláštní případ.</summary>
    [Fact]
    public void VejdeSeVse_JedenBlok()
    {
        var bloky = TableLayout.RozdelSpojeDoBloku(pocetSpoju: 10, popisneSloupceMm: 50, sloupecSpojeMm: 10, sirkaMm: 200);

        Assert.Single(bloky);
        Assert.Equal(0, bloky[0].Od);
        Assert.Equal(10, bloky[0].Pocet);
    }

    /// <summary>Popisné sloupce se opakují v každém bloku, takže na spoje zbývá jen zbytek šířky.
    /// Šířka 100, popisné 40 → 60 mm na spoje po 10 mm = 6 spojů na blok.</summary>
    [Fact]
    public void PopisneSloupceUbirajiMistoVKazdemBloku()
    {
        var bloky = TableLayout.RozdelSpojeDoBloku(pocetSpoju: 20, popisneSloupceMm: 40, sloupecSpojeMm: 10, sirkaMm: 100);

        Assert.Equal(4, bloky.Count);                       // 6 + 6 + 6 + 2
        Assert.Equal(new[] { 6, 6, 6, 2 }, bloky.Select(b => b.Pocet));
        Assert.Equal(new[] { 0, 6, 12, 18 }, bloky.Select(b => b.Od));
    }

    /// <summary>Žádný spoj se neztratí ani nezopakuje - součet přes bloky je původní počet
    /// a bloky na sebe navazují.</summary>
    [Theory]
    [InlineData(41, 60f, 8f, 140f)]
    [InlineData(109, 55f, 6f, 190f)]
    [InlineData(1, 50f, 10f, 60f)]
    public void ZadnySpojSeNeztratiAniNezopakuje(int spoju, float popisne, float sloupec, float sirka)
    {
        var bloky = TableLayout.RozdelSpojeDoBloku(spoju, popisne, sloupec, sirka);

        Assert.Equal(spoju, bloky.Sum(b => b.Pocet));
        int ocekavanyZacatek = 0;
        foreach (var b in bloky)
        {
            Assert.Equal(ocekavanyZacatek, b.Od);
            ocekavanyZacatek += b.Pocet;
        }
    }

    /// <summary>
    /// ⚠️ Past, kvůli které je v kódu pojistka: když se nevejde ani jeden sloupec spoje (úzký prvek,
    /// široké popisné sloupce), musí blok stejně dostat jeden - jinak by se dělení nikdy nedobralo
    /// konce. Přetečený blok je lepší než zacyklení nebo tiše zahozený spoj.
    /// </summary>
    [Fact]
    public void NevejdeSeAniJeden_PrestoSeDelenidobehne()
    {
        var bloky = TableLayout.RozdelSpojeDoBloku(pocetSpoju: 3, popisneSloupceMm: 100, sloupecSpojeMm: 10, sirkaMm: 100);

        Assert.Equal(3, bloky.Count);
        Assert.All(bloky, b => Assert.Equal(1, b.Pocet));
    }

    /// <summary>Bez spojů není co dělit.</summary>
    [Fact]
    public void ZadneSpoje_ZadnyBlok() =>
        Assert.Empty(TableLayout.RozdelSpojeDoBloku(0, 50, 10, 200));
}
