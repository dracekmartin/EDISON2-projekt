using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// `B151`: když tabulce linky chybí do stránky málo, zmenší se, místo aby se kvůli pár zastávkám
/// zakládala další strana. Mez je 90 % (rozhodnutí Jan 14. 9.).
/// </summary>
public class ZmensitMistoRezuTests
{
    private static LineTableElement Prvek()
    {
        var p = ElementRenderFixtures.LineTable();
        p.X = 0; p.Y = 0; p.Height = null;
        return p;
    }

    private static Template Sablona(LineTableElement prvek, float vyskaStrankyMm) => new()
    {
        PageSize = new PageSize { Width = 210, Height = vyskaStrankyMm },
        Sections =
        [
            new Section
            {
                Name = "Jízdní řád",
                PrintBehavior = SectionPrintBehavior.Once,
                OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                DesignerHeight = 10,
                Elements = [prvek]
            }
        ]
    };

    /// <summary>Výška stránky, při které se obsah vejde přesně na jednu stranu.</summary>
    private static float VyskaAkorat(LineTableElement prvek, DataContext data) =>
        new SkiaHeightCalculator().CalculateHeight(prvek, 210f, data);

    /// <summary>
    /// Tabulce chybí do stránky pár procent → vejde se na jednu stranu zmenšená, ne na dvě.
    /// </summary>
    [Fact]
    public void ChybiMalo_VejdeSeNaJednuStranku()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 40);
        float akorat = VyskaAkorat(prvek, data);

        // Stránka o 5 % kratší, než obsah potřebuje - na mez 90 % to pohodlně stačí.
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(prvek, akorat * 0.95f), data);

        Assert.Equal(1, vysledek.TotalPages);
        Assert.True(prvek.LayoutZmenseni < 1f, "tabulka se měla zmenšit");
        Assert.True(prvek.LayoutZmenseni >= 0.90f,
            $"zmenšení {prvek.LayoutZmenseni:P0} přelezlo mez 90 %");
    }

    /// <summary>
    /// Tabulce chybí hodně → mez 90 % na to nestačí, takže se tiskne v plné velikosti a řeže
    /// jako dřív. Řez ve zmenšeném písmu by stál menší text **i** stránku navíc.
    /// </summary>
    [Fact]
    public void ChybiHodne_TisknesSePlneAReze()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 40);
        float akorat = VyskaAkorat(prvek, data);

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(prvek, akorat * 0.55f), data);

        Assert.True(vysledek.TotalPages > 1, "na polovinu potřebné výšky se to vejít nemá");
        Assert.Equal(1f, prvek.LayoutZmenseni);
    }

    /// <summary>
    /// Co se vejde v plné velikosti, se zmenšovat nesmí - zmenšení, které stránku neušetří,
    /// uživateli jen zhorší čitelnost.
    /// </summary>
    [Fact]
    public void VejdeSeVPlneVelikosti_Nezmensuje()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 40);
        float akorat = VyskaAkorat(prvek, data);

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(prvek, akorat * 1.2f), data);

        Assert.Equal(1, vysledek.TotalPages);
        Assert.Equal(1f, prvek.LayoutZmenseni);
    }

    /// <summary>
    /// Poznámky pod tabulkou drží druhou stránku samy o sobě, takže počet stran dokumentu se
    /// zmenšením nezmění - tabulka se přesto zmenšit **musí**, protože se tím přestane dělit.
    /// </summary>
    /// <remarks>
    /// Tohle je ten případ, kvůli kterému se 14. 9. spouštěč překlopil z počtu stran dokumentu na
    /// počet stran tabulky (upřesnění Jan): poznámky jsou pod tabulkou skoro vždycky, takže podle
    /// dokumentu by se nezmenšilo nikdy.
    /// </remarks>
    [Fact]
    public void PoznamkyDrziDruhouStranku_TabulkaSePrestoZmensi()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 40);
        float akorat = VyskaAkorat(prvek, data);
        float vyskaStranky = akorat * 0.95f;

        var sablona = Sablona(prvek, vyskaStranky);
        // Blok pod tabulkou, který se na první stranu nevejde nikdy - jako legenda poznámek.
        sablona.Sections.Add(new Section
        {
            Name = "Poznámky",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.KeepTogether,
            DesignerHeight = vyskaStranky * 0.7f,
        });

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.Equal(2, vysledek.TotalPages);     // poznámky si druhou stranu drží
        Assert.True(prvek.LayoutZmenseni < 1f, "tabulka se měla zmenšit, i když počet stran zůstal");
        int stranekTabulky = vysledek.Pages.Count(p => p.Sections.Any(s => s.Source.Name == "Jízdní řád"));
        Assert.Equal(1, stranekTabulky);
    }

    /// <summary>
    /// Zmenšení je svislé - **vodorovné dělení na bloky se nesmí hnout**. Šířky sloupců rozhodují,
    /// kolik spojů se vejde do jednoho bloku; kdyby je zmenšení taky zúžilo, vyšla by jiná sestava,
    /// ne jen hustší.
    /// </summary>
    [Fact]
    public void ZmenseniNemeniVodorovneDeleni()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(120, pocetZastavek: 40);
        int blokuPlne = prvek.Rozvrzeni(data).Count;

        prvek.LayoutZmenseni = 0.90f;
        int blokuZmensene = prvek.Rozvrzeni(data).Count;

        Assert.True(blokuPlne > 1, "120 spojů se do jedné šířky vejít nemá");
        Assert.Equal(blokuPlne, blokuZmensene);
    }
}
