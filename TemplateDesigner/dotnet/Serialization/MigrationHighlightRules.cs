using System.Text.Json.Nodes;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.0.0 → 1.1.0: řádkové zvýraznění z jednoho pravidla (skalární <c>rowHighlightExpression</c> +
/// <c>rowHighlightColor</c>) na seznam <c>highlightRules[]</c>. Staré pole se převedou na jediné
/// pravidlo v seznamu a odstraní. Idempotentní (opakování nepřepíše existující seznam) a defensivní
/// (chybějící/nestringová hodnota → přeskočí).
/// </summary>
public class MigrationHighlightRules : ITemplateMigration
{
    public string FromVersion => "1.0.0";

    public string Description => "row highlight (jedno pravidlo) → highlightRules[] seznam";

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is JsonArray sections)
        {
            foreach (var section in sections)
            {
                if (section?["elements"] is JsonArray elements)
                {
                    foreach (var el in elements)
                        MigrateElement(el as JsonObject);
                }
            }
        }
        return root;
    }

    private static void MigrateElement(JsonObject? el)
    {
        if (el is null) return;

        string? expr = Str(el["rowHighlightExpression"]);
        string? color = Str(el["rowHighlightColor"]);
        el.Remove("rowHighlightExpression");
        el.Remove("rowHighlightColor");

        // Staré zvýraznění vyžadovalo podmínku I barvu (jinak nic nebarvilo) — bez obojího
        // pravidlo nevytvářet (žádný „dangling" bezbarevný záznam v editoru). Idempotence:
        // existující seznam nepřepisovat.
        if (string.IsNullOrWhiteSpace(expr) || string.IsNullOrWhiteSpace(color)) return;
        if (el["highlightRules"] is JsonArray) return;

        el["highlightRules"] = new JsonArray(new JsonObject
        {
            ["condition"] = expr,
            ["backgroundColor"] = color,
        });
    }

    private static string? Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
