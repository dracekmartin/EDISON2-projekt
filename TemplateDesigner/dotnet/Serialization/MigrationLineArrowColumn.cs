using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.8.0 → 1.9.0: linková tabulka dostala sloupec <b>šipky směru jízdy</b>
/// (<see cref="ColumnKeys.Arrow"/>), takže ho existující šablony musí mít v seznamu sloupců.
///
/// <para>Proč migrací a ne za běhu: dialog sloupců umí sloupce jen <b>přeskládat, skrýt a nastavit</b>,
/// nikoli přidat. Bez tohohle kroku by se šipka dala zapnout jen v šablonách založených po téhle
/// verzi a ve starších by chyběla natrvalo - a nešlo by poznat, proč.</para>
///
/// <para><b>Vkládá se skrytá</b> a před sloupec zastávek, tedy tam, kde ji má starý modul. Skrytá
/// proto, že migrace nesmí změnit vzhled hotové šablony: kdyby se zapnula, přibyl by v tisku sloupec,
/// aniž by na cokoli klikl (týž důvod jako u <see cref="MigrationRouteVariantsColumn"/>).</para>
///
/// <para>Idempotentní (sloupec už přítomný nepřidá znovu) a defensivní (chybějící sekce, jiný typ
/// hodnoty, prvek bez sloupců → přeskočí).</para>
/// </summary>
public class MigrationLineArrowColumn : ITemplateMigration
{
    public string FromVersion => "1.8.0";

    public string Description => "sloupec šipky směru v linkové tabulce";

    /// <summary>Shodná s výchozí definicí v <c>LineTableElement.GetDefaultColumns</c>.</summary>
    private const float SirkaMm = 4f;

    public JsonNode Migrate(JsonNode root)
    {
        foreach (var section in root["sections"]?.AsArray() ?? [])
        foreach (var element in section?["elements"]?.AsArray() ?? [])
            MigrateElement(element);
        return root;
    }

    private static void MigrateElement(JsonNode? element)
    {
        if (element is not JsonObject obj) return;
        if (obj["type"]?.GetValue<string>() != "LineTableElement") return;
        if (obj["columns"] is not JsonArray columns) return;
        if (IndexOfKey(columns, ColumnKeys.Arrow) >= 0) return;   // idempotence

        var sipka = new JsonObject
        {
            ["key"] = ColumnKeys.Arrow,
            ["header"] = "",
            ["width"] = SirkaMm,
            ["visible"] = false,
            ["alignment"] = nameof(TextHorizontalAlignment.Center),
        };

        int zastavkaIdx = IndexOfKey(columns, ColumnKeys.StopName);
        if (zastavkaIdx >= 0) columns.Insert(zastavkaIdx, sipka);
        else columns.Add(sipka);
    }

    private static int IndexOfKey(JsonArray columns, string key)
    {
        for (int i = 0; i < columns.Count; i++)
            if (columns[i]?["key"]?.GetValue<string>() == key) return i;
        return -1;
    }
}
