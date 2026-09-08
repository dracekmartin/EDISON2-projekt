using System.IO;
using ALWA.SYS;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;
using Xunit;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Sonda pro porovnání se starým postskriptovým tiskem: načte šablonu ze souboru, stáhne reálná
/// data linky z Caché a vyrenderuje PNG. Slouží k ladění šablony proti výtisku starého modulu.
///
/// ⚠️ **Defaultně vypnutá** - běží jen s <c>TD_PS_PROBE=1</c>, protože sahá na dev server
/// (VPN) a zapisuje mimo repozitář. Bez proměnné se přeskočí, takže sada zůstane strojově
/// nezávislá (architecture-rules must-not-happen #6).
///
/// Vstupy proměnnými prostředí: <c>TD_PS_TEMPLATE</c> (cesta k JSON šablony),
/// <c>TD_PS_OUT</c> (cílové PNG), <c>TD_PS_LINKA</c>, <c>TD_PS_SKUPINA</c>, <c>TD_PS_IDFA</c>,
/// <c>TD_PS_DPI</c>, <c>TD_PS_OBOUSMERNE</c>.
/// </summary>
public class PostScriptParitaProbe
{
    [Fact]
    public void VyrenderujLinkuDoPng()
    {
        if (Environment.GetEnvironmentVariable("TD_PS_PROBE") != "1") return;

        string templatePath = Env("TD_PS_TEMPLATE");
        string outPath      = Env("TD_PS_OUT");
        string idfa         = Env("TD_PS_IDFA", "CB_CSADAD");
        string skupina      = Env("TD_PS_SKUPINA", "1");
        string linka        = Env("TD_PS_LINKA", "330511");
        string obousmerne   = Env("TD_PS_OBOUSMERNE", "true");
        float  dpi          = float.Parse(Env("TD_PS_DPI", "200"));

        var template = TemplateSerializer.Deserialize(File.ReadAllText(templatePath));

        var mlRest = new MLRest("192.168.16.1", 57770)
        {
            ClassNamespace = "MLINE",
            DataNamespace  = idfa,
            Idfa           = idfa,
            LoginName      = "david.zeman",
            FullName       = "David Zeman",
            UserID         = "david.zeman",
            Mlp            = "david.zeman",
            Locale         = "cs_CZ",
            EdisonVersion  = "PROBE-1.0"
        };

        var inVar = new CVar();
        inVar["SKUPINA_LINEK_ID"]  = new CVar(skupina);
        inVar["LINKA_ID"]          = new CVar(linka);
        inVar["OBOUSMERNY_TISK"]   = new CVar(obousmerne);
        inVar["DELENI_SLOUPCU"]    = new CVar(Env("TD_PS_DELENI", "Pracovní dny / Soboty / Neděle+svátky"));
        inVar["TVAR_NAZVU_ZASTAVKY"] = new CVar("Plný CIS");

        var result = mlRest.Query("ALVA.TemplateDesigner.Agendy.LinkovyJrAd", "GetData", inVar);
        Assert.False(mlRest.RestStatus.State == MLRStatus.EState.Error, mlRest.RestStatus.Message);

        var pages = CacheAgendaProvider.ParsePages(
            result.Select(kvp => (kvp.Key, kvp.Value?.Value?.ToString())));
        Assert.NotNull(pages);

        var state = new DesignerState { CurrentTemplate = template };
        var canvas = new PreviewCanvas(state);
        int count = canvas.RenderMultiDataToPng(outPath, pages!, dpi, template);

        // Rozvržení tabulky linky - kolik spojů se vešlo do bloku a jak široký sloupec z toho vyšel.
        var popisBloku = "";
        foreach (var el in template.Sections.SelectMany(x => x.Elements)
                     .OfType<TemplateDesigner.Models.Elements.LineTableElement>())
        {
            var bloky = el.Rozvrzeni(pages![0]);
            popisBloku += $"prvek {el.Width} mm → {bloky.Count} bloků: "
                + string.Join(" | ", bloky.Select(b =>
                    $"{b.Pocet} slotů, {b.SirkaMm:0.0} mm, sloupec "
                    + $"{(b.SirkySloupcu.Count > 0 ? b.SirkySloupcu[0] : 0):0.00} mm"
                    + $" (základ {b.SloupecSpojeMm:0.00})")) + "\n";
        }

        // Kde se sekce reálně rozřízla proti tomu, kde to prvek dovolil - hledá se řez uvnitř bloku.
        var layout = new TemplateDesigner.Models.Layout.PageLayoutEngine(
            new TemplateDesigner.Models.Layout.SkiaHeightCalculator()).Layout(template, pages![0]);
        foreach (var el in template.Sections.SelectMany(x => x.Elements)
                     .OfType<TemplateDesigner.Models.Elements.LineTableElement>())
        {
            var zl = el.ZlomyMm(pages![0]);
            popisBloku += "vyska prvku " + el.CalculateHeightFromData(pages![0]).ToString("0.0")
                + " mm, silne zlomy: "
                + string.Join(", ", zl.Where(z => z.Silny).Select(z => z.Mm.ToString("0.0"))) + "\n";
        }
        foreach (var pg in layout.Pages)
            foreach (var ps2 in pg.Sections.Where(x => x.ClipTop is not null))
                popisBloku += $"str. {pg.PageNumber} sekce {ps2.Source.Name}: rez {ps2.ClipTop:0.0} → "
                    + $"{ps2.ClipBottom:0.0} (opakovana hlavicka {ps2.OpakovanaHlavickaMm:0.0})\n";

        var (nalezy, total) = canvas.CountOverflowingPages(pages!);
        File.WriteAllText(Path.ChangeExtension(outPath, ".txt"),
            $"stránek: {count} (layout {total})\n" + popisBloku +
            string.Join("\n", nalezy.Select(n => $"přetečení str. {n.CisloStranky}: {n.Druh} sekce={n.Sekce} prvek={n.Prvek} presah={n.PresahMm:0.0}mm")));

        Assert.True(count > 0, "Nevznikla ani jedna stránka");
    }

    private static string Env(string name, string? fallback = null) =>
        Environment.GetEnvironmentVariable(name)
        ?? fallback
        ?? throw new InvalidOperationException($"Chybí proměnná prostředí {name}");
}
