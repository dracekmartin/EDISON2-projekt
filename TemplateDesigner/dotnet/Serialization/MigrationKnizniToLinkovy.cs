using System.Text.Json.Nodes;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.1.0 → 1.2.0: přejmenování agendy „knižní JŘ" → „linkový JŘ" (knihy se už netisknou,
/// „knižní" byl legacy název formátu). Kód agendy <c>KNIZNI_JR_AD/MHD</c> → <c>LINKOVY_JR_AD/MHD</c>.
/// Bez remapu by staré šablony po přejmenování agend nenašly agendu v registru (prázdné schéma).
/// Idempotentní (nové kódy nechá být) a defensivní (chybějící/nestringový agendaCode → přeskočí).
/// </summary>
public class MigrationKnizniToLinkovy : ITemplateMigration
{
    public string FromVersion => "1.1.0";

    public string Description => "agenda KNIZNI_JR_* → LINKOVY_JR_*";

    public JsonNode Migrate(JsonNode root)
    {
        if (root is JsonObject obj && obj["agendaCode"] is JsonValue v
            && v.TryGetValue<string>(out var code))
        {
            obj["agendaCode"] = code switch
            {
                "KNIZNI_JR_AD"  => "LINKOVY_JR_AD",
                "KNIZNI_JR_MHD" => "LINKOVY_JR_MHD",
                _               => code,
            };
        }
        return root;
    }
}
