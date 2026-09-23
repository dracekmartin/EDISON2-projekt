using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.14.0 → 1.15.0: šířka sloupce spoje se přestala zadávat číslem, počítá se z obsahu.
///
/// <para>Zadané číslo bylo cílem, pod který sloupec stejně nešel (obsah je jeho dolní hranice),
/// a proti němu stála volba „stejně široké sloupce spojů", která ho uměla přebít. Dvě nastavení
/// na jednu věc, a vyhrávalo to druhé bez vysvětlení - proto číslo mizí a zbývá volba, jestli se
/// sloupce mají srovnat na nejširší.</para>
///
/// <para>Klíč <c>sirkaSloupceSpojeMm</c> se maže. Zapnutá jednotná šířka zůstává tam, kde ji
/// šablona má - výchozí stav se sice mění na vypnuto, ale serializace ho do šablon zapisuje,
/// takže existující rozvržení se nemění.</para>
/// </summary>
public class MigrationSirkaZObsahu : ITemplateMigration
{
    public string FromVersion => "1.14.0";

    public string Description => "šířka sloupce spoje se nezadává, počítá se z obsahu";

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

                o.Remove("sirkaSloupceSpojeMm");
                // Šablona z doby, kdy volba ještě neexistovala, měla sloupce srovnané - od 7. 9.
                // to bylo výchozí chování a bez zápisu by se jí rozvržení změnilo.
                o["jednotnaSirkaSpoju"] ??= true;
            }
        }
        return root;
    }
}
