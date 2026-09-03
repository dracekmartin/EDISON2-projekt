using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.9.0 → 1.10.0: pole <c>POPIS_SMER</c> se přejmenovalo na <c>KONECNA_ZASTAVKA</c>
/// (a <c>POPIS_SMER_CIS</c> na <c>KONECNA_ZASTAVKA_CIS</c>).
///
/// <para>Důvod je uživatelský, ne technický: zákazník hledal „název cílové zastávky" a nenašel ho,
/// protože pole se jmenovalo „Konečná směru" - přitom je to přesně ta zastávka, ze které se počítá
/// i „CIS číslo cílové zastávky" (#13158). Přejmenovalo se **načisto**, staré pole nezůstalo žít
/// vedle nového; tahle migrace je to, co drží existující šablony funkční.</para>
///
/// <para>Transformuje uložené šablony:</para>
/// <list type="bullet">
/// <item>Placeholder tokeny ve všech textech a výrazech: <c>{POPIS_SMER}</c> → <c>{KONECNA_ZASTAVKA}</c>,
/// včetně tvaru s formátem (<c>{POPIS_SMER:...}</c>).</item>
/// <item>Sloupce tabulek (<c>columns[].field</c>) odkazující na přejmenované pole.</item>
/// </list>
/// Idempotentní (nové názvy nechá být) a defenzivní (chybějící uzly přeskočí).
/// </summary>
public class MigrationKonecnaZastavka : ITemplateMigration
{
    public string FromVersion => "1.9.0";

    public string Description => "pole POPIS_SMER přejmenováno na KONECNA_ZASTAVKA";

    private static readonly Dictionary<string, string> FieldRemap = new(StringComparer.Ordinal)
    {
        ["POPIS_SMER"]     = "KONECNA_ZASTAVKA",
        ["POPIS_SMER_CIS"] = "KONECNA_ZASTAVKA_CIS",
    };

    // Hranice tokenu (?=[:}]) chrání delší názvy: `{POPIS_SMER_CIS}` nesmí dostat půlku náhrady
    // z pravidla pro `{POPIS_SMER}` - další znak je tam `_`, ne `:` ani `}`, takže se netrefí.
    private static readonly Regex SmerCisToken = new(@"\{POPIS_SMER_CIS(?=[:}])", RegexOptions.Compiled);
    private static readonly Regex SmerToken    = new(@"\{POPIS_SMER(?=[:}])", RegexOptions.Compiled);

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is JsonArray sections)
        {
            foreach (var section in sections)
                if (section?["elements"] is JsonArray elements)
                    foreach (var el in elements)
                        if (el?["columns"] is JsonArray cols)
                            RemapFieldColumns(cols);
        }
        RewriteTokens(root);
        return root;
    }

    /// <summary>Sloupce tabulek odkazují na pole holým názvem, bez složených závorek.</summary>
    private static void RemapFieldColumns(JsonArray cols)
    {
        foreach (var col in cols)
        {
            if (col is not JsonObject c) continue;
            string? field = Str(c["field"]);
            if (field is not null && FieldRemap.TryGetValue(field, out var newField))
                c["field"] = newField;
        }
    }

    /// <summary>Rekurzivně přepíše placeholder tokeny ve všech textových hodnotách dokumentu.</summary>
    private static void RewriteTokens(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj.ToList())
                {
                    if (kvp.Value is JsonValue v && v.TryGetValue<string>(out var s))
                    {
                        string rewritten = Rewrite(s);
                        if (rewritten != s) obj[kvp.Key] = rewritten;
                    }
                    else RewriteTokens(kvp.Value);
                }
                break;
            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s))
                    {
                        string rewritten = Rewrite(s);
                        if (rewritten != s) arr[i] = rewritten;
                    }
                    else RewriteTokens(arr[i]);
                }
                break;
        }
    }

    private static string Rewrite(string s)
    {
        if (s.IndexOf('{') < 0) return s;
        s = SmerCisToken.Replace(s, "{KONECNA_ZASTAVKA_CIS");
        s = SmerToken.Replace(s, "{KONECNA_ZASTAVKA");
        return s;
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
