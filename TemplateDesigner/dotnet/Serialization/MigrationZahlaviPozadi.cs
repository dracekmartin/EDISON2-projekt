using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.10.0 → 1.11.0: podbarvení záhlaví se zapisuje do prvku, aby prázdná hodnota mohla znamenat
/// „nekreslit nic".
///
/// <para>Uživatel vymazal šedou u záhlaví poznámek a ta se po uložení vrátila (nález Pavel 1. 9.,
/// #13158). Šedá totiž v prvku nikdy nebyla - byla zapečená jako <b>záloha kaskády</b>, takže
/// prázdná hodnota znamenala „nic neurčuji, vezmi zálohu", ne „žádné pozadí". Ty dva stavy nešlo
/// odlišit a „bez podbarvení" nebylo jak vyjádřit.</para>
///
/// <para>Záloha se proto zrušila a šedá se novým prvkům zapisuje. Aby hotové šablony vypadaly
/// stejně jako dřív, dopíše jim tahle migrace tutéž barvu, jakou by jim dřív dala záloha.</para>
///
/// <para>Idempotentní (prvek s vlastní hodnotou nechá být, <b>včetně prázdné</b> - ta už znamená
/// vědomé „bez podbarvení") a defenzivní (chybějící uzly přeskočí). Datové buňky se netýká, ty měly
/// zálohu průhlednou.</para>
/// </summary>
public class MigrationZahlaviPozadi : ITemplateMigration
{
    public string FromVersion => "1.10.0";

    public string Description => "podbarvení záhlaví se zapisuje do prvku (prázdno = bez podbarvení)";

    /// <summary>Typ prvku → barva, kterou mu dřív dala zapečená záloha kaskády.</summary>
    private static readonly Dictionary<string, string> ZalohaPodleTypu = new(StringComparer.Ordinal)
    {
        [ElementTypes.StopList]      = ScheduleElementBase.NoveZahlaviPozadi,
        [ElementTypes.DepartureGrid] = ScheduleElementBase.NoveZahlaviPozadi,
        [ElementTypes.NotesLegend]   = ScheduleElementBase.NoveZahlaviPozadi,
        [ElementTypes.LineTable]     = ScheduleElementBase.NoveZahlaviPozadi,
        [ElementTypes.Table]         = TableElement.NoveZahlaviPozadi,
        [ElementTypes.DynamicTable]  = DynamicTableElement.NoveZahlaviPozadi,
    };

    public JsonNode Migrate(JsonNode root)
    {
        if (root["sections"] is not JsonArray sections) return root;

        foreach (var section in sections)
        {
            if (section?["elements"] is not JsonArray elements) continue;
            foreach (var el in elements)
            {
                if (el is not JsonObject o) continue;
                string? typ = o["type"]?.GetValue<string>();
                if (typ is null || !ZalohaPodleTypu.TryGetValue(typ, out var barva)) continue;
                DopisPozadi(o, barva);
            }
        }
        return root;
    }

    /// <summary>
    /// Dopíše barvu do <c>headerStyle.backgroundColor</c>, pokud tam klíč vůbec není. Prvek, který
    /// hodnotu má, se nechává být - i když je prázdná.
    /// </summary>
    private static void DopisPozadi(JsonObject el, string barva)
    {
        if (el["headerStyle"] is not JsonObject styl)
        {
            // Prvek styl záhlaví neměl vůbec - dřív mu kaskáda dala zálohu, teď ji dostane zapsanou.
            el["headerStyle"] = new JsonObject { ["backgroundColor"] = barva };
            return;
        }
        if (!styl.ContainsKey("backgroundColor"))
            styl["backgroundColor"] = barva;
    }
}
