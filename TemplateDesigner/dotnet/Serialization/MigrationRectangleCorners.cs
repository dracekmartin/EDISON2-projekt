using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.4.0 → 1.5.0: rohy elementu sjednoceny na base (<c>cornerStyle</c> + <c>cornerSize</c> pro
/// všechny elementy). Dřív mělo zaoblení jen <see cref="Models.Elements.RectangleElement"/> ve
/// vlastním <c>cornerRadius</c>. Převedeme: <c>cornerRadius &gt; 0</c> → <c>cornerStyle=Rounded</c>
/// + <c>cornerSize=cornerRadius</c>; klíč <c>cornerRadius</c> odstraníme (jinde se ignoruje).
/// </summary>
public class MigrationRectangleCorners : ITemplateMigration
{
    public string FromVersion => "1.4.0";

    public string Description => "RectangleElement cornerRadius → base cornerStyle/cornerSize";

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
        if (el["cornerRadius"] is not JsonValue rv || !rv.TryGetValue<double>(out var radius)) return;

        if (radius > 0)
        {
            el["cornerStyle"] = "Rounded";
            el["cornerSize"] = radius;
        }
        el.Remove("cornerRadius");
    }
}
