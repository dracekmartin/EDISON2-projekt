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
}
