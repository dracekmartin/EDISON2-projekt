using System.IO;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Serialization;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Ruční zkouška importu z PDF naostro - dev server, skutečný model, bez otevírání návrháře.
/// Do výstupní složky uloží obrázek stránky, který dostal model (<c>stranka.png</c>), celý výpis
/// s odpovědí modelu (<c>ai-debug.txt</c>), protokol (<c>protokol.txt</c>) a šablonu
/// (<c>sablona.json</c>, v návrháři Importovat JSON). Volba Debug v dialogu je jen s lokálním
/// klíčem, tohle jde i s klíčem z dev serveru.
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_ZKOUSKA_IMPORTU_PDF</c> nic nedělá (pravidlo testů závislých na stroji).
/// <code>
/// TD_ZKOUSKA_IMPORTU_PDF=cesta.pdf TD_ZKOUSKA_VYSTUP=slozka [TD_ZKOUSKA_AGENDA=LINKOVY_JR_AD]
/// [TD_ZKOUSKA_STRANA=1] [TD_ZKOUSKA_JEN_OBRAZEK=1] dotnet test TemplateDesigner.Tests --filter ZkouskaImportuNaostro
/// </code>
/// Připojení bere z <c>templatedesigner.appsettings.local.json</c> (VPN). Každý běh je jedno
/// placené volání modelu a odpovědi se mezi běhy liší.
/// </remarks>
public class ZkouskaImportuNaostro(ITestOutputHelper output)
{
    [Fact]
    public async Task Zkouska()
    {
        string? pdf = Environment.GetEnvironmentVariable("TD_ZKOUSKA_IMPORTU_PDF");
        if (string.IsNullOrEmpty(pdf)) return;
        string outDir = Environment.GetEnvironmentVariable("TD_ZKOUSKA_VYSTUP")!;
        string agenda = Environment.GetEnvironmentVariable("TD_ZKOUSKA_AGENDA") ?? "LINKOVY_JR_AD";
        int strana = int.TryParse(Environment.GetEnvironmentVariable("TD_ZKOUSKA_STRANA"), out var s) ? s : 1;
        Directory.CreateDirectory(outDir);
        using (var doc = UglyToad.PdfPig.PdfDocument.Open(pdf))
        {
            output.WriteLine($"PDF: {doc.NumberOfPages} stran");
            var page = PdfPageExtractor.Extract(doc, strana);
            File.WriteAllBytes(Path.Combine(outDir, "stranka.png"),
                Convert.FromBase64String(PdfPageRasterizer.RasterizeToBase64Png(page)));
            // Rozbor stránky z PDF - podklad pro ručně psané očekávání (sada/*.ocekavani.json).
            File.WriteAllLines(Path.Combine(outDir, "rozbor.txt"),
            [
                $"Stránka {page.WidthMm:0.0} × {page.HeightMm:0.0} mm",
                "", "Bloky textu (x, y, šířka × výška mm, písmo, barva):",
                .. PdfTextGrouper.GroupWords(page.Words).OrderBy(b => b.Y).ThenBy(b => b.X).Select(b =>
                    $"  {b.X,6:0.0} {b.Y,6:0.0} {b.Width,6:0.0}×{b.Height,-5:0.0} {b.FontSizePt,4:0.0}pt {b.ColorHex}  {b.Text.Replace('\n', '⏎')}"),
                "", "Barevné plochy (x, y, šířka × výška mm, výplň):",
                .. page.Rectangles.Where(r => r.FillColorHex is not null && r.Width >= 5).OrderBy(r => r.Y).ThenBy(r => r.X).Select(r =>
                    $"  {r.X,6:0.0} {r.Y,6:0.0} {r.Width,6:0.0}×{r.Height:0.0} {r.FillColorHex}"),
            ]);
        }
        if (Environment.GetEnvironmentVariable("TD_ZKOUSKA_JEN_OBRAZEK") == "1") return;   // bez volání modelu

        var (template, pipeline) = await Kvalita.ImportNaostro.ImportujAsync(pdf, agenda, strana,
            st => output.WriteLine("  " + st));

        File.WriteAllText(Path.Combine(outDir, "ai-debug.txt"), pipeline.AiDebugLog ?? "");
        File.WriteAllLines(Path.Combine(outDir, "protokol.txt"), [.. pipeline.ImportLog, "", .. pipeline.DetailedLog]);
        File.WriteAllText(Path.Combine(outDir, "sablona.json"), TemplateSerializer.Serialize(template));

        output.WriteLine($"Stránka {template.PageSize.Width} × {template.PageSize.Height} mm");
        foreach (var sec in template.Sections)
        {
            output.WriteLine($"Sekce {sec.Name}: {sec.Role}, {sec.PrintBehavior}, {sec.OverflowBehavior}, výška {sec.DesignerHeight}");
            foreach (var el in sec.Elements)
                output.WriteLine($"   {el.GetType().Name} {el.X:0.#},{el.Y:0.#} {el.Width:0.#}×{el.Height?.ToString("0.#") ?? "auto"}"
                                 + (el is TemplateDesigner.Models.Elements.TextElement t ? $"  \"{t.Text}\"" : ""));
        }
        foreach (var l in pipeline.ImportLog) output.WriteLine("LOG " + l);
    }
}
