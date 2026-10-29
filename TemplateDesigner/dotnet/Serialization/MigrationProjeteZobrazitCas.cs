using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.16.0 → 1.17.0: čas u projetých zastávek v Seznamu zastávek je zaškrtávátko „Projeté: Zobrazit čas",
/// ne výběr Nezobrazovat / Zobrazit - výběr ze dvou možností ano/ne patří do zaškrtávátka (rozhodnutí
/// Jan 28. 10., popisky podle typu pole).
///
/// <para>Klíč <c>passedStopsTime</c> („Hide" / „Show", starší šablony i číslo 0 / 1) se mění na
/// <c>passedStopsShowTime</c> (true jen u „Show"). Chybějící klíč = výchozí skryto, nic se nezapíše.</para>
/// </summary>
public class MigrationProjeteZobrazitCas : ITemplateMigration
{
    public string FromVersion => "1.16.0";

    public string Description => "čas u projetých zastávek je zaškrtávátko";

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                if (o["type"]?.GetValue<string>() != ElementTypes.StopList) continue;
                if (!o.TryGetPropertyValue("passedStopsTime", out var puvodni)) continue;

                if (Zobrazit(puvodni)) o["passedStopsShowTime"] = true;
                o.Remove("passedStopsTime");
            }
        }
        return root;
    }

    private static bool Zobrazit(JsonNode? hodnota) => hodnota is JsonValue v
        && ((v.TryGetValue<string>(out var text) && string.Equals(text, "Show", StringComparison.OrdinalIgnoreCase))
            || (v.TryGetValue<int>(out var cislo) && cislo == 1));
}
