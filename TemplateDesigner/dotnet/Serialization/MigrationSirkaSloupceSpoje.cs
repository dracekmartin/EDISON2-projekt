using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.13.0 → 1.14.0: dvojice „nejmenší/největší šířka sloupce spoje" se slévá do jedné šířky.
///
/// <para>Dvě pole svádějí k tomu, že jde o pár, jenže zužovat uměl jen strop - a ten se zarážel
/// o změřený obsah, takže sloupec s dvojicí piktogramů nad časy nešlo zúžit vůbec. Nově se zadaná
/// šířka drží všude, kde se obsah vejde, a kde ne, zůstane sloupec tak široký, jak obsah
/// potřebuje.</para>
///
/// <para>Přenáší se strop, protože to bylo pole, kterým se šířka reálně určovala; když chybí,
/// vezme se dno. Nové chování je k obojímu blízké: hodnota drží šířku většiny sloupců a jen ty
/// s širším obsahem vyjdou širší, místo aby přetekly.</para>
/// </summary>
public class MigrationSirkaSloupceSpoje : ITemplateMigration
{
    public string FromVersion => "1.13.0";

    public string Description => "nejmenší a největší šířka sloupce spoje se slévá do jedné šířky";

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

                float? sirka = Hodnota(o, "nejvetsiSirkaSpojeMm") ?? Hodnota(o, "nejmensiSirkaSpojeMm");
                o.Remove("nejmensiSirkaSpojeMm");
                o.Remove("nejvetsiSirkaSpojeMm");
                if (sirka is float mm) o["sirkaSloupceSpojeMm"] = mm;
            }
        }
        return root;
    }

    /// <summary>Kladná hodnota klíče, nebo null - nula i prázdno znamenaly „nenastaveno".</summary>
    private static float? Hodnota(JsonObject o, string klic)
    {
        if (o[klic] is not JsonValue v || !v.TryGetValue<float>(out float mm)) return null;
        return mm > 0f ? mm : null;
    }
}
