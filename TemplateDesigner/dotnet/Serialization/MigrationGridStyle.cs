using System.Text.Json.Nodes;

namespace TemplateDesigner.Serialization;

/// <summary>
/// 1.6.0 → 1.7.0: mřížka tabulkových prvků je jeden sdílený objekt <c>grid</c>
/// (řádky · sloupce · barva · tloušťka mm · styl) a **vnější obrys má na starost rámeček elementu**.
///
/// <para>Do 1.6.0 vedly k vnějšímu obrysu dvě cesty - rámeček, který má každý prvek, a vlastní rámec
/// tabulky (zadrátovaný v kódu). JŘ prvky měly barvu/tloušťku/styl jako skaláry, tabulky místo toho
/// volbu <c>borderStyle</c> s pěti hodnotami a vzhled vůbec nenastavitelný.</para>
///
/// <para><b>Převod drží dosavadní vzhled.</b> JŘ prvky: skaláry → <c>grid</c> 1:1, přepínače čar
/// zapnuté (kreslily se vždy), u seznamu zastávek se přenesou jeho dva vlastní přepínače.
/// Tabulky: <c>Vše</c>→řádky+sloupce · <c>Řádky</c>→řádky · <c>Sloupce</c>→sloupce ·
/// <c>Vnější</c>/<c>Žádný</c>→žádné vnitřní čáry; a u všeho kromě <c>Žádný</c> se dosavadní
/// zadrátovaný rámec přenese do rámečku elementu, pokud si ho uživatel sám nenastavil.</para>
///
/// Idempotentní (bez starých klíčů nechá šablonu být) a defensivní (chybějící pole → default).
/// </summary>
public class MigrationGridStyle : ITemplateMigration
{
    public string FromVersion => "1.6.0";

    public string Description => "mřížka tabulkových prvků do sdíleného objektu; vnější obrys = rámeček prvku";

    /// <summary>Dřívější zadrátovaný rámec tabulky: 0,7 px při 96 DPI.</summary>
    private const double TableBorderMm = 0.19;

    private const string TableBorderColor = "#FF999999";
    private const string TableGridColor   = "#FFCCCCCC";

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
        if (el is null) return;

        if (el.ContainsKey("borderStyle")) MigrateTable(el);
        else if (HasAnyScheduleGridKey(el)) MigrateSchedule(el);
    }

    private static bool HasAnyScheduleGridKey(JsonObject el) =>
        el.ContainsKey("gridColor") || el.ContainsKey("gridLineWidth") || el.ContainsKey("gridLineStyle")
        || el.ContainsKey("showRowSeparators") || el.ContainsKey("showColumnSeparators");

    /// <summary>JŘ prvky: skaláry → objekt. Čáry se kreslily vždy, takže přepínače jdou na true;
    /// seznam zastávek měl vlastní dva, ty mají přednost.</summary>
    private static void MigrateSchedule(JsonObject el)
    {
        var grid = Grid(el);

        Move(el, "gridColor", grid, "color");
        Move(el, "gridLineWidth", grid, "width");
        Move(el, "gridLineStyle", grid, "style");

        grid["showRowLines"] = TakeBool(el, "showRowSeparators", defaultValue: true);
        grid["showColumnLines"] = TakeBool(el, "showColumnSeparators", defaultValue: true);
    }

    /// <summary>Tabulky: volba pěti hodnot → přepínače vnitřních čar + rámeček elementu.</summary>
    private static void MigrateTable(JsonObject el)
    {
        string style = el["borderStyle"]?.GetValue<string>() ?? "Outer";
        el.Remove("borderStyle");

        var grid = Grid(el);
        grid["showRowLines"] = style is "All" or "RowsOnly";
        grid["showColumnLines"] = style is "All" or "ColumnsOnly";
        grid["color"] ??= TableGridColor;

        // Vnější rámec kreslila tabulka sama - přenes ho do rámečku elementu, ať výstup zůstane.
        // Vlastní nastavení uživatele nepřebíjíme (rámeček byl dosud u tabulek bez efektu, ale
        // mohl být nastavený z doby, kdy element vznikl jako jiný typ).
        if (style == "None") return;
        if (el["borderWidth"] is null || IsZero(el["borderWidth"])) el["borderWidth"] = TableBorderMm;
        if (el["borderColor"] is null || IsTransparent(el["borderColor"])) el["borderColor"] = TableBorderColor;
    }

    private static JsonObject Grid(JsonObject el)
    {
        if (el["grid"] is JsonObject existing) return existing;
        var grid = new JsonObject();
        el["grid"] = grid;
        return grid;
    }

    private static void Move(JsonObject el, string from, JsonObject grid, string to)
    {
        if (el[from] is not { } value) return;
        grid[to] ??= value.DeepClone();
        el.Remove(from);
    }

    private static bool TakeBool(JsonObject el, string key, bool defaultValue)
    {
        if (el[key] is not { } node) return defaultValue;
        bool value = node is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        el.Remove(key);
        return value;
    }

    private static bool IsZero(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<double>(out var d) && d == 0;

    /// <summary>Průhledná barva = rámeček fakticky vypnutý, takže ho smíme nastavit.</summary>
    private static bool IsTransparent(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s)
        && s.Length == 9 && s.StartsWith("#00", StringComparison.OrdinalIgnoreCase);
}
