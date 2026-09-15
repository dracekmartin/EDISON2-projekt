using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EDControls.EDFilter;

/// <summary>
/// Persistovaný stav filtru per uživatel × host. Ukládá se přes
/// <see cref="UserPrefs.IUserPrefsService"/> jako JSON blob pod klíčem
/// (scope="EDControls.EDFilter", key=EDFilter.HostId).
///
/// Pojmenované varianty filtru přibudou jako sourozenecké klíče, čtení
/// <see cref="Values"/> se tím nemění.
/// </summary>
public sealed class EDFilterPrefs
{
    /// <summary>Verze JSON schématu - informativní, čtení je best-effort.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>Poslední hodnoty polí, klíčem je <c>EDFilterFieldBase.Key</c>.</summary>
    [JsonPropertyName("values")]
    public Dictionary<string, string> Values { get; set; } = [];
}

/// <summary>
/// JSON serializace pro <see cref="EDFilterPrefs"/>. Best-effort čtení - neznámá pole
/// se ignorují, malformed JSON vrátí null.
/// </summary>
public static class EDFilterPrefsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static JsonNode Serialize(EDFilterPrefs prefs)
        => JsonSerializer.SerializeToNode(prefs, Options)!;

    /// <summary>Deserializuje. Vrátí null pokud blob nelze parsovat nebo má jiný tvar.</summary>
    public static EDFilterPrefs? Deserialize(JsonNode? node)
    {
        if (node is null) return null;
        try
        {
            var prefs = node.Deserialize<EDFilterPrefs>(Options);
            if (prefs is null) return null;
            prefs.Values ??= [];
            return prefs;
        }
        catch (JsonException)
        {
            return null;  // best-effort
        }
    }
}
