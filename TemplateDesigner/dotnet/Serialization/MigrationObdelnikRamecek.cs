using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.17.0 → 1.18.0: Obdélník kreslí obrys jen rámečkem prvku (rozhodnutí Jan 28. 10.). Do té doby měl
/// vedle rámečku vlastní okraj (<c>strokeColor</c> / <c>strokeWidth</c>) se stejným tvarem i rohy -
/// dvě nastavení pro jednu čáru, mezi kterými uživatel rozdíl nepoznal.
///
/// <para>Viditelný okraj se přenese do rámečku, pokud rámeček viditelný není. Mají-li viditelné obojí,
/// zůstane rámeček (kreslil se navrch). Klíče okraje se mažou. Chybějící klíč = tehdejší výchozí
/// hodnota: okraj černý 0,5 mm, rámeček žádný.</para>
/// </summary>
public class MigrationObdelnikRamecek : ITemplateMigration
{
    public string FromVersion => "1.17.0";

    public string Description => "okraj obdélníku je rámeček prvku";

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                if (o["type"]?.GetValue<string>() != ElementTypes.Rectangle) continue;

                var okrajBarva = Text(o, "strokeColor") ?? "#FF000000";
                var okrajTloustka = Number(o, "strokeWidth") ?? 0.5;
                var ramecekBarva = Text(o, "borderColor") ?? "#00FFFFFF";
                var ramecekTloustka = Number(o, "borderWidth") ?? 0;

                if (Viditelny(okrajBarva, okrajTloustka) && !Viditelny(ramecekBarva, ramecekTloustka))
                {
                    o["borderColor"] = okrajBarva;
                    o["borderWidth"] = okrajTloustka;
                }
                o.Remove("strokeColor");
                o.Remove("strokeWidth");
            }
        }
        return root;
    }

    /// <summary>Čára se kreslí, když má tloušťku a barvu, která není průhledná (barva z dat se kreslí vždy).</summary>
    private static bool Viditelny(string barva, double tloustka) =>
        tloustka > 0 && (ColorHex.IsDataBound(barva) || ColorHex.TryParse(barva, out var c) && c.Alpha > 0);

    private static string? Text(JsonObject o, string klic) =>
        o[klic] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double? Number(JsonObject o, string klic) =>
        o[klic] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
}
