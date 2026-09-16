using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.12.0 → 1.13.0: zahodí per-sloupcovou typografii záhlaví u prvku Minuty odjezdů.
///
/// <para>Od 14. 6. kreslení typografii záhlaví bralo vždy z prvku a per-sloupcovou ignorovalo,
/// takže hodnoty v šablonách zůstaly ležet nepoužité. Teď se kaskáda zapíná zpátky - bez téhle
/// migrace by ožily a sloupec by najednou vypadal jinak, než jak vypadal celé léto.</para>
///
/// <para>Typicky jde o <c>bold = true</c>, které do každé skupiny dnů zapisoval import z PDF.
/// Právě kvůli němu se v záhlaví odjezdů nedalo vypnout tučné a kaskáda se tehdy vypnula.</para>
///
/// <para>Barvy, zarovnání a odsazení zůstávají - ty per sloupec platily celou dobu. Prázdný styl
/// po vyčištění se zahodí, ať v šabloně nezůstane uzel bez obsahu.</para>
/// </summary>
public class MigrationZahlaviOdjezduPerSloupec : ITemplateMigration
{
    public string FromVersion => "1.12.0";

    public string Description => "per-sloupcová typografie záhlaví odjezdů se maže (od 14. 6. se ignorovala)";

    /// <summary>Vlastnosti, které kreslení u záhlaví odjezdů ignorovalo.</summary>
    private static readonly string[] Typografie =
        ["fontFamily", "fontSize", "fontWeight", "bold", "italic", "underline", "strikethrough"];

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                if (o["type"]?.GetValue<string>() != ElementTypes.DepartureGrid) continue;

                VycistiStyl(o["hourColumnStyle"] as JsonObject);
                if (o["groupColumns"] is JsonArray skupiny)
                    foreach (var sk in skupiny)
                        VycistiStyl((sk as JsonObject)?["style"] as JsonObject);
            }
        }
        return root;
    }

    /// <summary>Vymaže typografii ze záhlaví sloupce; zbyde-li prázdný uzel, odstraní i ten.</summary>
    private static void VycistiStyl(JsonObject? styl)
    {
        if (styl?["header"] is not JsonObject zahlavi) return;
        foreach (var klic in Typografie) zahlavi.Remove(klic);
        if (zahlavi.Count == 0) styl.Remove("header");
    }
}
