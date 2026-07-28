using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.2.0 → 1.3.0: sjednocení datového/sloupcového kontraktu zastávek. Klíče sloupců StopListu byly
/// interní směrovací kódy (<c>StopName</c>, <c>TravelTime</c>…), pole ZASTAVKY měla kryptická drátová ID
/// (<c>POPIS</c>, <c>JE_AKTUALNI</c>, <c>TC</c>). Nově je vše čitelné field ID (<c>NAZEV_ZASTAVKY</c>,
/// <c>CAS_JIZDY</c>, <c>PORADI</c>, <c>AKTUALNI_ZASTAVKA</c>, <c>ZONY</c>) sdílené 1:1 mezi StopListem
/// a DynamicTable nad ZASTAVKY. <c>TČ</c> zrušeno úplně.
///
/// <para>Transformuje uložené šablony:</para>
/// <list type="bullet">
/// <item>StopList <c>columns[].key</c>: staré směrovací klíče → field ID; sloupce <c>Tc</c>/<c>RouteLetters</c>/<c>BaseRoute</c> odstraněny.</item>
/// <item>DynamicTable/Table <c>columns[].field</c>: <c>POPIS</c>→<c>NAZEV_ZASTAVKY</c>, <c>JE_AKTUALNI</c>→<c>AKTUALNI_ZASTAVKA</c>.</item>
/// <item>Placeholder tokeny ve všech textech/výrazech: <c>{POPIS}</c>→<c>{NAZEV_ZASTAVKY}</c>, <c>{JE_AKTUALNI}</c>→<c>{AKTUALNI_ZASTAVKA}</c>, <c>{TC}</c> odstraněno. Hranice tokenu chrání skalární pole (<c>{POPIS_DOPRAVCE}</c> se netkne).</item>
/// </list>
/// Idempotentní (nové klíče/ID nechá být) a defensivní (chybějící pole → přeskočí).
/// </summary>
public class MigrationUnifyStopColumns : ITemplateMigration
{
    public string FromVersion => "1.2.0";

    public string Description => "sjednocení kontraktu zastávek: čitelná field ID + TČ zrušeno";

    // StopList směrovací klíč → field ID. Klíče mimo mapu (a nedropnuté) zůstanou beze změny.
    private static readonly Dictionary<string, string> StopKeyRemap = new(StringComparer.Ordinal)
    {
        ["StopName"]   = "NAZEV_ZASTAVKY",
        ["TravelTime"] = "CAS_JIZDY",
        ["Zone"]       = "ZONY",
        ["Poradi"]     = "PORADI",
        ["JeAktualni"] = "AKTUALNI_ZASTAVKA",
    };

    // StopList sloupce, které v novém kontraktu neexistují - z uložené šablony se odstraní.
    private static readonly HashSet<string> DroppedStopKeys = new(StringComparer.Ordinal)
    {
        "Tc", "RouteLetters", "BaseRoute",
    };

    // Drátové ID polí ZASTAVKY (field mapping v DynamicTable/Table).
    private static readonly Dictionary<string, string> FieldRemap = new(StringComparer.Ordinal)
    {
        ["POPIS"]       = "NAZEV_ZASTAVKY",
        ["JE_AKTUALNI"] = "AKTUALNI_ZASTAVKA",
    };

    // Token boundary (?=[:}]) → přejmenuje jen celé pole {POPIS}/{POPIS:FMT}, ne {POPIS_DOPRAVCE}.
    private static readonly Regex PopisToken      = new(@"\{POPIS(?=[:}])", RegexOptions.Compiled);
    private static readonly Regex JeAktualniToken = new(@"\{JE_AKTUALNI(?=[:}])", RegexOptions.Compiled);
    private static readonly Regex TcToken         = new(@"\{TC(:[^}]*)?\}", RegexOptions.Compiled);

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is JsonArray sections)
        {
            foreach (var section in sections)
                if (section?["elements"] is JsonArray elements)
                    foreach (var el in elements)
                        MigrateElement(el as JsonObject);
        }
        RewriteTokens(root);
        return root;
    }

    private static void MigrateElement(JsonObject? el)
    {
        if (el is null) return;
        string? type = Str(el["type"]);

        if (type == "StopListElement" && el["columns"] is JsonArray stopCols)
            RemapStopColumns(el, stopCols);
        else if (el["columns"] is JsonArray tableCols)
            RemapFieldColumns(tableCols);
    }

    /// <summary>StopList: remap key + odstranění zrušených sloupců.</summary>
    private static void RemapStopColumns(JsonObject el, JsonArray cols)
    {
        var kept = new JsonArray();
        foreach (var col in cols)
        {
            if (col is not JsonObject c) continue;
            string? key = Str(c["key"]);
            if (key is null) { kept.Add(c.DeepClone()); continue; }
            if (DroppedStopKeys.Contains(key)) continue;
            if (StopKeyRemap.TryGetValue(key, out var newKey))
                c["key"] = newKey;
            kept.Add(c.DeepClone());
        }
        el["columns"] = kept;
    }

    /// <summary>DynamicTable/Table: remap field ID (POPIS/JE_AKTUALNI → čitelné).</summary>
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

    /// <summary>Rekurzivně přepíše placeholder tokeny ve všech string hodnotách dokumentu.</summary>
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
                        if (!ReferenceEquals(rewritten, s) && rewritten != s)
                            obj[kvp.Key] = rewritten;
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
        s = PopisToken.Replace(s, "{NAZEV_ZASTAVKY");
        s = JeAktualniToken.Replace(s, "{AKTUALNI_ZASTAVKA");
        s = TcToken.Replace(s, "");
        return s;
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
