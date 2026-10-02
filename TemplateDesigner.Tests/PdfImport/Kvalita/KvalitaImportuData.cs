using System.Globalization;
using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.PdfImport.Kvalita;

/// <summary>
/// Data linky pro tisk předloh do měření kvality importu. Každý popisný sloupec má hodnotu, jinak by
/// se na papíře neobjevil (prázdný sloupec se netiskne) a import by ho neměl z čeho poznat.
/// </summary>
public static class KvalitaImportuData
{
    private static readonly string[] Zastavky =
    [
        "Kaplice,aut.st.", "Kaplice,Blansko", "Netřebice", "Velešín,žel.st.", "Velešín,nám.",
        "Holašovice", "Kamenný Újezd", "Plav", "Roudné", "České Budějovice,Mladé",
        "České Budějovice,Mánesova", "České Budějovice,aut.nádr.",
    ];

    public static DataContext Linka() => new(
        values: new Dictionary<string, object?>
        {
            ["CISLO_LINKY"] = "330530",
            ["POPIS_LINKY"] = "Kaplice - Velešín - České Budějovice",
            ["DATUM_PLATNOSTI_OD"] = "14.12.2025",
            ["DATUM_PLATNOSTI_DO"] = "12.12.2026",
            ["POZNAMKA_NAD_LINKU"] = "Linka IDS JK 530",
            ["TYP_JR"] = "Linkový jízdní řád",
            ["POPIS_DOPRAVCE"] = "ČSAD AUTOBUSY České Budějovice a.s., tel. 386 100 111",
            ["VYCHOZI_ZASTAVKA"] = "Kaplice,aut.st.",
            ["KONECNA_ZASTAVKA"] = "České Budějovice,aut.nádr.",
        },
        rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["ZASTAVKY"] = [.. Zastavky.Select((n, i) => RenderSnapshot.Row(
                ("NAZEV_ZASTAVKY", n), ("PORADI", (i + 1).ToString()), ("TC", (i + 1).ToString()),
                ("ZONY", i < 5 ? "530" : "520"), ("MHD", i >= 9 ? "MHD" : ""),
                ("KM", new JsonArray((JsonNode)(i * 3).ToString()))))],
            ["SPOJE"] = [.. Enumerable.Range(0, 11).Select(s => RenderSnapshot.Row(
                ("CISLO_SPOJE", (s * 2 + 1).ToString()), ("SKUPINA", s < 8 ? "1" : "2"), ("SMER", "T"),
                ("PLATNOST", s < 8 ? new JsonArray("X") : new JsonArray("6", "+")),
                ("CASY", new JsonArray([.. Zastavky.Select((_, i) => (JsonNode)$"{5 + s}:{(i * 4) % 60:00}")]))))],
            ["SKUPINY_DNU"] =
            [
                RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Soboty, neděle a svátky"), ("PORADI", "2")),
            ],
            ["POZNAMKY"] =
            [
                RenderSnapshot.Row(("TYP", "nz"), ("ZNAK", "X"), ("TEXT", "jede v pracovních dnech")),
                RenderSnapshot.Row(("TYP", "nz"), ("ZNAK", "6"), ("TEXT", "jede v sobotu")),
                RenderSnapshot.Row(("TYP", "nz"), ("ZNAK", "+"), ("TEXT", "jede v neděli a ve státem uznané svátky")),
                RenderSnapshot.Row(("TYP", "znak"), ("ZNAK", "x"), ("TEXT", "zastávka s možností přestupu na železniční dopravu")),
                RenderSnapshot.Row(("TYP", "text"), ("ZNAK", ""), ("TEXT", "Na lince platí Tarif Jihočeského kraje.")),
            ],
        },
        culture: CultureInfo.GetCultureInfo("cs-CZ"));
}
