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
    public static Template Deserialize(string json) => Deserialize(json, jmenoSouboru: null);

    private static Template Deserialize(string json, string? jmenoSouboru)
    {
        try
        {
            JsonNode? root;
            try { root = JsonNode.Parse(json); }
            catch (JsonException ex) { throw PoskozenaSablona("není to platný JSON", jmenoSouboru, ex); }
            if (root is null) throw new TemplateDeserializationException("JSON deserializace vrátila null.");

            // Tvar se kontroluje před migrací - migrace s cizím tvarem spadly s hláškou o nepovedené
            // migraci, která uživateli nic neřekla.
            if (ProcNemaTvarSablony(root) is { } tvar) throw PoskozenaSablona(tvar, jmenoSouboru);
            var (migrated, wasMigrated, _) = Migrator.Migrate(root);
            if (ProcNeznamyPrvek(migrated) is { } prvek) throw PoskozenaSablona(prvek, jmenoSouboru);

            Template template;
            try
            {
                template = migrated.Deserialize<Template>(Options)
                    ?? throw new TemplateDeserializationException("JSON deserializace vrátila null.");
            }
            catch (JsonException ex)
            {
                throw PoskozenaSablona($"údaj „{ex.Path}“ má nečekanou hodnotu", jmenoSouboru, ex);
            }
            catch (NotSupportedException ex)
            {
                // Typ prvku existuje (ProcNeznamyPrvek), deserializace ho ale čte jen jako první údaj prvku.
                throw PoskozenaSablona("u některého prvku není typ uvedený jako první údaj", jmenoSouboru, ex);
            }

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

    private static readonly HashSet<string> ZnameTypyPrvku = typeof(TemplateElement)
        .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
        .Cast<JsonDerivedTypeAttribute>()
        .Select(a => a.TypeDiscriminator as string)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Proč JSON nemá tvar šablony (objekt se seznamem sekcí, sekce se seznamem prvků, prvek
    /// s typem); null = tvar sedí. Sekce ani prvky nejsou povinné - šablona z databáze je mít nemusí.</summary>
    private static string? ProcNemaTvarSablony(JsonNode root)
    {
        const string sekceNeboPrvky = "sekce nebo prvky nemají tvar šablony";
        if (root is not JsonObject sablona) return "nemá tvar šablony";
        if (sablona["sections"] is null) return null;
        if (sablona["sections"] is not JsonArray sekce) return sekceNeboPrvky;
        foreach (var s in sekce)
        {
            if (s is not JsonObject sekceObj) return sekceNeboPrvky;
            if (sekceObj["elements"] is null) continue;
            if (sekceObj["elements"] is not JsonArray prvky) return sekceNeboPrvky;
            foreach (var p in prvky)
            {
                if (p is not JsonObject prvek) return sekceNeboPrvky;
                if (prvek["type"] is not JsonValue typ || !typ.TryGetValue<string>(out var t) || t.Length == 0)
                    return $"prvek v sekci „{sekceObj["name"]?.ToString()}“ nemá uvedený typ";
            }
        }
        return null;
    }

    /// <summary>Typ prvku, který návrhář nezná (po migraci, ta typy přejmenovává); null = všechny zná.</summary>
    private static string? ProcNeznamyPrvek(JsonNode root)
    {
        foreach (var sekce in root["sections"] as JsonArray ?? new JsonArray())
            foreach (var prvek in sekce?["elements"] as JsonArray ?? new JsonArray())
            {
                var typ = prvek?["type"] is JsonValue v && v.TryGetValue<string>(out var t) ? t : null;
                if (typ is null || !ZnameTypyPrvku.Contains(typ))
                    return $"obsahuje prvek neznámého typu „{typ}“";
            }
        return null;
    }

    /// <summary>Šablona, kterou nejde přečíst. Zůstává chybou (ne informací jako cizí soubor): poškozený
    /// export může být naše chyba a ta se má dostat k nám.</summary>
    private static TemplateDeserializationException PoskozenaSablona(string duvod, string? jmenoSouboru, Exception? inner = null)
    {
        var text = jmenoSouboru is null
            ? $"Šablona je poškozená: {duvod}."
            : $"Šablonu ze souboru „{jmenoSouboru}“ nejde načíst, soubor je poškozený: {duvod}.";
        return inner is null ? new(text) : new(text, inner);
    }

    /// <summary>Proč soubor není šablona, srozumitelně pro uživatele; null = vypadá jako JSON
    /// a chybu v něm má hlásit až deserializace.</summary>
    /// <remarks>Uživatel při importu vybral PDF předlohy a dostal „'%' is an invalid start of
    /// a value" jako chybu aplikace (telemetrie 19. 10., CB_CSADAD).</remarks>
    internal static string? ProcNeniSablona(string obsah, string jmenoSouboru)
    {
        var zacatek = obsah.AsSpan().TrimStart("﻿ \t\r\n");
        if (zacatek.StartsWith("{")) return null;
        return TextCizihoSouboru(jmenoSouboru, zacatek.StartsWith("%PDF") ? "je PDF, ne šablona" : "není šablona návrháře");
    }

    private static string TextCizihoSouboru(string jmenoSouboru, string co) =>
        $"Soubor „{jmenoSouboru}“ {co}. Ze souboru jde načíst jen šablona uložená přes "
        + "Soubor → Exportovat do souboru (soubor .json).";

    /// <summary>
    /// Načte šablonu ze souboru. Před deserializací aplikuje <see cref="TemplateMigrator"/>.
    /// </summary>
    /// <exception cref="TemplateDeserializationException">Pokud soubor nelze načíst nebo není platná šablona.</exception>
    public static async Task<Template> LoadAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            string json = await File.ReadAllTextAsync(filePath, ct);
            var jmeno = Path.GetFileName(filePath);
            if (ProcNeniSablona(json, jmeno) is { } duvod)
                throw new TemplateDeserializationException(duvod) { NeniSablona = true };
            var sablona = Deserialize(json, jmeno);
            // Šablona z návrháře má vždy aspoň jednu sekci (poslední smazat nejde) - bez ní jde o cizí JSON.
            if (sablona.Sections.Count == 0)
                throw new TemplateDeserializationException(TextCizihoSouboru(jmeno, "není šablona návrháře")) { NeniSablona = true };
            return sablona;
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

    /// <summary>Soubor vůbec není šablona (PDF, text, JSON bez sekcí) - uživatel vybral jiný soubor,
    /// nejde o chybu aplikace. Zpráva je pro uživatele srozumitelná.</summary>
    public bool NeniSablona { get; init; }
}
