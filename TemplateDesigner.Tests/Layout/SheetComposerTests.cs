using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Testy SheetComposer - čistá logika imposice (stránky → listy): parsování konfigurace
/// z exportních polí, auto mřížka, sekvenční skládání, opakování stránky, centrování bloku.
/// </summary>
public class SheetComposerTests
{
    private static readonly PageSize A5 = new() { Width = 148, Height = 210 };

    private static SheetLayoutOptions A5OnA4Landscape(bool repeat = false, bool marks = false) => new()
    {
        SheetWidth = 297, SheetHeight = 210,
        Rows = 1, Cols = 2,
        PageWidth = 148, PageHeight = 210,
        RepeatEachPage = repeat,
        DrawCutMarks = marks,
    };

    private static List<Page> Pages(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => new Page())];

    // ── Parsování formátů a mřížky ───────────────────────────────────────────

    [Theory]
    [InlineData("A4_PORTRAIT", 210f, 297f)]
    [InlineData("A4_LANDSCAPE", 297f, 210f)]
    [InlineData("A3_PORTRAIT", 297f, 420f)]
    [InlineData("A3_LANDSCAPE", 420f, 297f)]
    public void ParseSheetFormat_ZnameKlice(string key, float w, float h)
    {
        Assert.Equal((w, h), SheetComposer.ParseSheetFormat(key));
    }

    [Fact]
    public void ParseSheetFormat_NeznamyKlic_VraciNull()
    {
        Assert.Null(SheetComposer.ParseSheetFormat("B5_PORTRAIT"));
    }

    [Fact]
    public void ResolveGrid_Auto_NajdeNejvetsiFit()
    {
        // A5 (148×210) na A4 naležato (297×210) → 1 řádek × 2 sloupce
        Assert.Equal((1, 2), SheetComposer.ResolveGrid("AUTO", 148, 210, 297, 210));
        // A5 na A3 na výšku (297×420) → 2×2
        Assert.Equal((2, 2), SheetComposer.ResolveGrid("AUTO", 148, 210, 297, 420));
    }

    [Fact]
    public void ResolveGrid_ExplicitniMrizka_Projde()
    {
        Assert.Equal((1, 2), SheetComposer.ResolveGrid("1x2", 148, 210, 297, 210));
    }

    [Fact]
    public void ResolveGrid_MrizkaSeNevejde_Vyhodi()
    {
        // 2×2 A5 se na A4 naležato nevejde (2 řádky × 210 mm > 210 mm)
        var ex = Assert.Throws<ArgumentException>(() => SheetComposer.ResolveGrid("2x2", 148, 210, 297, 210));
        Assert.Contains("nevejde", ex.Message);
    }

    [Fact]
    public void ResolveGrid_StrankaVetsiNezList_Vyhodi()
    {
        // A3 stránka na A4 list
        Assert.Throws<ArgumentException>(() => SheetComposer.ResolveGrid("AUTO", 297, 420, 210, 297));
    }

    [Fact]
    public void ResolveGrid_NeplatnyKlic_Vyhodi()
    {
        Assert.Throws<ArgumentException>(() => SheetComposer.ResolveGrid("blabla", 148, 210, 297, 210));
    }

    // ── FromParamValues (wire hodnoty z EDInputFormu) ────────────────────────

    [Fact]
    public void FromParamValues_None_VraciNull()
    {
        var vals = new Dictionary<string, object?> { [SheetLayoutKeys.Sheet] = "NONE" };
        Assert.Null(SheetComposer.FromParamValues(vals, A5));

        // Chybějící klíč = taky vypnuto
        Assert.Null(SheetComposer.FromParamValues(new Dictionary<string, object?>(), A5));
    }

    [Fact]
    public void FromParamValues_A4Landscape_AutoMrizka()
    {
        var vals = new Dictionary<string, object?>
        {
            [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE",
            [SheetLayoutKeys.Grid] = "AUTO",
            [SheetLayoutKeys.Repeat] = "1",
            [SheetLayoutKeys.Marks] = "true",
        };
        var o = SheetComposer.FromParamValues(vals, A5)!;

        Assert.Equal((297f, 210f), (o.SheetWidth, o.SheetHeight));
        Assert.Equal((1, 2), (o.Rows, o.Cols));
        Assert.Equal((148f, 210f), (o.PageWidth, o.PageHeight));
        Assert.True(o.RepeatEachPage);
        Assert.True(o.DrawCutMarks);
    }

    // ── Compose - skládání stránek na listy ──────────────────────────────────

    [Fact]
    public void Compose_Sekvencne_DveStrankyNaList()
    {
        var pages = Pages(5);
        var sheets = SheetComposer.Compose(pages, A5OnA4Landscape());

        Assert.Equal(3, sheets.Count);
        Assert.Equal(2, sheets[0].Pages.Count);
        Assert.Equal(2, sheets[1].Pages.Count);
        Assert.Single(sheets[2].Pages);   // lichá stránka → poslední pozice prázdná

        // Pořadí: stránky po sobě zleva doprava
        Assert.Same(pages[0], sheets[0].Pages[0].Page);
        Assert.Same(pages[1], sheets[0].Pages[1].Page);
        Assert.Same(pages[4], sheets[2].Pages[0].Page);
    }

    [Fact]
    public void Compose_Opakovani_KazdaStrankaVyplniList()
    {
        var pages = Pages(3);
        var sheets = SheetComposer.Compose(pages, A5OnA4Landscape(repeat: true));

        Assert.Equal(3, sheets.Count);
        foreach (var (sheet, page) in sheets.Zip(pages))
        {
            Assert.Equal(2, sheet.Pages.Count);
            Assert.All(sheet.Pages, p => Assert.Same(page, p.Page));
        }
    }

    [Fact]
    public void Compose_CentrovaniBloku()
    {
        // 2×148 = 296 na listu 297 → blok začíná na X = 0.5; výška přesně sedí → Y = 0
        var sheets = SheetComposer.Compose(Pages(2), A5OnA4Landscape());

        Assert.Equal(0.5f, sheets[0].Pages[0].OffsetX, 3);
        Assert.Equal(0f, sheets[0].Pages[0].OffsetY, 3);
        Assert.Equal(148.5f, sheets[0].Pages[1].OffsetX, 3);
    }

    [Fact]
    public void Compose_PrazdnyVstup_ZadneListy()
    {
        Assert.Empty(SheetComposer.Compose([], A5OnA4Landscape()));
    }

    [Fact]
    public void CutLines_VsechnyHraniceVcetneKrajnich()
    {
        var o = A5OnA4Landscape(marks: true);

        Assert.Equal([0.5f, 148.5f, 296.5f], o.CutLinesX.Select(x => MathF.Round(x, 3)));
        Assert.Equal([0f, 210f], o.CutLinesY.Select(y => MathF.Round(y, 3)));
    }
}
