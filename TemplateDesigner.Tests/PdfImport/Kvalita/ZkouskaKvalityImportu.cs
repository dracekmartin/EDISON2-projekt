using System.IO;
using System.Text;
using TemplateDesigner.Designer;
using TemplateDesigner.PdfImport;
using TemplateDesigner.Serialization;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.PdfImport.Kvalita;

/// <summary>
/// Měření kvality importu linkového JŘ naostro nad sadou předloh: náš tisk ze známých šablon
/// (očekávání se odvodí ze zdroje) a skutečná PDF s ručně zapsaným očekáváním
/// (<c>sada/&lt;jméno&gt;.ocekavani.json</c>, PDF leží mimo repo). Výsledek je přehled
/// <c>kvalita.md</c> - kolik kontrol prošlo a které ne - a u každého běhu šablona a výpis modelu.
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_KVALITA_VYSTUP</c> nic nedělá. Každý běh je placené volání modelu.
/// <code>
/// TD_KVALITA_VYSTUP=slozka [TD_KVALITA_BEHY=1] [TD_KVALITA_SADA=slozka-s-pdf] [TD_KVALITA_JEN=940]
/// dotnet test TemplateDesigner.Tests --filter ZkouskaKvalityImportu
/// </code>
/// </remarks>
public class ZkouskaKvalityImportu(ITestOutputHelper output)
{
    private const string Agenda = "LINKOVY_JR_AD";

    /// <summary>Naše šablony, jejichž výtisk se importuje - jméno případu × cesta v repu.</summary>
    private static readonly (string Nazev, string Soubor)[] NaseTisky =
    [
        ("tisk-postskript-a5", "TemplateDesigner/templates/linkovy-jr-ad-postskript-a5.json"),
        ("tisk-poznamky-vedle-a4", "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-poznamky-vedle-a4.json"),
        ("tisk-vzor-940", "TemplateDesigner/templates/vzor-940-idzk.json"),
    ];

    [Fact]
    public async Task Zkouska()
    {
        string? vystup = Environment.GetEnvironmentVariable("TD_KVALITA_VYSTUP");
        if (string.IsNullOrEmpty(vystup)) return;
        int behy = int.TryParse(Environment.GetEnvironmentVariable("TD_KVALITA_BEHY"), out var b) ? b : 1;
        string? jen = Environment.GetEnvironmentVariable("TD_KVALITA_JEN");
        string? sada = Environment.GetEnvironmentVariable("TD_KVALITA_SADA");
        Directory.CreateDirectory(vystup);

        var pripady = new List<(string Nazev, string Pdf, OcekavaniImportu O)>();
        foreach (var (nazev, soubor) in NaseTisky)
        {
            var sablona = TemplateSerializer.Deserialize(File.ReadAllText(Path.Combine(RepoRoot(), soubor)))!;
            var data = KvalitaImportuData.Linka();
            string slozka = Directory.CreateDirectory(Path.Combine(vystup, nazev)).FullName;
            string pdf = Path.Combine(slozka, "predloha.pdf");
            new PreviewCanvas(new DesignerState { CurrentTemplate = sablona }).RenderMultiDataToPdf(pdf, [data]);
            ExtractedPage vytisk;
            using (var doc = UglyToad.PdfPig.PdfDocument.Open(pdf)) vytisk = PdfPageExtractor.Extract(doc, 1);
            var o = KvalitaImportu.ZeSablony(sablona, data, vytisk, nazev);
            File.WriteAllText(Path.Combine(slozka, "ocekavani.json"), KvalitaImportu.DoJson(o));
            pripady.Add((nazev, pdf, o));
        }
        if (!string.IsNullOrEmpty(sada))
            foreach (var json in Directory.GetFiles(SadaDir(), "*.ocekavani.json"))
            {
                string nazev = Path.GetFileName(json).Replace(".ocekavani.json", "");
                string pdf = Path.Combine(sada, nazev + ".pdf");
                if (File.Exists(pdf)) pripady.Add((nazev, pdf, KvalitaImportu.Nacti(json)));
                else output.WriteLine($"Předloha {nazev}: PDF {pdf} chybí, přeskočena");
            }

        var prehled = new StringBuilder()
            .AppendLine($"# Kvalita importu linkového JŘ - {DateTime.Now:d. M. yyyy H:mm}")
            .AppendLine()
            .AppendLine("| Předloha | Běh | Kontroly | Odchylka textů | Neprošlo |")
            .AppendLine("|---|---|---|---|---|");

        foreach (var (nazev, pdf, o) in pripady.Where(p => jen is null || p.Nazev.Contains(jen, StringComparison.OrdinalIgnoreCase)))
            for (int beh = 1; beh <= behy; beh++)
            {
                string slozka = Directory.CreateDirectory(Path.Combine(vystup, nazev, $"beh{beh}")).FullName;
                Hodnoceni h;
                try
                {
                    var (sablona, pipeline) = await ImportNaostro.ImportujAsync(pdf, Agenda, 1);
                    File.WriteAllText(Path.Combine(slozka, "sablona.json"), TemplateSerializer.Serialize(sablona));
                    File.WriteAllText(Path.Combine(slozka, "ai-debug.txt"), pipeline.AiDebugLog ?? "");
                    File.WriteAllLines(Path.Combine(slozka, "protokol.txt"), pipeline.ImportLog);
                    h = KvalitaImportu.Vyhodnot(sablona, o);
                }
                catch (Exception ex)
                {
                    h = new Hodnoceni([new Kontrola("Import", "proběhl", false, ex.Message)], []);
                }

                File.WriteAllLines(Path.Combine(slozka, "kontroly.txt"),
                    h.Kontroly.Select(k => $"{(k.Ok ? "OK " : "-- ")} {k.Oblast,-9} {k.Nazev}  {k.Detail}"));
                string neproslo = string.Join("; ", h.Kontroly.Where(k => !k.Ok).Select(k => $"{k.Nazev} ({k.Detail})"));
                string odchylka = h.PrumernaOdchylkaTextuMm is float d ? $"{d:0.0} mm" : "-";
                prehled.AppendLine($"| {nazev} | {beh} | {h.Splneno}/{h.Kontroly.Count} | {odchylka} | {neproslo.Replace("|", "/")} |");
                output.WriteLine($"{nazev} běh {beh}: {h.Splneno}/{h.Kontroly.Count}, odchylka textů {odchylka}");
            }

        File.WriteAllText(Path.Combine(vystup, "kvalita.md"), prehled.ToString());
    }

    private static string SadaDir() => Path.Combine(RepoRoot(), "TemplateDesigner.Tests", "PdfImport", "Kvalita", "sada");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Kořen repa (EDISON2.sln) nenalezen.");
    }
}
