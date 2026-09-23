using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Parametr „Počet stran" (#8785): sazba zmenší nejdřív poznámky, pak tabulku.
/// </summary>
public class PocetStranTests
{
    private static DataContext SParametrem(string hodnota) =>
        new(new Dictionary<string, object?> { [PageLayoutEngine.CilStranKlic] = hodnota });

    [Theory]
    [InlineData("1 strana", 1)]
    [InlineData("2 strany", 2)]
    [InlineData("Automaticky (dle linky)", 0)]
    [InlineData("", 0)]
    public void CilStran_ZTextuVolby(string volba, int ocekavano)
    {
        Assert.Equal(ocekavano, PageLayoutEngine.CilStran(SParametrem(volba)));
    }

    [Fact]
    public void CilStran_BezDat_BezOmezeni()
    {
        Assert.Equal(0, PageLayoutEngine.CilStran(null));
    }

    private static DataContext Poznamky(int pocet) => RenderSnapshot.Data(
        ("POZNAMKY",
        [
            .. Enumerable.Range(1, pocet).Select(i => RenderSnapshot.Row(
                ("DRUH", "7"), ("TYP", "text"), ("ZNAK", ""),
                ("TEXT", $"poznámka číslo {i} s delším textem, který se zalomí do dalšího řádku legendy"))),
        ]));

    /// <summary>Zmenšení poznámek musí zmenšit i jejich výšku - jinak by sazba nic neušetřila.</summary>
    [Fact]
    public void ZmenseniLegendy_ZmensiJejiVysku()
    {
        var legenda = new NotesLegendElement { X = 0, Y = 0, Width = 90, Height = null };
        var data = Poznamky(12);

        float plna = legenda.CalculateHeightFromData(data);
        legenda.LayoutZmenseni = 0.6f;
        float mensi = legenda.CalculateHeightFromData(data);

        Assert.True(plna > 0);
        Assert.True(mensi < plna * 0.7f, $"plná {plna:0.0} mm, zmenšená {mensi:0.0} mm");
    }

    /// <summary>
    /// Sestava, kterou legenda vytlačí na druhou stranu, se s cílem „1 strana" vejde na jednu -
    /// a zmenšila se jen legenda.
    /// </summary>
    [Fact]
    public void CilJednaStrana_ZmensiLegenduAVejdeSe()
    {
        var legenda = new NotesLegendElement { X = 0, Y = 0, Width = 190, Height = null, Columns = 2 };
        var tpl = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 60, MarginTop = 0, MarginBottom = 0, MarginLeft = 0, MarginRight = 0 },
            // Prázdná sekce drží většinu stránky, na legendu zbyde 25 mm - v plné velikosti se tam
            // nevejde a skočí na druhou stranu.
            Sections =
            [
                new Section { Name = "Tabulka", DesignerHeight = 35f },
                new Section { Name = "Poznámky", DesignerHeight = null, Elements = [legenda] },
            ],
        };
        var engine = new PageLayoutEngine(new SkiaHeightCalculator());
        var data = Poznamky(14);

        int bezCile = engine.Layout(tpl, data).TotalPages;
        var sCilem = engine.Layout(tpl, data.WithExtraValues(
            new Dictionary<string, object?> { [PageLayoutEngine.CilStranKlic] = "1 strana" }));

        Assert.True(bezCile > 1, $"test potřebuje sestavu přes víc stran, vyšlo {bezCile}");
        Assert.Equal(1, sCilem.TotalPages);
        Assert.True(legenda.LayoutZmenseni < 1f);
        Assert.Null(sCilem.CilStranNesplnen);
    }

    /// <summary>
    /// Když se sestava nevejde ani na mezích, zůstane v plné velikosti - zmenšení, které stránku
    /// neušetří, jen zhorší čitelnost (rozhodnutí Jan 23. 9.).
    /// </summary>
    [Fact]
    public void CilNesplnitelny_ZustanePlnaVelikost()
    {
        var legenda = new NotesLegendElement { X = 0, Y = 0, Width = 190, Height = null, Columns = 2 };
        var tpl = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 60, MarginTop = 0, MarginBottom = 0, MarginLeft = 0, MarginRight = 0 },
            Sections =
            [
                new Section { Name = "Tabulka", DesignerHeight = 35f },
                new Section { Name = "Poznámky", DesignerHeight = null, Elements = [legenda] },
            ],
        };
        var engine = new PageLayoutEngine(new SkiaHeightCalculator());
        var data = Poznamky(80).WithExtraValues(
            new Dictionary<string, object?> { [PageLayoutEngine.CilStranKlic] = "1 strana" });

        var vysledek = engine.Layout(tpl, data);

        Assert.True(vysledek.TotalPages > 1);
        Assert.Equal(1, vysledek.CilStranNesplnen);
        Assert.Equal(1f, legenda.LayoutZmenseni);
    }
}
