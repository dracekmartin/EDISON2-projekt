using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EDControls.EDList;

/// <summary>
/// Persistovaný stav sloupců EDList per uživatel × host. Ukládá se přes
/// <see cref="UserPrefs.IUserPrefsService"/> jako JSON blob pod klíčem
/// (scope="EDControls.EDList", key=EDList.HostId).
/// </summary>
internal sealed class ColumnLayout
{
    /// <summary>Verze JSON schématu - informativní, čtení je best-effort.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("columns")]
    public List<ColumnPref> Columns { get; set; } = [];

    /// <summary>Sort descriptions - null nebo prázdné = bez sortu.</summary>
    [JsonPropertyName("sort")]
    public List<SortPref>? Sort { get; set; }
}

internal sealed class ColumnPref
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>Šířka v pixelech. 0 = auto (nezachycuje šířku).</summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>DataGrid display index (pořadí zobrazení, 0-based).</summary>
    [JsonPropertyName("displayIndex")]
    public int DisplayIndex { get; set; }
}

internal sealed class SortPref
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    /// <summary>"asc" nebo "desc". Default "asc".</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}

/// <summary>
/// JSON serializace pro <see cref="ColumnLayout"/>. Best-effort čtení - neznámá pole
/// se ignorují, malformed JSON vrátí null.
/// </summary>
internal static class ColumnLayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static JsonNode Serialize(ColumnLayout layout)
        => JsonSerializer.SerializeToNode(layout, Options)!;

    /// <summary>Deserializuje. Vrátí null pokud blob nelze parsovat nebo má jiný tvar.</summary>
    public static ColumnLayout? Deserialize(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            var layout = node.Deserialize<ColumnLayout>(Options);
            if (layout is null) return null;
            // Defenzivně - nullovat pokud server vrátí prázdné kolekce ve špatném tvaru
            layout.Columns ??= [];
            return layout;
        }
        catch (JsonException)
        {
            return null;  // best-effort
        }
    }
}
