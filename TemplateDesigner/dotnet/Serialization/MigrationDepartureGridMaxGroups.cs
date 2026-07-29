using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.3.0 → 1.4.0: DepartureGrid <c>maxGroupCount</c> - starý implicitní default 3 → 0 (bez limitu).
/// Strop 3 chránil layout před érou SMO seskupování (sloupec per typ provozu = desítky sloupců);
/// po zavedení SMO sloupců tiše ořezával 4. skupinu dnů v rozporu s checkboxy v dialogu sloupců
/// i SMO nastavením. Jiné hodnoty než 3 (1, 2, 4…) = vědomé nastavení → zůstávají.
/// </summary>
public class MigrationDepartureGridMaxGroups : ITemplateMigration
{
    public string FromVersion => "1.3.0";

    public string Description => "DepartureGrid maxGroupCount: starý default 3 → 0 (bez limitu)";

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
        if (el["type"] is not JsonValue tv || !tv.TryGetValue<string>(out var type)) return;
        if (type != ElementTypes.DepartureGrid) return;

        if (el["maxGroupCount"] is JsonValue mv && mv.TryGetValue<int>(out var max) && max == 3)
            el["maxGroupCount"] = 0;
    }
}
