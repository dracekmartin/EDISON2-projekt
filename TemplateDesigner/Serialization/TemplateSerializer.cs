using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TemplateDesigner.Models;

namespace TemplateDesigner.Serialization;

/// <summary>
/// Ukládá a načítá šablony jako JSON soubory.
/// Formát: camelCase, enums jako string, null hodnoty vynechány, floaty na 4 desetinná místa.
/// </summary>
public static class TemplateSerializer
{
    public const string FileExtension = ".json";
    public const string FileFilter = "Šablona (*.json)|*.json|Všechny soubory (*.*)|*.*";

    private static readonly JsonSerializerOptions Options = BuildOptions();

    // --- Serializace ---

    public static string Serialize(Template template)
    {
        template.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(template, Options);
    }

    public static async Task SaveAsync(Template template, string filePath, CancellationToken ct = default)
    {
        template.ModifiedAt = DateTime.UtcNow;
        await using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, template, Options, ct);
    }

    // --- Deserializace ---

    /// <summary>
    /// Načte šablonu z JSON stringu. Vrátí null při chybě parsování.
    /// </summary>
    /// <exception cref="TemplateDeserializationException">Pokud JSON nelze načíst nebo není platná šablona.</exception>
    public static Template Deserialize(string json)
    {
        try
        {
            var template = JsonSerializer.Deserialize<Template>(json, Options);
            return template ?? throw new TemplateDeserializationException("JSON deserializace vrátila null.");
        }
        catch (JsonException ex)
        {
            throw new TemplateDeserializationException($"Neplatný formát JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Načte šablonu ze souboru.
    /// </summary>
    /// <exception cref="TemplateDeserializationException">Pokud soubor nelze načíst nebo není platná šablona.</exception>
    public static async Task<Template> LoadAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var template = await JsonSerializer.DeserializeAsync<Template>(stream, Options, ct);
            return template ?? throw new TemplateDeserializationException("Soubor neobsahuje platnou šablonu.");
        }
        catch (TemplateDeserializationException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new TemplateDeserializationException($"Neplatný formát šablony v souboru '{Path.GetFileName(filePath)}': {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new TemplateDeserializationException($"Soubor '{Path.GetFileName(filePath)}' nelze otevřít: {ex.Message}", ex);
        }
    }

    // --- Nastavení serializeru ---

    private static JsonSerializerOptions BuildOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Null hodnoty se do JSON nevypisují (defaultní/false/0 hodnoty se vypisují)
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            // Enums jako čitelné stringy: "OnEveryPage" místo 1
            new JsonStringEnumConverter(),
            // Floaty zaokrouhleny na 4 desetinná místa: 0.5000001 → 0.5
            new RoundedFloatConverter(),
            new RoundedNullableFloatConverter()
        }
    };
}

// ---------------------------------------------------------------------------
// Interní konvertory

/// <summary>Zaokrouhlí float na 4 desetinná místa při zápisu do JSON.</summary>
internal sealed class RoundedFloatConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetSingle();

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        => writer.WriteNumberValue(MathF.Round(value, 4));
}

/// <summary>Totéž pro nullable float.</summary>
internal sealed class RoundedNullableFloatConverter : JsonConverter<float?>
{
    public override float? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetSingle();

    public override void Write(Utf8JsonWriter writer, float? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(MathF.Round(value.Value, 4));
    }
}

// ---------------------------------------------------------------------------

public class TemplateDeserializationException : Exception
{
    public TemplateDeserializationException(string message) : base(message) { }
    public TemplateDeserializationException(string message, Exception inner) : base(message, inner) { }
}
