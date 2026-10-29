using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.15.0 → 1.16.0: podbarvení lichých Tč v Tabulce spojů se nastavuje jen barvou, prázdná = bez
/// podbarvení - stejně jako Střídání řádků u ostatních tabulek (rozhodnutí Jan 28. 10., `B194d` E3c).
///
/// <para>Klíč <c>highlightOddTc</c> se maže. Vypnuté podbarvení se přepíše na
/// <c>oddTcBackColor: null</c>; zapnuté nebo chybějící (výchozí bylo zapnuto) nechá barvu, jak je.</para>
/// </summary>
public class MigrationLichaTcBarvou : ITemplateMigration
{
    public string FromVersion => "1.15.0";

    public string Description => "podbarvení lichých Tč se nastavuje jen barvou";

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                if (o["type"]?.GetValue<string>() != ElementTypes.LineTable) continue;
                if (!o.TryGetPropertyValue("highlightOddTc", out var zapnuto)) continue;

                if (zapnuto is JsonValue v && v.TryGetValue<bool>(out var b) && !b)
                    o["oddTcBackColor"] = null;
                o.Remove("highlightOddTc");
            }
        }
        return root;
    }
}
