using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    // --- Serializace Template ---

    public static string Serialize(Template template)
    {
        StampForSave(template);
        return JsonSerializer.Serialize(template, Options);
    }

    /// <summary>
    /// Tvar šablony <b>bez razítkování</b> - pro porovnání dvou šablon. <see cref="Serialize"/> při
    /// každém volání přepíše čas změny na „teď", takže se jím dvě šablony nikdy nerovnají (a seed
    /// pak přepisoval systémové šablony při každém startu).
    /// </summary>
    internal static string SerializeForCompare(Template template) =>
        JsonSerializer.Serialize(template, Options);

    /// <summary>Razítko při uložení: čas změny + aktuální verze formátu (uložením se šablona
    /// zapisuje v aktuálním schématu, ne v tom, ze kterého byla načtena).</summary>
    private static void StampForSave(Template template)
    {
        template.ModifiedAt = DateTime.UtcNow;
        template.Version = TemplateMigrator.CurrentVersion;
    }

    /// <summary>
    /// Hluboká kopie šablony přes JSON round-trip - snapshot pro export na pozadí
    /// (uživatel může dál editovat živou šablonu, běžící export se nezmění).
    /// Na rozdíl od <see cref="Serialize"/> nemodifikuje ModifiedAt; přeskakuje migrator
    /// (in-memory šablona je vždy aktuální verze).
    /// </summary>
    public static Template DeepClone(Template template) =>
        JsonSerializer.Deserialize<Template>(JsonSerializer.Serialize(template, Options), Options)
            ?? throw new InvalidOperationException("Snapshot šablony selhal (deserializace vrátila null).");

    // --- Serializace Section (pro custom šablony) ---

    /// <summary>
    /// Tisk „Kromě první" zmizel z nabídky (`B177`, 24. 9.) - stará sekce se převede na „Každá
    /// stránka" s podmínkou <c>{STRANA} &gt; 1</c>, která se chová stejně. Vrací, jestli se něco změnilo.
    /// </summary>
    /// <remarks>Mimo verzový pipeline, jako přemapování kódů agend: hodnota výčtu zůstává, takže
    /// ji starší exe dál přečte, a uložením se propíše nový tvar.</remarks>
    internal static bool PrevedKromePrvni(Section sekce)
    {
        if (sekce.PrintBehavior != SectionPrintBehavior.OnEveryPageExceptFirst) return false;
        sekce.PrintBehavior = SectionPrintBehavior.OnEveryPage;
        sekce.VisibilityExpression = string.IsNullOrWhiteSpace(sekce.VisibilityExpression)
            ? "{STRANA} > 1"
            : $"{{STRANA}} > 1 && ({sekce.VisibilityExpression})";
        return true;
    }

    /// <summary>
    /// Opakovaná sekce s Umístěním „Obsah" se zařadila podle pořadí: nad posledním obsahem hlavička,
    /// pod ním patička. Formulář (`B177`) takovou volbu nemá, proto se zařazení zapíše výslovně -
    /// stejným pravidlem jako v sazbě (<c>PageLayoutEngine.ClassifySections</c>), takže se tisk
    /// nezmění. Starší exe výslovnou hlavičku i patičku zná.
    /// </summary>
    internal static bool UrciUmisteniPodlePoradi(List<Section> sekce)
    {
        int posledniObsah = sekce.FindLastIndex(s =>
            s.PrintBehavior is SectionPrintBehavior.Once or SectionPrintBehavior.PerDataRow);
        bool zmena = false;
        for (int i = 0; i < sekce.Count; i++)
        {
            var s = sekce[i];
            if (s.PrintBehavior is SectionPrintBehavior.Once or SectionPrintBehavior.PerDataRow) continue;
            if (s.Role != SectionRole.Body) continue;
            s.Role = posledniObsah >= 0 && i > posledniObsah ? SectionRole.Footer : SectionRole.Header;
            zmena = true;
        }
        return zmena;
    }

    /// <summary>Serializuje sekci do JSON (pro uložení jako custom šablona).</summary>
    public static string SerializeSection(Section section)
    {
        return JsonSerializer.Serialize(section, Options);
    }

    /// <summary>
    /// Deserializuje sekci z JSON (custom šablona sekce). Šablony sekcí nemají vlastní verzi →
    /// obalí se do mini-šablony <c>1.0.0</c> a protáhnou <see cref="Migrator"/>, aby se aplikovaly
    /// stejné migrace jako na plnou šablonu. Bez toho by byla tahle cesta trvale migrátor-slepá
    /// a tiše ztrácela data při každé budoucí breaking změně JSON formátu.
    /// </summary>
    public static Section? DeserializeSection(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is null) return null;

            var wrapper = new JsonObject
            {
                ["version"] = "1.0.0",
                ["sections"] = new JsonArray(node),
            };
            var (migrated, _, _) = Migrator.Migrate(wrapper);

            if (migrated["sections"] is not JsonArray { Count: > 0 } sections) return null;
            var sekce = sections[0]?.Deserialize<Section>(Options);
            if (sekce is not null) PrevedKromePrvni(sekce);
            return sekce;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TemplateDeserializationException)
        {
            // Vadná šablona sekce se přeskočí (nezabíjí načtení ostatních)
            return null;
        }
    }

    public static async Task SaveAsync(Template template, string filePath, CancellationToken ct = default)
    {
        StampForSave(template);
        // Serializace běží na threadpoolu - JsonSerializer.SerializeAsync dělá
        // reflection walk synchronně na volajícím threadu. Velké šablony
        // (schedule elementy, base64 obrázky) jinak zamrazí UI.
        var bytes = await Task.Run(() => JsonSerializer.SerializeToUtf8Bytes(template, Options), ct);
        await File.WriteAllBytesAsync(filePath, bytes, ct);
    }

    /// <summary>Synchronní varianta pro volající kteří nemohou await (např. ConfirmUnsavedChanges při zavírání okna).</summary>
    public static void Save(Template template, string filePath)
    {
        StampForSave(template);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(template, Options);
        File.WriteAllBytes(filePath, bytes);
    }

    // --- Deserializace ---

    /// <summary>
    /// Sdílený migrátor - bezstavový, thread-safe, lze opakovaně použít.
    /// Pokud bude potřeba dependency injection, refactor na instance per consumer.
    /// </summary>
    private static readonly TemplateMigrator Migrator = new();

    /// <summary>
    /// Přejmenované agenda kódy: starý kód → aktuální kód v registru.
    /// Konvence zavedená 2026-05-20: <c>&lt;TYP_SESTAVY&gt;_&lt;ZDROJ&gt;</c> - staré
    /// šablony uložené s původním <c>"MHD"</c> jinak po loadu nesedí s registrem
    /// (prázdné instance, prázdná pole agendy, nefunkční data). FAKTURY se neměnilo.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyAgendaCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MHD"] = "ZASTAVKOVY_JR_MHD",
    };

    /// <summary>
    /// Načte šablonu z JSON stringu. Před deserializací aplikuje <see cref="TemplateMigrator"/>
    /// pokud verze v JSON je nižší než <see cref="TemplateMigrator.CurrentVersion"/>.
    /// </summary>
    /// <exception cref="TemplateDeserializationException">Pokud JSON nelze načíst nebo není platná šablona.</exception>
    public static Template Deserialize(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)
                ?? throw new TemplateDeserializationException("JSON deserializace vrátila null.");
            var (migrated, wasMigrated, _) = Migrator.Migrate(root);
            var template = migrated.Deserialize<Template>(Options)
                ?? throw new TemplateDeserializationException("JSON deserializace vrátila null.");

            // Přemapuj přejmenované agenda kódy (legacy "MHD" → "ZASTAVKOVY_JR_MHD").
            // Mimo verzový pipeline - rename není verzová breaking change v JSON, ale
            // staré šablony s ním nesedí s registrem. Uložením se canonical kód propíše.
            if (template.AgendaCode is { } code && LegacyAgendaCodes.TryGetValue(code, out var canonical))
            {
                template.AgendaCode = canonical;
                wasMigrated = true;
            }

            // Převody sekcí (`B177`) tisk nemění, proto nehlásí migraci - stavový řádek by jinak
            // tvrdil „nový formát", který to není.
            foreach (var sekce in template.Sections)
                PrevedKromePrvni(sekce);
            UrciUmisteniPodlePoradi(template.Sections);

            template.WasMigrated = wasMigrated;
            return template;
        }
        catch (TemplateDeserializationException) { throw; }
        catch (JsonException ex)
        {
            throw new TemplateDeserializationException($"Neplatný formát JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Načte šablonu ze souboru. Před deserializací aplikuje <see cref="TemplateMigrator"/>.
    /// </summary>
    /// <exception cref="TemplateDeserializationException">Pokud soubor nelze načíst nebo není platná šablona.</exception>
    public static async Task<Template> LoadAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            string json = await File.ReadAllTextAsync(filePath, ct);
            return Deserialize(json);
        }
        catch (TemplateDeserializationException) { throw; }
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
