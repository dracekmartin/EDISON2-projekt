using System.Diagnostics;
using System.IO;
using SkiaSharp;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;
using TemplateDesigner.Tests.PdfImport.Kvalita;
using Xunit.Abstractions;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Ruční měření náhledu nad skutečnými daty z dev serveru: kolik trvá načtení dat, rozložení
/// na stránky a jedno překreslení náhledu, jak ho dělá návrhář, a kolik to zabere paměti.
/// </summary>
/// <remarks>
/// Bez proměnné <c>TD_VYKON</c> nic nedělá. Připojení z <c>templatedesigner.appsettings.local.json</c> (VPN).
/// <code>
/// TD_VYKON=1 TD_IDFA=KV_DP TD_NAMESPACE=KV_DP TD_VYKON_LINKY=1096,1073
/// [TD_VYKON_SABLONA=cesta.json] dotnet test TemplateDesigner.Tests --filter VykonNahleduNaostro
/// </code>
/// </remarks>
public class VykonNahleduNaostro(ITestOutputHelper output)
{
    [Fact]
    public async Task Mereni()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TD_VYKON"))) return;
        var linky = (Environment.GetEnvironmentVariable("TD_VYKON_LINKY") ?? "1096")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string soubor = Environment.GetEnvironmentVariable("TD_VYKON_SABLONA")
            ?? "TemplateDesigner/templates/linkovy-jr-mhd-postskript-a5.json";
        string cesta = Path.IsPathRooted(soubor) ? soubor : Path.Combine(RepoRoot(), soubor);

        var (provider, _) = await ImportNaostro.PripojAsync();
        var sablona = TemplateSerializer.Deserialize(File.ReadAllText(cesta))!;
        string agenda = sablona.AgendaCode ?? "LINKOVY_JR_MHD";
        var parametry = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in provider.GetParameters(agenda)) parametry[p.Name] = p.DefaultValue;
        foreach (var (k, v) in sablona.PrintParameters ?? []) parametry[k] = v;

        foreach (var linka in linky)
        {
            var sw = Stopwatch.StartNew();
            var stranky = provider.GetData(agenda, TemplateDesignerViewModel.ComposeGetDataParams(
                parametry, "LINKA_ID", linka, null, null, false, "cs"));
            long nacteni = sw.ElapsedMilliseconds;
            if (stranky is null || stranky.Count == 0) { output.WriteLine($"{linka}: bez dat"); continue; }
            var data = stranky[0];

            var canvas = new PreviewCanvas(new DesignerState { CurrentTemplate = sablona });
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long pamet0 = GC.GetTotalMemory(true);
            long alokace0 = GC.GetTotalAllocatedBytes(true);

            sw.Restart();
            var hlaseni = canvas.HlaseniSazby([data]);
            long sazba = sw.ElapsedMilliseconds;

            sw.Restart();
            var uzke = TripColumnWidthWarnings.Seznam(sablona, [data]);
            long uzkeMs = sw.ElapsedMilliseconds;

            var casy = new List<long>();
            using var surface = SKSurface.Create(new SKImageInfo(1400, 900));
            for (int i = 0; i < 3; i++)
            {
                sw.Restart();
                canvas.Render(surface.Canvas, 1400, 900, data);
                casy.Add(sw.ElapsedMilliseconds);
            }
            long alokace = GC.GetTotalAllocatedBytes(true) - alokace0;
            long pamet = GC.GetTotalMemory(false) - pamet0;

            output.WriteLine($"{linka}: data {nacteni} ms ({stranky.Count} stran dat, {stranky.Sum(s => s.GetRows("SPOJE").Count())} spojů)");
            output.WriteLine($"   hlášky sazby (rozložení) {sazba} ms, úzké sloupce {uzkeMs} ms, překreslení náhledu {string.Join(" / ", casy)} ms");
            output.WriteLine($"   alokováno {alokace / 1024 / 1024} MB, drženo navíc {pamet / 1024 / 1024} MB, hlášek {hlaseni.Count}, úzkých {uzke.Count}");
            output.WriteLine($"   stran sazby (první strana dat) {canvas.CountOverflowingPages([data]).Total}");

            // Krok zoomu v návrháři: výška plochy (sazba) + překreslení celé plochy v té velikosti.
            // „nová data" = jako dnes návrhář (data se skládají znovu, tabulka spoje čte znovu).
            foreach (float zoom in new[] { 1f, 2f, 3f })
            {
                sw.Restart();
                float vyska = canvas.CalculatePageHeightPx(zoom, data);
                long vyskaMs = sw.ElapsedMilliseconds;
                int sirka = (int)canvas.ComputePageWidthPx(zoom);
                using var plocha = SKSurface.Create(new SKImageInfo(sirka, (int)vyska));
                sw.Restart();
                canvas.Render(plocha.Canvas, sirka, vyska, data);
                long kresleniMs = sw.ElapsedMilliseconds;
                var noveData = data.WithCulture(data.Culture);
                sw.Restart();
                canvas.CalculatePageHeightPx(zoom, noveData);
                canvas.Render(plocha.Canvas, sirka, vyska, noveData);
                long noveMs = sw.ElapsedMilliseconds;
                output.WriteLine($"   zoom {zoom * 100:0} % ({sirka}x{(int)vyska} px): výška plochy {vyskaMs} ms, kreslení {kresleniMs} ms, krok s novými daty {noveMs} ms");
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
