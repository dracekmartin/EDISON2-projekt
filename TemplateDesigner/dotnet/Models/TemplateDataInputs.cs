using System.Globalization;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Údaje ze šablony, které jdou serveru při načtení dat (<c>GetData</c> i ukázková data): vlastní sloupce
/// vývěsky, turnusy ke spojům a jazyk textů značek. Odpověď na ně dává šablona, ne uživatel před tiskem,
/// proto se skládají vždy čerstvě ze šablony a nikdy z uložených hodnot parametrů (třída chyb <c>B24</c>).
/// </summary>
/// <remarks>
/// Do 30. 10. byly opsané na čtyřech místech (vstup <c>GetData</c>, kontrola „změnila se data?", vstup
/// ukázkových dat, výjimky testu parametrů) a ukázková data turnusy neznala. Nový takový údaj je vlastnost
/// tady, v <see cref="From"/> a ve <see cref="Values"/>: přenačtení náhledu (rovnost záznamu), ukázková
/// data, jejich cache i test parametrů ho převezmou.
/// </remarks>
public sealed record TemplateDataInputs(string? ColumnsDefinitionJson, bool TurnusyKeSpojum, string Jazyk)
{
    public const string ColumnsDefinitionKey = "DEFINICE_SLOUPCU";
    public const string TurnusyKey = "TURNUSY_KE_SPOJUM";
    public const string JazykKey = "JAZYK";

    /// <summary>Všechny klíče, které tenhle záznam serveru posílá.</summary>
    public static IReadOnlyList<string> Keys { get; } = [ColumnsDefinitionKey, TurnusyKey, JazykKey];

    public static TemplateDataInputs From(Template template)
    {
        var prvky = template.Sections.SelectMany(s => s.Elements).ToList();
        return new(
            // Definice platí pro celou šablonu - z prvního prvku, který ji má; žádná = rozvržení podle linky.
            prvky.OfType<IColumnLayoutOwner>().Select(e => e.BuildColumnsDefinitionWireJson()).FirstOrDefault(j => j is not null),
            // Dotaz do modulu turnusů je drahý - jen když je některý prvek opravdu tiskne.
            prvky.OfType<LineTableElement>().Any(e => e.TurnusyKeSpojum),
            // Jazyk z kultury šablony (vlastnosti stránky), tou se řídí i symbolový font.
            template.GetCulture().TwoLetterISOLanguageName.ToLowerInvariant());
    }

    /// <summary>Hodnoty pro server. Bez definice sloupců zůstane server u rozvržení podle linky,
    /// bez jazyka u češtiny - proto se prázdné neposílají.</summary>
    public IEnumerable<KeyValuePair<string, string>> Values()
    {
        if (!string.IsNullOrEmpty(ColumnsDefinitionJson)) yield return new(ColumnsDefinitionKey, ColumnsDefinitionJson);
        yield return new(TurnusyKey, TurnusyKeSpojum ? "true" : "false");
        if (!string.IsNullOrEmpty(Jazyk)) yield return new(JazykKey, Jazyk);
    }

    /// <summary>Zapíše údaje do vstupu <c>GetData</c>.</summary>
    /// <remarks>⚠️ Klíč, který šablona nemá, se musí <b>odebrat</b>: jinak ve vstupu zůstane hodnota z uložených
    /// parametrů a server dostane rozvržení, které šablona nemá. Do 4. 9. se jen přidávalo, takže uživatel, který si
    /// někdy vlastní sloupce nastavil, je dostával i do šablon bez nich a spoj mimo ně z tabulky tiše zmizel.</remarks>
    public void ApplyTo(IDictionary<string, object?> parameters)
    {
        foreach (var key in Keys) parameters.Remove(key);
        foreach (var (key, value) in Values()) parameters[key] = value;
    }
}
