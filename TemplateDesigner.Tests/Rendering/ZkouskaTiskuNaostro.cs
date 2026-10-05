using System.IO;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;
using TemplateDesigner.Tests.PdfImport.Kvalita;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Ruční zkouška tisku naostro - šablony z repa se skutečnými daty linek z dev serveru, bez
/// návrháře. Uloží PDF, obrázky stránek a hlášky sazby, ať jde výstup prohlédnout (např. před
/// ukázkou zákazníkovi). Parametry sestavy jako v návrháři: výchozí z agendy, přes ně uložené
/// v šabloně.
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_TISK_VYSTUP</c> nic nedělá. Připojení z <c>templatedesigner.appsettings.local.json</c> (VPN).
/// <code>
/// TD_TISK_VYSTUP=slozka [TD_TISK_LINKY=330530,330510] [TD_TISK_SABLONY=cesta1.json,cesta2.json]
/// [TD_TISK_STRAN=2] [TD_TISK_DPI=110] dotnet test TemplateDesigner.Tests --filter ZkouskaTiskuNaostro
/// </code>
/// </remarks>
public class ZkouskaTiskuNaostro(ITestOutputHelper output)
{
    private static readonly string[] VychoziSablony =
    [
        "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-postskript-a5.json",
        "TemplateDesigner/dotnet/SystemTemplates/linkovy-jr-ad-poznamky-vedle-a4.json",
    ];

    [Fact]
    public async Task Zkouska()
    {
        string? vystup = Environment.GetEnvironmentVariable("TD_TISK_VYSTUP");
        if (string.IsNullOrEmpty(vystup)) return;
        var linky = (Environment.GetEnvironmentVariable("TD_TISK_LINKY") ?? "330530")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sablony = Environment.GetEnvironmentVariable("TD_TISK_SABLONY")?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? VychoziSablony;
        int stran = int.TryParse(Environment.GetEnvironmentVariable("TD_TISK_STRAN"), out var s) ? s : 2;
        float dpi = float.TryParse(Environment.GetEnvironmentVariable("TD_TISK_DPI"), out var d) ? d : 110f;
        Directory.CreateDirectory(vystup);

        var (provider, _) = await ImportNaostro.PripojAsync();
        if (linky is ["?"])
        {
            // Jen vypsat linky, které firma z připojení má (jako výběr instancí v návrháři).
            var mlRest = EDControls.EDViewModel.EDServices.Current.RequireMLRest;
            foreach (var agenda in new[] { "LINKOVY_JR_AD", "ZASTAVKOVY_JR_AD" })
            {
                var cv = new ALWA.SYS.CVar();
                cv["search"] = new ALWA.SYS.CVar("");
                cv["limit"] = new ALWA.SYS.CVar("40");
                var res = mlRest.QueryOrThrow($"ALVA.TemplateDesigner.Agendy.{(agenda == "LINKOVY_JR_AD" ? "LinkovyJrAd" : "ZastavkoveJrAd")}",
                    "GetAvailableInstances", cv);
                output.WriteLine($"{agenda}: {res["1"]?.Value}");
            }
            return;
        }
        foreach (var soubor in sablony)
        {
            string cesta = Path.IsPathRooted(soubor) ? soubor : Path.Combine(RepoRoot(), soubor);
            var sablona = TemplateSerializer.Deserialize(File.ReadAllText(cesta))!;
            string agenda = sablona.AgendaCode ?? "LINKOVY_JR_AD";
            const string instance = "LINKA_ID";   // všechny agendy JŘ (zastávkové i linkové)

            var parametry = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in provider.GetParameters(agenda)) parametry[p.Name] = p.DefaultValue;
            foreach (var (k, v) in sablona.PrintParameters ?? []) parametry[k] = v;
            string? sloupce = sablona.Sections.SelectMany(x => x.Elements).OfType<IColumnLayoutOwner>()
                .Select(e => e.BuildColumnsDefinitionWireJson()).FirstOrDefault(j => j is not null);
            bool turnusy = sablona.Sections.SelectMany(x => x.Elements)
                .OfType<LineTableElement>().Any(e => e.TurnusyKeSpojum);

            foreach (var linka in linky)
            {
                string jmeno = $"{Path.GetFileNameWithoutExtension(cesta)}-{linka}";
                var stranky = provider.GetData(agenda, TemplateDesignerViewModel.ComposeGetDataParams(
                    parametry, instance, linka, null, sloupce, turnusy, "cs"));
                if (stranky is null || stranky.Count == 0)
                {
                    output.WriteLine($"{jmeno}: linka nemá data");
                    continue;
                }

                var canvas = new PreviewCanvas(new DesignerState { CurrentTemplate = sablona });
                var vyber = stranky.Take(stran).ToList();
                canvas.RenderMultiDataToPdf(Path.Combine(vystup, jmeno + ".pdf"), vyber);
                int obrazku = canvas.RenderMultiDataToPng(Path.Combine(vystup, jmeno + ".png"), vyber, dpi: dpi);
                var hlaseni = canvas.HlaseniSazby(vyber);
                File.WriteAllLines(Path.Combine(vystup, jmeno + ".hlaseni.txt"),
                    hlaseni.Select(h => $"{h.Zavaznost}: {h.Kde} · {h.Co}{(h.Rada is null ? "" : " → " + h.Rada)}"));
                output.WriteLine($"{jmeno}: {stranky.Count} stran dat, vykresleno {obrazku} obrázků, hlášky {hlaseni.Count}");
                foreach (var h in hlaseni) output.WriteLine($"   {h.Zavaznost}: {h.Kde} · {h.Co}");
            }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Kořen repa (EDISON2.sln) nenalezen.");
    }
}
