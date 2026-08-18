using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.7.0 → 1.8.0: varianty jízdních dob se zapínají jako sloupec, ne vlastností prvku. Zaškrtávátko
/// <c>showAllRouteVariants</c> bylo pozůstatek doby, kdy variantní sloupce ten „Čas jízdy"
/// <b>nahrazovaly</b>; od 13. 8. stojí vedle sebe, takže to bylo nastavení sloupců schované mimo
/// sloupce (a šířka ani styl variant nešly nastavit vůbec).
///
/// <para>Převod u každého <c>StopListElement</c>: za sloupec „Čas jízdy" se vloží položka
/// <see cref="StopFields.TravelTimeVariants"/>, která přebere jeho zarovnání i per-sloupcový styl
/// (dosud si je varianty půjčovaly od něj za běhu) a dostane dosud zadrátovanou šířku 6 mm.
/// Vlastnost <c>showAllRouteVariants</c> se odstraní.</para>
///
/// <para><b>Viditelnost je věrný převod dnešního vzhledu, ne oprava:</b> varianty se rozvíjely ZE
/// sloupce „Čas", takže při jeho skrytí zmizely, i když je uživatel měl zaškrtnuté. Nová položka je
/// proto viditelná jen tehdy, když bylo zaškrtnuté obojí. Kdyby se zapnula podle zaškrtávátka, mohlo
/// by se v hotové vývěsce objevit až dvanáct sloupců, aniž by na cokoli klikl (rozhodnutí Jan
/// 2026-08-13). Past samotná je tímhle krokem pryč - položka je nadále nezávislá.</para>
///
/// <para>Idempotentní (položku už přítomnou nepřidá znovu) a defensivní (chybějící sekce/sloupce,
/// jiný typ hodnoty → přeskočí).</para>
/// </summary>
public class MigrationRouteVariantsColumn : ITemplateMigration
{
    public string FromVersion => "1.7.0";

    public string Description => "varianty tras jako sloupec místo vlastnosti prvku";

    /// <summary>Dosavadní zadrátovaná šířka variantního sloupce (<c>VariantColumnWidthMm</c>).</summary>
    private const float VariantWidthMm = 6f;

    private const string LegacyFlag = "showAllRouteVariants";

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
        if (obj["type"]?.GetValue<string>() != "StopListElement") return;

        // Vlajku čteme i tam, kde chybí - default prvku byl „zapnuto" (legacy chování).
        bool flag = ReadFlag(obj);
        obj.Remove(LegacyFlag);

        if (obj["columns"] is not JsonArray columns) return;
        if (IndexOfKey(columns, StopFields.TravelTimeVariants) >= 0) return;   // idempotence

        int casIdx = IndexOfKey(columns, StopFields.TravelTime);
        bool casVisible = casIdx >= 0 && ReadVisible(columns[casIdx]);

        var varianty = new JsonObject
        {
            ["key"]     = StopFields.TravelTimeVariants,
            ["header"]  = "",
            ["width"]   = VariantWidthMm,
            // Rozvinutí viselo na sloupci „Čas" - bez něj se varianty netiskly, ať bylo zaškrtnuto cokoli.
            ["visible"] = flag && casVisible,
        };

        // Zarovnání a styl si varianty dosud braly ze sloupce „Čas" za běhu; teď je nesou samy,
        // takže se musí zkopírovat, jinak by se zákazníkovi změnil vzhled hotové vývěsky.
        if (casIdx >= 0 && columns[casIdx] is JsonObject cas)
        {
            if (cas["alignment"] is { } alignment) varianty["alignment"] = alignment.DeepClone();
            if (cas["style"]     is { } style)     varianty["style"]     = style.DeepClone();
        }

        columns.Insert(casIdx >= 0 ? casIdx + 1 : 0, varianty);
    }

    /// <summary>Hodnota zrušené vlastnosti; chybějící = zapnuto (dosavadní default prvku).
    /// Tolerantní k tvaru - starší ruční úpravy šablon nesly i <c>"true"</c> jako text.</summary>
    private static bool ReadFlag(JsonObject obj) => obj[LegacyFlag] switch
    {
        null                        => true,
        JsonValue v when v.TryGetValue(out bool b)   => b,
        JsonValue v when v.TryGetValue(out string? s) => !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),
        _                           => true,
    };

    private static bool ReadVisible(JsonNode? column) =>
        column?["visible"] is not JsonValue v || !v.TryGetValue(out bool b) || b;

    private static int IndexOfKey(JsonArray columns, string key)
    {
        for (int i = 0; i < columns.Count; i++)
            if (columns[i]?["key"]?.GetValue<string>() == key) return i;
        return -1;
    }
}
