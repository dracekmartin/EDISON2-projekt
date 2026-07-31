using System.Text.Json.Nodes;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.5.0 → 1.6.0: tloušťka šipky směru jízdy se zadává v milimetrech
/// (<c>directionArrowWidth</c>) místo přepínače „tučná" (<c>directionArrowBold</c>). Dvě cesty
/// k jedné vlastnosti byly zbytečné a přepínač neuměl to, co zákazník potřebuje - konkrétní
/// tloušťku.
///
/// <para>Převod drží dosavadní vzhled: <c>directionArrowBold=true</c> → 0,4 mm, jinak 0,2 mm
/// (hodnoty odpovídají dřívějším 1,5 / 0,8 px při zobrazení 1:1). Klíč <c>directionArrowBold</c>
/// se odstraní.</para>
///
/// Idempotentní (bez starého klíče nechá šablonu být) a defensivní (chybějící pole → default).
/// </summary>
public class MigrationArrowWidth : ITemplateMigration
{
    public string FromVersion => "1.5.0";

    public string Description => "šipka směru: přepínač tučná -> tloušťka v mm";

    /// <summary>Tloušťka odpovídající dřívějšímu „tučná šipka" (mm).</summary>
    private const double BoldWidthMm = 0.4;

    /// <summary>Tloušťka odpovídající dřívější netučné šipce (mm).</summary>
    private const double NormalWidthMm = 0.2;

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
                MigrateElement(el as JsonObject);
        }
        return root;
    }

    private static void MigrateElement(JsonObject? el)
    {
        if (el is null || !el.ContainsKey("directionArrowBold")) return;

        bool bold = el["directionArrowBold"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

        // Explicitní tloušťka (uložená mezitím) má přednost - přepínač jen doplňuje, co chybí.
        if (el["directionArrowWidth"] is null)
            el["directionArrowWidth"] = bold ? BoldWidthMm : NormalWidthMm;

        el.Remove("directionArrowBold");
    }
}
