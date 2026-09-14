using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Volba mezi dvěma řezy tabulky linky: <b>oddělené skupiny dnů, kdykoli nevyjdou na víc bloků</b>.
/// </summary>
/// <remarks>
/// <para>Tak to dělá starý modul (`JRapiLinPrtWin.mac:857-908`) a je to důvod, proč z něj koukají
/// listy jen s pracovními dny, přestože to nikdo nenastavil - oddělení se bere, protože je zadarmo.
/// Do 6. 9. jsme řezali jen podle šířky, takže blok běžně obsahoval konec jedné skupiny a začátek
/// další.</para>
/// <para>Testuje se tady <b>samotné pravidlo</b>, bez geometrie: kolik sloupců se na blok vejde,
/// rozhoduje šířka prvku a písmo (to drží <see cref="LineTableBlokyTests"/>), ale výběr řezu
/// závisí jen na velikostech skupin a na tom čísle.</para>
/// </remarks>
public class RezPodleSkupinDnuTests
{
    /// <summary>Skupiny dnů podle velikostí - `Skupiny(25, 9, 9)` = 25 sloupců skupiny 1, pak 9 a 9.</summary>
    private static List<int> Skupiny(params int[] velikosti)
    {
        var out_ = new List<int>();
        for (int g = 0; g < velikosti.Length; g++)
            for (int i = 0; i < velikosti[g]; i++)
                out_.Add(g + 1);
        return out_;
    }

    /// <summary>
    /// Reálná linka `CB_GWBUS 2 / 0320144`, směr tam: skupiny 25 / 9 / 9 spojů, devět na blok.
    /// Oddělený řez vyjde na 3+1+1 bloků, úsporný na 5 - stejně, takže se skupiny oddělí.
    /// </summary>
    [Fact]
    public void KdyzOddeleniNestojiBlokNavic_SkupinySeOddeli()
    {
        var bloky = TableLayout.ZvolBloky(Skupiny(25, 9, 9), pocetSloupcu: 43, naBlok: 9);

        Assert.Equal(5, bloky.Count);
        // Skupina 1 se dělí uvnitř sebe rovným dílem (9 + 8 + 8), skupiny 2 a 3 mají vlastní blok.
        Assert.Equal([(0, 9), (9, 8), (17, 8), (25, 9), (34, 9)],
            bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>
    /// Táž linka, směr zpět: skupiny 24 / 10 / 9. Oddělený řez vyjde na 3+2+1 = 6 bloků proti
    /// pěti úsporným, takže se **neoddělí** - přesně to rozhodnutí, které dělá starý modul.
    /// </summary>
    [Fact]
    public void KdyzOddeleniStojiBlokNavic_ZustaneUspornyRez()
    {
        var bloky = TableLayout.ZvolBloky(Skupiny(24, 10, 9), pocetSloupcu: 43, naBlok: 9);

        Assert.Equal(5, bloky.Count);
        Assert.Equal([(0, 9), (9, 9), (18, 9), (27, 8), (35, 8)],
            bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>Skupina větší než blok se dál řeže - oddělení není licence na přetečení. Uvnitř
    /// skupiny se dělí rovným dílem, takže 20 spojů po devíti vyjde 7 + 7 + 6, ne 9 + 9 + 2.</summary>
    [Fact]
    public void SkupinaVetsiNezBlok_SeDalRezePoBlocich()
    {
        var bloky = TableLayout.BlokyPodleSkupin(Skupiny(20), naBlok: 9);

        Assert.Equal([(0, 7), (7, 7), (14, 6)], bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>
    /// ⚠️ Obousměrná sestava (skupiny slotů nejsou k dispozici) → jen úsporný řez. Slot tam nese
    /// spoj z každého směru a jejich skupiny se neshodují, takže „hranice skupiny" není jedno místo.
    /// </summary>
    [Fact]
    public void BezSkupinSlotu_JenUspornyRez()
    {
        var bloky = TableLayout.ZvolBloky(null, pocetSloupcu: 43, naBlok: 9);

        Assert.Equal(5, bloky.Count);
        Assert.Equal(9, bloky[0].Pocet);
    }

    /// <summary>Jedna skupina na celou linku - obojí vyjde stejně, nic se nemění.</summary>
    [Fact]
    public void JednaSkupina_VyjdeStejneJakoUspornyRez()
    {
        var podleSkupin = TableLayout.ZvolBloky(Skupiny(43), pocetSloupcu: 43, naBlok: 9);
        var podleSirky = TableLayout.BlokyPodleSirky(43, 9);

        Assert.Equal(podleSirky.Count, podleSkupin.Count);
        Assert.Equal(podleSirky.Select(b => (b.Od, b.Pocet)), podleSkupin.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>
    /// ⚠️ Hodně malých skupin oddělení **nedostane**: tři skupiny po pěti vyjdou oddělené na tři
    /// bloky, ale úsporně na dva. Pravidlo je „oddělit, když je to zadarmo", ne „oddělit vždy" -
    /// jinak by tabulka s pěti skupinami dnů zabrala pět listů místo dvou.
    /// </summary>
    [Fact]
    public void MaleSkupiny_KdyzBySeOddelenimPribylBlok_ZustanouSpolu()
    {
        var bloky = TableLayout.ZvolBloky(Skupiny(5, 5, 5), pocetSloupcu: 15, naBlok: 9);

        Assert.Equal([(0, 8), (8, 7)], bloky.Select(b => (b.Od, b.Pocet)));
    }

    /// <summary>Sloty bez spojů nedají žádný blok - volající pak nemusí řešit zvláštní případ.</summary>
    [Fact]
    public void ZadneSloupce_ZadnyBlok()
    {
        Assert.Empty(TableLayout.ZvolBloky(Skupiny(), pocetSloupcu: 0, naBlok: 9));
        Assert.Empty(TableLayout.BlokyPodleSirky(0, 9));
    }
}
