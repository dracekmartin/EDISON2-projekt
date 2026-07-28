using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Testy PageLayoutEngine - čistá logika (Template + DataContext → stránky),
/// výšky elementů dodává FixedHeightCalculator (žádný SkiaSharp).
/// A4 bez okrajů → tisková plocha 297 mm na výšku.
/// </summary>
public class PageLayoutEngineTests
{
    private static PageLayoutEngine Engine(float? dynamicHeight = null) =>
        new(new FixedHeightCalculator(dynamicHeight));

    private static Template A4Template(params Section[] sections) => new()
    {
        PageSize = PageSize.A4Portrait,   // 210×297, okraje 0
        Sections = [.. sections]
    };

    private static Section BodySection(float height, string name = "Sekce",
        SectionOverflowBehavior overflow = SectionOverflowBehavior.KeepTogether) => new()
    {
        Name = name,
        DesignerHeight = height,
        PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = overflow
    };

    // ── Základní stránkování ─────────────────────────────────────────────────

    [Fact]
    public void PrazdnaSablona_VratiJednuPrazdnouStranku()
    {
        var result = Engine().Layout(A4Template(), null);

        Assert.Equal(1, result.TotalPages);
        Assert.Empty(result.Pages[0].Sections);
    }

    [Fact]
    public void DveSekceKtereSeVejdou_JednaStranka_SpravnaYPozice()
    {
        var result = Engine().Layout(A4Template(BodySection(100), BodySection(100)), null);

        Assert.Equal(1, result.TotalPages);
        var placed = result.Pages[0].Sections;
        Assert.Equal(2, placed.Count);
        Assert.Equal(0f, placed[0].Y);
        Assert.Equal(100f, placed[1].Y);
        Assert.Equal(100f, placed[1].Height);
    }

    [Fact]
    public void KeepTogether_SekcePresahujiciStranku_SePresunouNaDalsi()
    {
        // 3×150 mm na 297 mm plochu → každá sekce na vlastní stránce
        var result = Engine().Layout(
            A4Template(BodySection(150), BodySection(150), BodySection(150)), null);

        Assert.Equal(3, result.TotalPages);
        foreach (var page in result.Pages)
        {
            var placed = Assert.Single(page.Sections);
            Assert.Equal(0f, placed.Y);
            Assert.Equal(150f, placed.Height);
        }
    }

    [Fact]
    public void KeepTogether_SekceVyssiNezStranka_ZustaneNaPrazdneStrance()
    {
        // Sekce 400 mm se nikam nevejde - nesmí vzniknout prázdná úvodní stránka ani smyčka
        var result = Engine().Layout(A4Template(BodySection(400)), null);

        Assert.Equal(1, result.TotalPages);
        var placed = Assert.Single(result.Pages[0].Sections);
        Assert.Equal(0f, placed.Y);
        Assert.Equal(400f, placed.Height);
    }

    [Fact]
    public void SplitAcrossPages_RozdeliSekciNaChunkySeSpravnymClipem()
    {
        var result = Engine().Layout(
            A4Template(BodySection(400, overflow: SectionOverflowBehavior.SplitAcrossPages)), null);

        Assert.Equal(2, result.TotalPages);

        var chunk1 = Assert.Single(result.Pages[0].Sections);
        Assert.Equal(0f, chunk1.Y);
        Assert.Equal(297f, chunk1.Height, precision: 2);
        Assert.Equal(0f, chunk1.ClipTop!.Value, precision: 2);
        Assert.Equal(297f, chunk1.ClipBottom!.Value, precision: 2);

        var chunk2 = Assert.Single(result.Pages[1].Sections);
        Assert.Equal(0f, chunk2.Y);
        Assert.Equal(103f, chunk2.Height, precision: 2);
        Assert.Equal(297f, chunk2.ClipTop!.Value, precision: 2);
        Assert.Equal(400f, chunk2.ClipBottom!.Value, precision: 2);
    }

    // ── Hlavičky a patičky (PrintBehavior × Role) ────────────────────────────

    private static Section Header(SectionPrintBehavior behavior, float height = 20f) => new()
    {
        Name = "Hlavička",
        DesignerHeight = height,
        PrintBehavior = behavior,
        Role = SectionRole.Header
    };

    private static Section Footer(SectionPrintBehavior behavior, float height = 15f) => new()
    {
        Name = "Patička",
        DesignerHeight = height,
        PrintBehavior = behavior,
        Role = SectionRole.Footer
    };

    [Fact]
    public void OnEveryPageHlavicka_JeNaKazdeStranceANahore()
    {
        var result = Engine().Layout(A4Template(
            Header(SectionPrintBehavior.OnEveryPage),
            BodySection(500, overflow: SectionOverflowBehavior.SplitAcrossPages)), null);

        Assert.True(result.TotalPages >= 2);
        foreach (var page in result.Pages)
        {
            var header = page.Sections.First(s => s.Source.Name == "Hlavička");
            Assert.Equal(0f, header.Y);
            // Body obsah začíná až pod hlavičkou
            var body = page.Sections.First(s => s.Source.Name == "Sekce");
            Assert.Equal(20f, body.Y);
        }
    }

    [Fact]
    public void OnFirstPageOnlyHlavicka_JeJenNaPrvniStrance()
    {
        var result = Engine().Layout(A4Template(
            Header(SectionPrintBehavior.OnFirstPageOnly),
            BodySection(500, overflow: SectionOverflowBehavior.SplitAcrossPages)), null);

        Assert.True(result.TotalPages >= 2);
        Assert.Contains(result.Pages[0].Sections, s => s.Source.Name == "Hlavička");
        Assert.DoesNotContain(result.Pages[1].Sections, s => s.Source.Name == "Hlavička");
    }

    [Fact]
    public void OnEveryPageExceptFirstHlavicka_ChybiNaPrvniStrance()
    {
        var result = Engine().Layout(A4Template(
            Header(SectionPrintBehavior.OnEveryPageExceptFirst),
            BodySection(500, overflow: SectionOverflowBehavior.SplitAcrossPages)), null);

        Assert.True(result.TotalPages >= 2);
        Assert.DoesNotContain(result.Pages[0].Sections, s => s.Source.Name == "Hlavička");
        Assert.Contains(result.Pages[1].Sections, s => s.Source.Name == "Hlavička");
    }

    [Fact]
    public void OnLastPageOnlyPaticka_JeJenNaPosledniStrance_DvouprochodovyLayout()
    {
        var result = Engine().Layout(A4Template(
            BodySection(500, overflow: SectionOverflowBehavior.SplitAcrossPages),
            Footer(SectionPrintBehavior.OnLastPageOnly)), null);

        Assert.True(result.TotalPages >= 2);
        Assert.DoesNotContain(result.Pages[0].Sections, s => s.Source.Name == "Patička");
        Assert.Contains(result.Pages[^1].Sections, s => s.Source.Name == "Patička");
    }

    [Fact]
    public void Paticka_JeUkotvenaKeSpodnimuOkrajiStranky()
    {
        var result = Engine().Layout(A4Template(
            BodySection(50),
            Footer(SectionPrintBehavior.OnEveryPage, height: 15f)), null);

        var footer = result.Pages[0].Sections.First(s => s.Source.Name == "Patička");
        Assert.Equal(297f - 15f, footer.Y, precision: 2);
    }

    // ── PAGE / TOTAL_PAGES ───────────────────────────────────────────────────

    [Fact]
    public void PageATotalPages_SeInjektujiDoDatKazdeSekce_IBezVstupnichDat()
    {
        var result = Engine().Layout(A4Template(
            BodySection(150), BodySection(150), BodySection(150)), null);

        Assert.Equal(3, result.TotalPages);
        for (int i = 0; i < 3; i++)
        {
            var placed = Assert.Single(result.Pages[i].Sections);
            Assert.NotNull(placed.Data);
            Assert.Equal((i + 1).ToString(), placed.Data!.GetString("PAGE"));
            Assert.Equal("3", placed.Data.GetString("TOTAL_PAGES"));
        }
    }

    // ── PerDataRow ───────────────────────────────────────────────────────────

    [Fact]
    public void PerDataRow_ExpandujeSekciPerRadekSDatyRadku()
    {
        var section = new Section
        {
            Name = "Řádek",
            DesignerHeight = 20f,
            PrintBehavior = SectionPrintBehavior.PerDataRow,
            DataSource = "POLOZKY"
        };
        var data = new DataContext(rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["POLOZKY"] = new List<DataContext>
            {
                new(new Dictionary<string, object?> { ["NAZEV"] = "A" }),
                new(new Dictionary<string, object?> { ["NAZEV"] = "B" }),
                new(new Dictionary<string, object?> { ["NAZEV"] = "C" }),
            }
        });

        var result = Engine().Layout(A4Template(section), data);

        var placed = result.Pages[0].Sections;
        Assert.Equal(3, placed.Count);
        Assert.Equal("A", placed[0].Data!.GetString("NAZEV"));
        Assert.Equal("B", placed[1].Data!.GetString("NAZEV"));
        Assert.Equal("C", placed[2].Data!.GetString("NAZEV"));
    }

    // ── VisibilityExpression ─────────────────────────────────────────────────

    [Fact]
    public void SekceSFalseVisibilityExpression_SeNevytiskne()
    {
        var hidden = BodySection(50, name: "Skrytá");
        hidden.VisibilityExpression = "{ZOBRAZIT} == 1";
        var data = new DataContext(new Dictionary<string, object?> { ["ZOBRAZIT"] = "0" });

        var result = Engine().Layout(A4Template(hidden, BodySection(50, name: "Viditelná")), data);

        var placed = result.Pages[0].Sections;
        Assert.Single(placed);
        Assert.Equal("Viditelná", placed[0].Source.Name);
    }

    // ── Dynamická výška elementu ─────────────────────────────────────────────

    [Fact]
    public void DynamickyElement_ZvetsiSekciNadDesignerHeight()
    {
        var section = BodySection(30);
        section.Elements.Add(new TextElement { X = 0, Y = 0, Width = 100, Height = null });

        // Kalkulátor vrátí dynamickému elementu 50 mm → sekce = max(30, 50)
        var result = Engine(dynamicHeight: 50f).Layout(A4Template(section), null);

        var placed = Assert.Single(result.Pages[0].Sections);
        Assert.Equal(50f, placed.Height, precision: 2);
    }

    // ── Kultura ──────────────────────────────────────────────────────────────

    [Fact]
    public void CultureCode_SePropagujeDoDatSekci()
    {
        var template = A4Template(BodySection(50));
        template.CultureCode = "cs-CZ";
        var data = new DataContext(new Dictionary<string, object?> { ["X"] = "1" });

        var result = Engine().Layout(template, data);

        var placed = Assert.Single(result.Pages[0].Sections);
        Assert.Equal("cs-CZ", placed.Data!.Culture.Name);
    }
}
