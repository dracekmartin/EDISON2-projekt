using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rozřezání linkové tabulky do bloků na úrovni prvku - tedy s <b>opravdovými</b> šířkami sloupců,
/// ne s hodnotami dosazenými do vzorce.
/// </summary>
/// <remarks>
/// <para><see cref="SloupcoveBlokyTests"/> ověřují dělicí vzorec samotný a byly celou dobu zelené
/// i ve chvíli, kdy tabulka v návrháři vycházela nečitelná. Vzorec totiž dostával špatné vstupy:
/// popisné sloupce se měřily při plném počtu spojů, tedy zmáčknuté na minimum, takže se zdálo,
/// že na spoje zbývá spousta místa. První blok pak pobral skoro všechny spoje a přetekl.</para>
/// <para>Proto se tady netestuje vzorec, ale <b>výsledek</b>: co vyjde z prvku nad daty.</para>
/// </remarks>
public class LineTableBlokyTests
{
    /// <summary>Linka s tolika spoji, že se do šířky nevejdou - právě na téhle se to lámalo.</summary>
    private static DataContext SirokaLinka(int pocetSpoju)
    {
        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i => RenderSnapshot.Row(("CISLO_SPOJE", (i * 2).ToString()),
                                            ("SKUPINA", i <= pocetSpoju * 2 / 3 ? "1" : "2")))
            .ToArray();
        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "České Budějovice, aut.nádr."), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Velešín, náměstí"), ("PORADI", "2")),
                RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "Kaplice, aut.st."), ("PORADI", "3")),
            ]),
            ("SPOJE", spoje),
            ("SKUPINY_DNU",
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota, neděle"), ("PORADI", "2")),
            ]));
    }

    private static LineTableElement Prvek() => ElementRenderFixtures.LineTable();

    /// <summary>
    /// ⚠️ Podstata celé opravy: blok se musí do šířky prvku <b>vejít</b>. Dokud se počet spojů
    /// odvozoval z popisných sloupců změřených pro jiný počet, první blok šířku překročil
    /// a časy v něm byly přes sebe.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(41)]
    [InlineData(120)]
    public void KazdyBlokSeVejdeDoSirky(int spoju)
    {
        var prvek = Prvek();

        var bloky = prvek.Rozvrzeni(SirokaLinka(spoju));

        Assert.NotEmpty(bloky);
        Assert.All(bloky, b => Assert.True(b.SirkaMm <= prvek.Width + 0.01f,
            $"blok od spoje {b.Od} ({b.Pocet} spojů) je {b.SirkaMm:0.0} mm, prvek má {prvek.Width} mm"));
    }

    /// <summary>Bloky vycházejí zhruba stejně velké. Chyba se poznala právě takhle: 36 spojů
    /// v prvním bloku a 5 ve druhém.</summary>
    [Fact]
    public void BlokyJsouStejneVelke_PosledniNejvyseStejny()
    {
        var bloky = Prvek().Rozvrzeni(SirokaLinka(41));

        Assert.True(bloky.Count > 1, "41 spojů se do 190 mm vejít nemá");
        int prvni = bloky[0].Pocet;
        Assert.All(bloky.Take(bloky.Count - 1), b => Assert.Equal(prvni, b.Pocet));
        Assert.True(bloky[^1].Pocet <= prvni);
    }

    /// <summary>Žádný spoj se dělením neztratí ani nezopakuje.</summary>
    [Fact]
    public void VsechnySpojeJsouPraveJednou()
    {
        var bloky = Prvek().Rozvrzeni(SirokaLinka(41));

        Assert.Equal(41, bloky.Sum(b => b.Pocet));
        int ocekavany = 0;
        foreach (var b in bloky) { Assert.Equal(ocekavany, b.Od); ocekavany += b.Pocet; }
    }

    /// <summary>Vejde-li se linka celá, blok je jeden - dělení se nesmí plést do běžného případu.</summary>
    [Fact]
    public void UzkaLinka_JedenBlok() => Assert.Single(Prvek().Rozvrzeni(SirokaLinka(4)));

    /// <summary>Výška roste s počtem bloků - stránkování musí vědět, že tabulka je pod sebou
    /// několikrát. Kdyby se počítala jen z jednoho bloku, zbytek by se ořízl.</summary>
    [Fact]
    public void VyskaRosteSPoctemBloku()
    {
        var prvek = Prvek();
        prvek.Height = null;   // dynamická výška, jinak se počítá z pevné hodnoty
        var uzka = SirokaLinka(4);
        var siroka = SirokaLinka(41);

        float vyskaJednoho = prvek.CalculateHeightFromData(uzka);
        int bloku = prvek.Rozvrzeni(siroka).Count;
        float vyskaVice = prvek.CalculateHeightFromData(siroka);

        Assert.True(bloku > 1);
        Assert.True(vyskaVice > vyskaJednoho * (bloku - 1),
            $"{bloku} bloků má být zhruba {bloku}× vyšší než jeden ({vyskaVice:0.0} vs {vyskaJednoho:0.0} mm)");
    }

    /// <summary>
    /// ⚠️ Jádro věci: <b>na sloupec spoje musí zbýt tolik, aby se do něj vešel čas</b>. Tohle je ta
    /// podmínka, která v návrháři chyběla - dno šířky bylo pevné 3 mm a čas se do 3 mm nevejde
    /// v žádném písmu. Dělení se přitom řídilo právě tím dnem, takže do bloku nasypalo tolik spojů,
    /// kolik se jich při nečitelné šířce vešlo, a tabulka vyšla jako slitá šeď (nahlásil Jan 25. 8.).
    /// </summary>
    [Fact]
    public void SloupecSpojeUnesePlnyCas()
    {
        var prvek = Prvek();
        float potreba = SirkaCasuMm(prvek.DataStyle.FontSize ?? 9f);

        var bloky = prvek.Rozvrzeni(SirokaLinka(60));

        Assert.All(bloky, b => Assert.True(b.SloupecSpojeMm >= potreba,
            $"sloupec spoje má {b.SloupecSpojeMm:0.00} mm, čas „00:00\" potřebuje {potreba:0.00} mm"));
    }

    /// <summary>Šířka času v milimetrech - z pohledu toho, kdo se na tisk dívá, ne z výpočtu prvku.</summary>
    private static float SirkaCasuMm(float fontPt)
    {
        using var face = SKTypeface.FromFamilyName("Arial");
        using var font = new SKFont(face, fontPt);            // velikost v pt → míra vyjde v pt
        return font.MeasureText("00:00") * 0.3528f;
    }
}
