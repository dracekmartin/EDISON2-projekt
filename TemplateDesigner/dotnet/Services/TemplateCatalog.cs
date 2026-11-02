using System.Text.Json.Nodes;

namespace TemplateDesigner.Services;

/// <summary>
/// Strom šablon firmy podle oblasti práce, agendy a varianty (`API.StromSablon`, `B190`). Skládá ho server
/// z registru agend a nic v něm uloženého není; okno šablon podle něj filtruje seznam a ukazuje výchozí šablonu
/// pro tisk. „Strom šablony" (sekce a prvky jedné šablony) je jiná věc.
/// </summary>
public sealed record TemplateCatalog(IReadOnlyList<CatalogArea> Areas)
{
    public const string CacheClass = "ALVA.TemplateDesigner.API.StromSablon";

    public static readonly TemplateCatalog Empty = new([]);

    /// <summary>OUT[1] <c>GetStrom</c>; prázdný nebo neplatný JSON = prázdný strom.</summary>
    public static TemplateCatalog Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (System.Text.Json.JsonException) { return Empty; }

        var areas = new List<CatalogArea>();
        foreach (var area in Items(root?["oblasti"]))
        {
            var agendas = new List<CatalogAgenda>();
            foreach (var agenda in Items(area["agendy"]))
            {
                var variants = new List<CatalogVariant>();
                foreach (var variant in Items(agenda["varianty"]))
                    variants.Add(new CatalogVariant(Text(variant, "code"), Text(variant, "label"),
                        int.TryParse(Text(variant, "pocet"), out var count) ? count : 0, ParseDefault(variant["vychozi"]),
                        Text(variant, "tiskne") == "true"));
                agendas.Add(new CatalogAgenda(Text(agenda, "code"), Text(agenda, "label"), variants));
            }
            areas.Add(new CatalogArea(Text(area, "code"), Text(area, "label"), agendas));
        }
        return new TemplateCatalog(areas);
    }

    private static DefaultPrintTemplate? ParseDefault(JsonNode? node) =>
        node is not null && Guid.TryParse(Text(node, "templateId"), out var id)
            ? new DefaultPrintTemplate(id, Text(node, "name"), Text(node, "zdroj") == "firma")
            : null;

    private static IEnumerable<JsonNode> Items(JsonNode? array) =>
        array is JsonArray items ? items.OfType<JsonNode>() : [];

    private static string Text(JsonNode node, string key) => node[key]?.ToString() ?? "";
}

/// <summary>Oblast práce (Jízdní řády, Mzdy…).</summary>
public sealed record CatalogArea(string Code, string Label, IReadOnlyList<CatalogAgenda> Agendas);

/// <summary>Agenda = druh výstupu (Linkový JŘ, Výplatní páska).</summary>
public sealed record CatalogAgenda(string Code, string Label, IReadOnlyList<CatalogVariant> Variants);

/// <summary>Varianta = zdroj dat; <see cref="Code"/> je kód agendy šablon (<c>LINKOVY_JR_AD</c>). Prázdný
/// <see cref="Label"/> = agenda bez variant. <see cref="Count"/> = šablony, které firma vidí (vlastní i M-line).
/// <see cref="CanPrint"/> = agenda se tiskne z modulu a smí mít výchozí šablonu pro tisk (Obecná bez dat ne).</summary>
public sealed record CatalogVariant(string Code, string Label, int Count, DefaultPrintTemplate? Default, bool CanPrint);

/// <summary>Šablona, kterou tisk agendy vezme bez volby volajícího: výchozí firmy, jinak M-line.</summary>
public sealed record DefaultPrintTemplate(Guid TemplateId, string Name, bool IsCompanyDefault);
