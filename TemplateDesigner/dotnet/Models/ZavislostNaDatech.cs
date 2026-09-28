using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Models;

/// <summary>
/// Změní přepnutí stránky dat (zastávka, směr, linka) to, co se vytiskne? Když ne, nabídka nad
/// náhledem jen mate - uživatel přepíná a nic se neděje (nález Jan 28. 9. u stazky pod agendou
/// linkového JŘ).
/// </summary>
/// <remarks>
/// Pochybnost se vždy rozhodne pro „mění": nabídka navíc je menší škoda než nabídka, která chybí.
/// Proto se každý prvek mimo čistě statické typy (text, tvar, obrázek, kód, pevná tabulka) bere
/// jako plněný z dat, a stejně tak cokoli ve složených závorkách, co není prosté pole.
/// </remarks>
public static class ZavislostNaDatech
{
    private static readonly Regex Zavorka = new(@"\{([^{}]*)\}", RegexOptions.Compiled);
    private static readonly Regex ProstePole = new(@"^(\w+)(?::[^}]*)?$", RegexOptions.Compiled);

    public static bool PrepinaniMaSmysl(Template template, IReadOnlyList<DataContext> stranky)
    {
        if (stranky.Count == 0) return false;
        if (template.Sections.Any(s => s.PrintBehavior == SectionPrintBehavior.PerDataRow)) return true;
        if (template.Sections.SelectMany(s => s.Elements).Any(e => !JeStaticky(e))) return true;
        if (stranky.Count < 2) return false;

        var pole = new HashSet<string>();
        foreach (var text in Texty(template))
            foreach (Match m in Zavorka.Matches(text))
            {
                if (ProstePole.Match(m.Groups[1].Value) is not { Success: true } p) return true;
                pole.Add(p.Groups[1].Value);
            }

        return pole.Any(nazev => stranky.Select(s => s.GetString(nazev)).Distinct().Skip(1).Any());
    }

    private static bool JeStaticky(TemplateElement e) => e switch
    {
        TableElement t => t.Mode == TableMode.Fixed,
        TextElement or RectangleElement or LineElement or ImageElement or BarcodeElement => true,
        _ => false,
    };

    /// <summary>Všechny textové hodnoty v sekcích šablony - kdekoli může být pole ve závorkách
    /// (text, buňka, podmínka viditelnosti, obsah kódu, cesta k obrázku).</summary>
    private static IEnumerable<string> Texty(Template template)
    {
        var json = JsonNode.Parse(TemplateSerializer.Serialize(new Template { Sections = template.Sections }));
        var zasobnik = new Stack<JsonNode?>([json]);
        while (zasobnik.Count > 0)
            switch (zasobnik.Pop())
            {
                case JsonObject o: foreach (var (_, v) in o) zasobnik.Push(v); break;
                case JsonArray a: foreach (var v in a) zasobnik.Push(v); break;
                case JsonValue v when v.TryGetValue<string>(out var s) && s.Contains('{'): yield return s; break;
            }
    }
}
