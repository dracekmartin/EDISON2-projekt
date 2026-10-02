using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Services;
using TemplateDesigner.Tests.Rendering;
using UglyToad.PdfPig;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Import linkového JŘ nad skutečným PDF: vyrobí ho náš tisk ze šablony s hlavičkou, tabulkou
/// spojů a patičkou, a na něj se pustí skládání šablony s odpovědí, jakou by vrátil model. Model se
/// nevolá - ověřuje se to, co dělá import sám: že časy a zastávky z tabulky neskončí jako volné
/// texty a že texty kolem tabulky zůstanou.
/// </summary>
public class LineScheduleImportPdfTests(ITestOutputHelper output)
{
    private const float PageW = 297f, PageH = 210f, Margin = 10f, HeaderH = 15f, FooterH = 10f;

    private static readonly string[] Zastavky =
        ["Kroměříž", "Hulín", "Břest", "Kojetín", "Chropyně", "Bochoř", "Věrovany", "Přerov"];

    private static Template SourceTemplate() => new()
    {
        Name = "zdroj",
        AgendaCode = "LINKOVY_JR_AD",
        PageSize = new PageSize { Width = PageW, Height = PageH, MarginTop = Margin, MarginBottom = Margin,
                                  MarginLeft = Margin, MarginRight = Margin },
        Sections =
        [
            new Section
            {
                Name = "Hlavička", Role = SectionRole.Header, PrintBehavior = SectionPrintBehavior.OnEveryPage,
                DesignerHeight = HeaderH,
                Elements = [new TextElement { X = 0, Y = 2, Width = 100, Height = 8, Text = "Linka {CISLO_LINKY}",
                                              Style = new TextStyle { FontSize = 14 } }],
            },
            new Section
            {
                Name = "Jízdní řád", OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages, DesignerHeight = 30,
                Elements = [new LineTableElement { X = 0, Y = 0, Width = PageW - 2 * Margin,
                                                   DataStyle = new TextStyle { FontSize = 7 } }],
            },
            new Section
            {
                Name = "Patička", Role = SectionRole.Footer, PrintBehavior = SectionPrintBehavior.OnEveryPage,
                DesignerHeight = FooterH,
                Elements = [new TextElement { X = 0, Y = 2, Width = 150, Height = 5, Text = "Dopravce: ČSAD Kroměříž",
                                              Style = new TextStyle { FontSize = 8 } }],
            },
        ],
    };

    private static DataContext Data() => new(
        values: new Dictionary<string, object?> { ["CISLO_LINKY"] = "940" },
        rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["ZASTAVKY"] = [.. Zastavky.Select((n, i) => RenderSnapshot.Row(("NAZEV_ZASTAVKY", n), ("PORADI", (i + 1).ToString())))],
            ["SPOJE"] = [.. Enumerable.Range(1, 8).Select(s => RenderSnapshot.Row(
                ("CISLO_SPOJE", (s * 2 - 1).ToString()), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", new JsonArray([.. Zastavky.Select((_, i) => (JsonNode)$"{5 + s}:{i * 4:00}")]))))],
            ["SKUPINY_DNU"] = [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))],
        },
        culture: System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"));

    private static ExtractedPage RenderAndExtract()
    {
        var state = new DesignerState { CurrentTemplate = SourceTemplate() };
        var path = Path.Combine(Path.GetTempPath(), $"td-import-linkovy-{Guid.NewGuid():N}.pdf");
        try
        {
            new PreviewCanvas(state).RenderMultiDataToPdf(path, [Data()]);
            using var pdf = PdfDocument.Open(path);
            return PdfPageExtractor.Extract(pdf, 1);
        }
        finally { File.Delete(path); }
    }

    /// <summary>Odpověď modelu: oblast tabulky (změřená ze slov mezi hlavičkou a patičkou, jako by
    /// ji model vyznačil přesně) a jeden namapovaný nadpis. Patičku model „zapomněl".</summary>
    private static AiVisionResult Vision(ExtractedPage page)
    {
        float tableTop = Margin + HeaderH, footerTop = PageH - Margin - FooterH;
        var tableWords = page.Words.Where(w => w.Y >= tableTop - 1 && w.Y < footerTop - 1).ToList();
        float top = tableWords.Min(w => w.Y) - 1, bottom = tableWords.Max(w => w.Y + w.Height) + 1;

        return new AiVisionResult
        {
            Confidence = 0.9f,
            Elements =
            [
                new AiVisionElement { Type = "LineTable", HasHeader = true, Columns = ["StopName", "Tc"],
                    Left = Margin / PageW, Top = top / PageH, Right = (PageW - Margin) / PageW, Bottom = bottom / PageH },
            ],
            TextMappings =
            [
                new AiVisionTextMapping { Text = "Linka 940", Role = "title", DbField = "CISLO_LINKY",
                    PlaceholderText = "Linka {CISLO_LINKY}", Confidence = 0.95f,
                    X = Margin / PageW, Y = (Margin + 2) / PageH, W = 100 / PageW, H = 8 / PageH },
            ],
        };
    }

    [Fact]
    public void ZTabulkyNevzniknouVolneTextyAleTextyKolemZustanou()
    {
        var page = RenderAndExtract();
        var elements = AiGuidedScheduleBuilder.BuildFromVision(Vision(page), page, 0, 0);
        var texts = elements.OfType<TextElement>().Select(t => t.Text).ToList();
        output.WriteLine("Texty: " + string.Join(" | ", texts));

        Assert.Single(elements.OfType<LineTableElement>());
        Assert.Contains("Linka {CISLO_LINKY}", texts);
        Assert.Contains(texts, t => t.Contains("Dopravce"));
        Assert.DoesNotContain(texts, t => Zastavky.Any(t.Contains) && !t.Contains("Dopravce"));
        Assert.DoesNotContain(texts, t => Regex.IsMatch(t, @"^\d{1,2}([:\s]\d{2})?$"));
    }

    [Fact]
    public void SablonaMaHlavickuTabulkuAPaticku()
    {
        var page = RenderAndExtract();
        var elements = AiGuidedScheduleBuilder.BuildFromVision(Vision(page), page, 0, 0);

        var sections = AiVisionImportPipeline.SplitLineScheduleSections(elements);

        Assert.Equal(["Hlavička", "Jízdní řád", "Poznámky"], sections.Select(s => s.Name));
        Assert.Contains(sections[0].Elements, e => e is TextElement { Text: "Linka {CISLO_LINKY}" });
        Assert.IsType<LineTableElement>(Assert.Single(sections[1].Elements));
        Assert.Contains(sections[2].Elements, e => e is TextElement t && t.Text.Contains("Dopravce"));
    }

    [Fact]
    public void PismoTabulkySeZmeriZPdf()
    {
        var page = RenderAndExtract();
        var table = AiGuidedScheduleBuilder.BuildFromVision(Vision(page), page, 0, 0)
            .OfType<LineTableElement>().Single();
        output.WriteLine($"Data {table.DataStyle.FontSize} pt, záhlaví {table.HeaderStyle.FontSize} pt, "
                         + $"řádek {table.DataRowHeight} mm, sloupce {string.Join(",", table.Columns.Select(c => c.Key))}");

        Assert.Equal(7f, table.DataStyle.FontSize);
        Assert.Equal([ColumnKeys.StopName, ColumnKeys.Tc], table.Columns.Take(2).Select(c => c.Key));
    }
}
