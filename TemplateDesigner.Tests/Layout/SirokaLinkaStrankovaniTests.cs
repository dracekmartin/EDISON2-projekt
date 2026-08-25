using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Doklad k rozhodnutí, že **vodorovné opakování sekce není potřeba** (plánovaný `L5-3`, zrušen
/// 25. 8.): bloky se sázejí pod sebe, takže široká linka vyjede na víc stránek **stávajícím
/// svislým** stránkováním.
/// </summary>
/// <remarks>
/// Bez tohohle testu by to bylo jen tvrzení v dokumentaci. Zrušený krok plánu se neobhajuje
/// úvahou - buď to ta cesta unese, nebo ho bylo potřeba udělat.
/// </remarks>
public class SirokaLinkaStrankovaniTests
{
    private static Template Sablona(LineTableElement prvek) => new()
    {
        PageSize = PageSize.A4Portrait,
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

    private static LineTableElement Prvek()
    {
        var p = ElementRenderFixtures.LineTable();
        p.X = 0; p.Y = 0; p.Height = null;   // dynamická výška - roste s počtem bloků
        return p;
    }

    /// <summary>Linka, která se do šířky nevejde, vyjede na víc stránek - a to bez jediné změny
    /// v enginu, jen tím, že si prvek řekne o větší výšku.</summary>
    [Fact]
    public void SirokaLinka_VyjedeNaViceStranek()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(200, pocetZastavek: 25);
        int bloku = prvek.Rozvrzeni(data).Count;

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(Sablona(prvek), data);

        Assert.True(bloku > 1, "200 spojů se do jedné šířky vejít nemá");
        Assert.True(vysledek.TotalPages > 1,
            $"{bloku} bloků pod sebou se má rozpadnout na víc stránek, vyšla jedna");
    }

    /// <summary>Úzká linka zůstane na jedné stránce - stránkování se nesmí plést do běžného případu.</summary>
    [Fact]
    public void UzkaLinka_ZustaneNaJedneStrance()
    {
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(Prvek()), LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 25));

        Assert.Equal(1, vysledek.TotalPages);
    }

    /// <summary>Rozřezaná sekce navazuje bez díry i bez překryvu - jinak by na přechodu stránek
    /// zmizel nebo se zopakoval pruh tabulky.</summary>
    [Fact]
    public void ChunkyNaSebeNavazuji()
    {
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(Prvek()), LineTableBlokyTests.SirokaLinkaData(200, pocetZastavek: 25));

        float? konecPredchoziho = null;
        foreach (var stranka in vysledek.Pages)
        {
            var sekce = Assert.Single(stranka.Sections);
            if (konecPredchoziho is float k) Assert.Equal(k, sekce.ClipTop!.Value, 2);
            konecPredchoziho = sekce.ClipBottom;
        }
    }

    /// <summary>
    /// ⚠️ Řez stránky musí padnout <b>na hranici bloku</b>, ne kam zrovna dojde stránka.
    /// </summary>
    /// <remarks>
    /// Změřeno 25. 8. na A4 na šířku: dva bloky po 105,7 mm na ploše 210 mm daly první stránku
    /// s blokem a půl a druhou s <b>1,4 mm proužkem</b> - tabulka useknutá pár řádků před koncem.
    /// Geometrický řez o blocích nevěděl; prvek je teď nabízí přes <c>INedelitelneDily</c>.
    /// </remarks>
    [Fact]
    public void RezPadneNaHraniciBloku()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var data = LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28);
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;

        var hranice = ((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.NotEmpty(hranice);
        Assert.True(vysledek.TotalPages > 1, "linka se má rozpadnout na víc stránek");
        foreach (var stranka in vysledek.Pages.Skip(1))
        {
            float rez = stranka.Sections[0].ClipTop!.Value;
            Assert.True(hranice.Any(z => Math.Abs(z - rez) < 0.05f),
                $"řez na {rez:0.0} mm nepadl na hranici bloku ({string.Join(", ", hranice.Select(z => $"{z:0.0}"))})");
        }
    }

    /// <summary>
    /// Díl vyšší než celá stránka se rozřízne i tak - jinak by se nevešel nikdy a stránkování by se
    /// zacyklilo. Řez ale musí padnout <b>mezi řádky zastávek</b>, ne doprostřed textu: proto prvek
    /// nabízí vedle hranic bloků i slabé zlomy na hranicích řádků.
    /// </summary>
    [Fact]
    public void BlokVyssiNezStranka_SeRozrizneMeziRadky()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        // jeden blok (málo spojů) a tolik zastávek, že sám přeroste stránku
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 200);

        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(data);
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.True(vysledek.TotalPages > 1);
        Assert.Single(((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).DefaultIfEmpty(),
            default(Zlom));   // jediný blok → žádná silná hranice
        foreach (var stranka in vysledek.Pages.Skip(1))
        {
            float rez = stranka.Sections[0].ClipTop!.Value;
            Assert.True(zlomy.Any(z => Math.Abs(z.Mm - rez) < 0.05f),
                $"řez na {rez:0.0} mm nepadl mezi řádky - tabulka je useknutá uprostřed textu");
        }
    }

    /// <summary>Hranice bloku má přednost před hranicí řádku, i když je výš. Kdyby se bralo prostě
    /// „poslední, co se vejde", řez by padl doprostřed bloku pokaždé, když v něm zbyl nějaký řádek.</summary>
    [Fact]
    public void SilnyZlomMaPrednostPredSlabym()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        var data = LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28);

        var silne = ((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.NotEmpty(silne);
        foreach (var stranka in vysledek.Pages.Skip(1))
            Assert.Contains(silne, z => Math.Abs(z - stranka.Sections[0].ClipTop!.Value) < 0.05f);
    }

    /// <summary>
    /// Když řez padne <b>doprostřed</b> bloku, pokračovací stránka musí zopakovat jeho hlavu -
    /// jinak jsou tam řádky zastávek bez čísel spojů a nejde poznat, který sloupec je který spoj.
    /// Místo na ni vyhradí stránkování, aby ji prvek nepřetiskl přes obsah.
    /// </summary>
    [Fact]
    public void RezUprostredBloku_ZopakujeHlavicku()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 200);   // jeden vysoký blok

        float hlavicka = ((INedelitelneDily)prvek).OpakovanaHlavickaMm(data);
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.True(hlavicka > 0.01f, "prvek má hlavu, kterou lze zopakovat");
        Assert.True(vysledek.TotalPages > 1);
        Assert.Equal(0f, vysledek.Pages[0].Sections[0].OpakovanaHlavickaMm);   // první stránka ji má vlastní
        foreach (var stranka in vysledek.Pages.Skip(1))
            Assert.Equal(hlavicka, stranka.Sections[0].OpakovanaHlavickaMm, 2);
    }

    /// <summary>Stránka, která začíná novým blokem, si hlavičku přinese sama - opakovat se nesmí,
    /// jinak by tam byla dvakrát a ukrojila by místo, které nikdo nepotřebuje.</summary>
    [Fact]
    public void RezNaHraniciBloku_HlavickuNeopakuje()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(sablona, LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28));

        Assert.True(vysledek.TotalPages > 1);
        Assert.All(vysledek.Pages, s => Assert.Equal(0f, s.Sections[0].OpakovanaHlavickaMm));
    }
}
