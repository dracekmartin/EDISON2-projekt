using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Když se sloupce spojů nevejdou do šířky, rozdělí se mezi části <b>rovným dílem</b>.
/// </summary>
/// <remarks>
/// <para>Do 14. 9. se části plnily do plna a poslední zůstala kratká - u linky s 21 spoji po deseti
/// nesla poslední část jediný sloupec a zbytek stránky byl prázdný. Tak to dělá windowsový tisk
/// (`JRapiLinPrtWin.mac:868-880`); postskriptový místo toho spočítá počet částí a spoje mezi ně
/// rozdělí (`JRapiLinPrtPS.mac:492`). Rovnoměrné dělení je tedy volba mezi dvěma starými tisky,
/// ne dorovnání na jeden z nich (rozhodnutí Jan 14. 9.).</para>
/// <para>Podmínka, za které to Jan schválil: <b>nesmí přibýt část</b> oproti plnění do plna.</para>
/// </remarks>
public class RovnomerneDeleniSpojuTests
{
    /// <summary>Skupiny dnů podle velikostí - `Skupiny(8, 7)` = 8 sloupců skupiny 1, pak 7 skupiny 2.</summary>
    private static List<int> Skupiny(params int[] velikosti)
    {
        var out_ = new List<int>();
        for (int g = 0; g < velikosti.Length; g++)
            for (int i = 0; i < velikosti[g]; i++)
                out_.Add(g + 1);
        return out_;
    }

    /// <summary>Plnění do plna, proti kterému se poměřuje počet částí - to, co dělal řez do 14. 9.</summary>
    private static int CastiDoPlna(int pocet, int naBlok)
    {
        int casti = 0;
        for (int od = 0; od < pocet; od += naBlok) casti++;
        return casti;
    }

    /// <summary>Jádro věci: 21 spojů po deseti nevyjde 10 + 10 + 1, ale 7 + 7 + 7.</summary>
    [Fact]
    public void CastiVyjdouStejneVelke()
    {
        var bloky = TableLayout.BlokyPodleSirky(pocetSloupcu: 21, naBlok: 10);

        Assert.Equal([(0, 7), (7, 7), (14, 7)], bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>Co nevyjde beze zbytku, dostanou po jednom první části - 25 po devíti je 9 + 8 + 8.</summary>
    [Fact]
    public void ZbytekSeRozdeliPoJednom()
    {
        var bloky = TableLayout.BlokyPodleSirky(pocetSloupcu: 25, naBlok: 9);

        Assert.Equal([(0, 9), (9, 8), (17, 8)], bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>
    /// Podmínka, za které je to schválené: <b>část navíc nepřibude</b>. Ani žádný spoj nesmí
    /// vypadnout a žádná část nesmí být širší, než se vejde.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(17)]
    public void PocetCastiSeNezvysi(int naBlok)
    {
        for (int pocet = 1; pocet <= 200; pocet++)
        {
            var bloky = TableLayout.BlokyPodleSirky(pocet, naBlok);

            Assert.Equal(CastiDoPlna(pocet, naBlok), bloky.Count);
            Assert.Equal(pocet, bloky.Sum(b => b.Pocet));
            Assert.All(bloky, b => Assert.InRange(b.Pocet, 1, naBlok));
            // Části na sebe musí navazovat, jinak by se spoje tiskly dvakrát nebo vůbec.
            int ocekavanyOd = 0;
            foreach (var b in bloky) { Assert.Equal(ocekavanyOd, b.Od); ocekavanyOd += b.Pocet; }
        }
    }

    /// <summary>Části se od sebe smějí lišit nejvýš o jeden sloupec - jinak to není rovným dílem.</summary>
    [Fact]
    public void CastiSeLisiNejvyseOJeden()
    {
        for (int naBlok = 1; naBlok <= 20; naBlok++)
            for (int pocet = 1; pocet <= 200; pocet++)
            {
                var bloky = TableLayout.BlokyPodleSirky(pocet, naBlok);
                int nej = bloky.Max(b => b.Pocet), nejmene = bloky.Min(b => b.Pocet);
                Assert.True(nej - nejmene <= 1,
                    $"{pocet} spojů po {naBlok}: části {nejmene}..{nej}");
            }
    }

    /// <summary>
    /// ⚠️ Hranice skupiny dnů se nepřekročí ani kvůli vyrovnání. Skupiny 8 a 7 po deseti zůstanou
    /// tak, jak jsou - přesunout jeden spoj by zrušilo přesně to, kvůli čemu se skupiny oddělují.
    /// </summary>
    [Fact]
    public void PresHraniciSkupinySeSpojNepresune()
    {
        var bloky = TableLayout.ZvolBloky(Skupiny(8, 7), pocetSloupcu: 15, naBlok: 10);

        Assert.Equal([(0, 8), (8, 7)], bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>Uvnitř skupiny, která se musí řezat, se naopak vyrovnává - 21 spojů dá 7 + 7 + 7
    /// a další skupina si drží vlastní část.</summary>
    [Fact]
    public void UvnitrSkupinySeVyrovnava()
    {
        var bloky = TableLayout.BlokyPodleSkupin(Skupiny(21, 4), naBlok: 10);

        Assert.Equal([(0, 7), (7, 7), (14, 7), (21, 4)], bloky.Select(b => (b.Od, b.Pocet)));
    }
}
