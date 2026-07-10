using System.IO;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using UglyToad.PdfPig;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// End-to-end smoke test imposice: A5 šablona → RenderMultiDataToPdf se SheetLayoutOptions
/// → PDF ověřené přes PdfPig (počet stránek dokumentu = počet listů, rozměr = list, obsah přítomen).
/// </summary>
public class SheetPdfExportTests
{
    private const float PtPerMm = 72f / 25.4f;

    private static DesignerState A5State()
    {
        var tpl = new Template
        {
            Name = "test",
            PageSize = new PageSize { Width = 148, Height = 210 },
            Sections =
            [
                new Section
                {
                    Name = "Obsah",
                    DesignerHeight = 100,
                    Elements = [new TextElement { X = 5, Y = 5, Width = 100, Height = 10, Text = "Zastávka {NAZEV}" }]
                }
            ]
        };
        return new DesignerState { CurrentTemplate = tpl };
    }

    private static DataContext Data(string nazev) =>
        new(new Dictionary<string, object?> { ["NAZEV"] = nazev });

    [Fact]
    public void TriDatoveStranky_2up_DvaListyA4Nalezato()
    {
        var state = A5State();
        var canvas = new PreviewCanvas(state);
        var sheets = SheetComposer.FromParamValues(new Dictionary<string, object?>
        {
            [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE",
            [SheetLayoutKeys.Grid] = "AUTO",
            [SheetLayoutKeys.Marks] = "1",
        }, state.CurrentTemplate.PageSize);

        var path = Path.Combine(Path.GetTempPath(), $"td-imposice-{Guid.NewGuid():N}.pdf");
        try
        {
            canvas.RenderMultiDataToPdf(path, [Data("První"), Data("Druhá"), Data("Třetí")], sheetLayout: sheets);

            using var pdf = PdfDocument.Open(path);
            Assert.Equal(2, pdf.NumberOfPages);   // 3 logické stránky, 2 na list

            var page = pdf.GetPage(1);
            Assert.Equal(297f * PtPerMm, (float)page.Width, 0);    // list A4 naležato
            Assert.Equal(210f * PtPerMm, (float)page.Height, 0);
            Assert.Contains("První", page.Text);
            Assert.Contains("Druhá", page.Text);
            Assert.Contains("Třetí", pdf.GetPage(2).Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BezImposice_KazdaStrankaVlastniA5()
    {
        var state = A5State();
        var canvas = new PreviewCanvas(state);

        var path = Path.Combine(Path.GetTempPath(), $"td-plain-{Guid.NewGuid():N}.pdf");
        try
        {
            canvas.RenderMultiDataToPdf(path, [Data("První"), Data("Druhá")]);

            using var pdf = PdfDocument.Open(path);
            Assert.Equal(2, pdf.NumberOfPages);
            Assert.Equal(148f * PtPerMm, (float)pdf.GetPage(1).Width, 0);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Opakovani_KazdaStrankaNaCelyList()
    {
        var state = A5State();
        var canvas = new PreviewCanvas(state);
        var sheets = SheetComposer.FromParamValues(new Dictionary<string, object?>
        {
            [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE",
            [SheetLayoutKeys.Repeat] = "1",
        }, state.CurrentTemplate.PageSize);

        var path = Path.Combine(Path.GetTempPath(), $"td-repeat-{Guid.NewGuid():N}.pdf");
        try
        {
            canvas.RenderMultiDataToPdf(path, [Data("První"), Data("Druhá")], sheetLayout: sheets);

            using var pdf = PdfDocument.Open(path);
            Assert.Equal(2, pdf.NumberOfPages);   // 1 list per stránka (každá 2×)

            // Táž vývěska dvakrát na listu → text se vyskytuje 2×
            var text = pdf.GetPage(1).Text;
            Assert.Equal(2, text.Split("První").Length - 1);
        }
        finally { File.Delete(path); }
    }
}
