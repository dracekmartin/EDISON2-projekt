using System.Text.Json.Nodes;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Models;

/// <summary>
/// Fonty, které šablona chce, ale na tomhle počítači nejsou. <see cref="FontResolver"/> je tiše
/// nahradí (embedded → OS → <b>Arial</b> → Skia default), takže výstup vypadá v pořádku a jen je
/// jiný, než autor zamýšlel - a na dvou počítačích jiný navzájem. Šablony se přitom sdílejí přes
/// databázi, takže „font, který mám já" a „font, který má kolega" nejsou totéž.
///
/// <para><b>Proč se hledá v serializované šabloně, ne v properties:</b> font může sedět na spoustě
/// míst (styl prvku, styl sloupce, styl buňky, výchozí styl dokumentu, zóny JŘ prvků) a ručně
/// vyjmenovaný seznam by při přidání další stylové vrstvy tiše zestárnul - přesně jako zestaraly
/// výčty v `elementy-architektura.md`. Průchod JSONem chytí každé pole <c>fontFamily</c>, které
/// v šabloně je nebo kdy bude, včetně <c>CustomProperties</c>.</para>
/// </summary>
public static class FontWarnings
{
    /// <summary>Wire název property <see cref="TextStyle.FontFamily"/> (camelCase serializer).</summary>
    private const string FontFamilyKey = "fontFamily";

    /// <summary>
    /// Rodiny písma, které šablona používá, ale nejsou k dispozici. Seřazené, bez duplicit.
    /// Prázdný seznam = vše v pořádku.
    /// </summary>
    public static IReadOnlyList<string> MissingFamilies(Template? template)
    {
        if (template is null) return [];

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Collect(JsonNode.Parse(TemplateSerializer.Serialize(template)), used);
        }
        catch (Exception)
        {
            // Fail-open: upozornění je pomůcka, ne brána. Rozbitá serializace se projeví jinde
            // (uložení šablony) a nemá smysl kvůli ní shodit návrhář.
            return [];
        }

        return [.. used.Where(f => !FontResolver.IsInstalled(f))
                       .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static void Collect(JsonNode? node, HashSet<string> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == FontFamilyKey && value is JsonValue v &&
                        v.TryGetValue<string>(out var family) && !string.IsNullOrWhiteSpace(family))
                        into.Add(family.Trim());
                    else
                        Collect(value, into);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Collect(item, into);
                break;
        }
    }

    /// <summary>
    /// Věta pro uživatele, nebo null když je vše v pořádku. Jmenuje konkrétní fonty a říká,
    /// co se místo nich stane - bez toho by hláška nutila hádat.
    /// </summary>
    public static string? Summarize(Template? template)
    {
        var missing = MissingFamilies(template);
        if (missing.Count == 0) return null;

        string vyjmenovane = string.Join(", ", missing.Select(f => $"„{f}\""));
        return missing.Count == 1
            ? $"Font {vyjmenovane} není na tomto počítači nainstalovaný - tiskne se místo něj Arial."
            : $"Tyto fonty nejsou na tomto počítači nainstalované: {vyjmenovane} - tiskne se místo nich Arial.";
    }
}
