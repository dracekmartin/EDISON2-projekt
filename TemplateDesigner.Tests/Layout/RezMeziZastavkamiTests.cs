using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// `B135`: řez tabulky linky smí padnout jen na hranici mezi zastávkami. Zastávka rozdvojená na
/// příjezd a odjezd, stejně jako zastávka se svými přípojnými řádky, odchází na další stránku
/// vcelku.
/// </summary>
/// <remarks>
/// Bez tohohle by řez rozdělil „př" a „od" téže zastávky - v dnešním tvaru je to ošklivé, ale
/// čitelné (název se opakuje na obou řádcích), v postskriptovém tvaru (`B140`) je název psaný
/// jednou přes oba řádky, takže by druhá stránka dostala řádek bez názvu i bez tarifního čísla.
/// Staré pravidlo zní stejně (`JRapiLinPrtPS8.mac:83` couvá po dvou, dokud nenarazí na obyčejnou
/// jednořádkovou zastávku) - podklad v `docs/linkovy-knizni-jr-implementace-analyza.md §17.8`.
/// </remarks>
public class RezMeziZastavkamiTests
{
    /// <summary>Linka, kde zastávka na daném pořadí má příjezdový i odjezdový řádek.</summary>
    /// <param name="rozdvojenaPoradi">Pořadí zastávek, které dostanou dva řádky.</param>
    private static DataContext Linka(int pocetSpoju, int pocetZastavek, params int[] rozdvojenaPoradi)
    {
        var zastavky = new List<DataContext>();
        for (int i = 1; i <= pocetZastavek; i++)
        {
            if (rozdvojenaPoradi.Contains(i))
                zastavky.Add(RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "prijezd"),
                                                ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())));
            zastavky.Add(RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "zastavka"),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())));
        }

        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i => RenderSnapshot.Row(("CISLO_SPOJE", (i * 2).ToString()), ("SKUPINA", "1")))
            .ToArray();

        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    /// <summary>Linka, kde vybraná zastávka nese přípojný řádek (na spoj navazuje jiná linka).</summary>
    private static DataContext LinkaSPripojem(int pocetSpoju, int pocetZastavek, int pripojNaPoradi)
    {
        var zastavky = Enumerable.Range(1, pocetZastavek)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "zastavka"),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())))
            .ToArray();

        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i =>
            {
                var pole = new List<(string, object?)>
                {
                    ("CISLO_SPOJE", (i * 2).ToString()),
                    ("SKUPINA", "1"),
                };
                if (i == 1)
                    pole.Add(("PRIPOJE", RenderSnapshot.Nested(
                        [[("TYP", "navazuje"), ("PORADI", pripojNaPoradi.ToString()), ("RADEK", "1"),
                          ("CAS", "12:30"), ("LINKA", "330520"), ("SPOJ", "7"), ("TEXT", "Kaplice")]])));
                return RenderSnapshot.Row([.. pole]);
            })
            .ToArray();

        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    private static LineTableElement Prvek()
    {
        var p = ElementRenderFixtures.LineTable();
        p.X = 0; p.Y = 0; p.Height = null;   // dynamická výška - o stránkování rozhoduje engine
        return p;
    }

    private static Template Sablona(LineTableElement prvek, float vyskaStrankyMm = 297f) => new()
    {
        PageSize = new PageSize { Width = 210, Height = vyskaStrankyMm },
        Sections =
        [
            new Section
            {
                Name = "Jízdní řád",
                PrintBehavior = SectionPrintBehavior.Once,
                OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                DesignerHeight = 50,
                Elements = [prvek]
            }
        ]
    };

    /// <summary>
    /// Deset zastávek, z toho tři rozdvojené = 13 řádků. Hranic uvnitř bloku je 12, z toho
    /// devět má být mezi zastávkami a tři uvnitř rozdvojených.
    /// </summary>
    [Fact]
    public void RozdvojenaZastavka_HraniceUvnitrNiJeJenNouzova()
    {
        var prvek = Prvek();
        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(Linka(6, pocetZastavek: 10, 3, 6, 9));

        Assert.DoesNotContain(zlomy, z => z.Uroven == ZlomUroven.Blok);   // úzká linka = jeden blok
        Assert.Equal(9, zlomy.Count(z => z.Uroven == ZlomUroven.Zastavka));
        Assert.Equal(3, zlomy.Count(z => z.Uroven == ZlomUroven.Radek));
    }

    /// <summary>Bez rozdvojených zastávek je každá hranice plnohodnotná - nouzová žádná.</summary>
    [Fact]
    public void BezneZastavky_ZadnaHraniceNeniNouzova()
    {
        var prvek = Prvek();
        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(Linka(6, pocetZastavek: 10));

        Assert.Equal(9, zlomy.Count(z => z.Uroven == ZlomUroven.Zastavka));
        Assert.DoesNotContain(zlomy, z => z.Uroven == ZlomUroven.Radek);
    }

    /// <summary>
    /// Přípojný řádek patří ke své zastávce, takže hranice mezi nimi je jen nouzová - jinak by
    /// „navazuje" viselo na další stránce bez zastávky, ke které se vztahuje.
    /// </summary>
    [Fact]
    public void PripojnyRadek_HraniceNadNimJeJenNouzova()
    {
        var prvek = Prvek();
        var data = LinkaSPripojem(6, pocetZastavek: 10, pripojNaPoradi: 4);

        var sPripojem = ((INedelitelneDily)prvek).ZlomyMm(data);
        var bez = ((INedelitelneDily)Prvek()).ZlomyMm(Linka(6, pocetZastavek: 10));

        Assert.Equal(1, sPripojem.Count(z => z.Uroven == ZlomUroven.Radek));
        Assert.Equal(bez.Count(z => z.Uroven == ZlomUroven.Zastavka),
                     sPripojem.Count(z => z.Uroven == ZlomUroven.Zastavka));
    }

    /// <summary>
    /// Nouzová úroveň musí zůstat nabídnutá - bez ní by stránkování u zastávky vyšší než celá
    /// stránka nemělo kam říznout a smyčka by se zacyklila.
    /// </summary>
    [Fact]
    public void NouzovaUrovenZustavaNabidnuta()
    {
        var prvek = Prvek();
        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(Linka(6, pocetZastavek: 4, 2));

        Assert.Contains(zlomy, z => z.Uroven == ZlomUroven.Radek);
    }

    /// <summary>
    /// Celý řetěz: dlouhá linka s rozdvojenými zastávkami se rozpadne na víc stránek a **žádný**
    /// řez nesmí padnout doprostřed zastávky.
    /// </summary>
    /// <remarks>
    /// Tohle je ta brána, která drží featuru. Testy nad <c>ZlomyMm</c> výš jen říkají, co se
    /// nabízí; teprve tady je vidět, co si z nabídky vzal engine.
    /// </remarks>
    [Theory]
    [InlineData(297f)] [InlineData(280f)] [InlineData(263f)] [InlineData(241f)]
    [InlineData(220f)] [InlineData(198f)] [InlineData(176f)] [InlineData(155f)]
    [InlineData(133f)] [InlineData(112f)]
    public void RezNaStrankach_NepadneDoprostredZastavky(float vyskaStranky)
    {
        var prvek = Prvek();
        // Rozdvojené jsou VŠECHNY zastávky, takže každá druhá hranice je uvnitř zastávky.
        // ⚠️ Výšek stránky je víc schválně: při jedné se „poslední, co se vejde" trefí do dobré
        // hranice náhodou (řádky jsou stejně vysoké, takže o všem rozhoduje parita) a test by
        // mlčel. Napříč deseti výškami parita vyjde jednou tak a jednou tak - ověřeno mutací.
        var data = Linka(6, pocetZastavek: 120, [.. Enumerable.Range(1, 120)]);
        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(data);

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(prvek, vyskaStranky), data);
        var rezy = vysledek.Pages
            .SelectMany(p => p.Sections)
            .Select(s => s.ClipBottom ?? 0f)
            .Where(y => y > 0.01f)
            .ToList();

        Assert.True(vysledek.TotalPages > 1, "120 rozdvojených zastávek se na jednu stránku vejít nemá");

        var nouzove = zlomy.Where(z => z.Uroven == ZlomUroven.Radek).Select(z => z.Mm).ToList();
        foreach (float rez in rezy.Take(rezy.Count - 1))   // poslední řez je konec obsahu, ne zlom
            Assert.DoesNotContain(nouzove, y => Math.Abs(y - rez) < 0.05f);
    }
}
