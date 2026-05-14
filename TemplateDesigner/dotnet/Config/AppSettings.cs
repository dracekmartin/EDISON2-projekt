using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Konfigurace aplikace.
/// Pořadí načítání (každá další vrstva přepíše předchozí):
///  1. Embedded resource (zabudovaný v .exe — baseline, aby aplikace fungovala self-contained)
///  2. Externí templatedesigner.appsettings.json vedle exe (volitelný override pro celý config)
///  3. Externí templatedesigner.appsettings.local.json vedle exe (volitelný per-user override; gitignored)
///
/// Záměrně nesdílí název s generickým appsettings.json kvůli složce C:\Edison\,
/// kde žije víc aplikací (EdisonCacheMcp atd.) s vlastní strukturou JSON.
/// </summary>
public class AppSettings
{
    public const string ConfigFileName      = "templatedesigner.appsettings.json";
    public const string LocalConfigFileName = "templatedesigner.appsettings.local.json";

    public CacheConnectionSettings? Cache    { get; set; }
    public List<SchemaAgendaSettings> Agendas { get; set; } = [];
    public AiAssistantSettings AiAssistant   { get; set; } = new();

    /// <summary>Zdroj nakonec použitého configu (pro diagnostiku startu).</summary>
    [JsonIgnore]
    public ConfigSource Source { get; private set; } = ConfigSource.None;

    /// <summary>Singleton instance načtená při startu aplikace.</summary>
    public static AppSettings? Current { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    public static AppSettings Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, ConfigFileName);
        var localPath = Path.Combine(baseDir, LocalConfigFileName);

        // 1. Embedded baseline (vždy přítomný v .exe)
        var (settings, source) = LoadFromEmbedded();

        // 2. Externí soubor vedle exe (přepíše embedded, pokud existuje)
        if (File.Exists(path))
        {
            var fromFile = TryLoadFromFile(path);
            if (fromFile is not null)
            {
                settings = fromFile;
                source = ConfigSource.ExternalFile;
            }
        }

        settings ??= new AppSettings();
        settings.Source = source;

        // 3. Lokální override (per-user, gitignored)
        if (File.Exists(localPath))
        {
            var local = TryLoadFromFile(localPath);
            if (local is not null) MergeLocal(settings, local);
        }

        Current = settings;
        return Current;
    }

    /// <summary>Načte embedded resource zabudovaný do assembly při buildu.</summary>
    private static (AppSettings? settings, ConfigSource source) LoadFromEmbedded()
    {
        try
        {
            var assembly = typeof(AppSettings).Assembly;
            using var stream = assembly.GetManifestResourceStream(ConfigFileName);
            if (stream is null) return (null, ConfigSource.None);

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            var json = reader.ReadToEnd();
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return parsed is not null ? (parsed, ConfigSource.Embedded) : (null, ConfigSource.None);
        }
        catch { return (null, ConfigSource.None); }
    }

    private static AppSettings? TryLoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch { return null; }
    }

    /// <summary>Přepíše hodnoty v target hodnotami z local (jen pokud jsou v local explicitně nastavené).</summary>
    private static void MergeLocal(AppSettings target, AppSettings local)
    {
        if (local.Cache is not null)
            target.Cache = local.Cache;
        if (local.Agendas.Count > 0)
            target.Agendas = local.Agendas;
        if (!string.IsNullOrEmpty(local.AiAssistant.AnthropicApiKey))
            target.AiAssistant.AnthropicApiKey = local.AiAssistant.AnthropicApiKey;
        if (local.AiAssistant.Enabled != target.AiAssistant.Enabled)
            target.AiAssistant.Enabled = local.AiAssistant.Enabled;
    }
}

/// <summary>Odkud byl primární config načten (pro diagnostiku).</summary>
public enum ConfigSource
{
    None,           // Nic se nepodařilo načíst (chyba při buildu)
    Embedded,       // Z embedded resource v .exe
    ExternalFile    // Z externího souboru vedle .exe
}

/// <summary>Nastavení připojení k Caché / IRIS serveru přes MLRest.</summary>
public class CacheConnectionSettings
{
    /// <summary>IP adresa nebo hostname serveru.</summary>
    public string Server    { get; set; } = "127.0.0.1";

    /// <summary>Port MLRest serveru (výchozí 57772).</summary>
    public int    Port      { get; set; } = 57772;

    /// <summary>Namespace Caché — třetí parametr konstruktoru MLRest.</summary>
    public string Namespace { get; set; } = "ALWA";

    /// <summary>IDFA identifikátor — čtvrtý parametr konstruktoru MLRest.</summary>
    public string Idfa      { get; set; } = "";

    /// <summary>Přihlašovací jméno (MLRest.LoginName).</summary>
    public string LoginName { get; set; } = "";

    /// <summary>Celé jméno (MLRest.FullName).</summary>
    public string FullName  { get; set; } = "";

    /// <summary>MLP token pro autentizaci (MLRest.Mlp).</summary>
    public string Mlp       { get; set; } = "";

    /// <summary>True pokud je připojení nakonfigurováno.</summary>
    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && Port > 0;
}

/// <summary>
/// Definice jedné agendy načítané z Caché.
/// Caché třída musí dědit z ALVA.TemplateDesigner.Agendas.AgendaAPI
/// a implementovat GetSchema, GetTableTemplates, GetTableTemplate, GetData.
/// </summary>
public class SchemaAgendaSettings
{
    /// <summary>Kód agendy — musí odpovídat Template.AgendaCode, např. "JR".</summary>
    public string Code       { get; set; } = "";

    /// <summary>Čitelný název agendy pro UI.</summary>
    public string Name       { get; set; } = "";

    /// <summary>
    /// Caché třída implementující AgendaAPI,
    /// např. "ALVA.TemplateDesigner.Agendas.ZastavkoveJrMhdAPI".
    /// </summary>
    public string CacheClass { get; set; } = "";
}

/// <summary>Nastavení AI asistenta pro generování šablon.</summary>
public class AiAssistantSettings
{
    /// <summary>Povolení AI asistenta (false = skryje menu položku).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Anthropic API klíč pro volání Claude API.
    /// Pokud není nastaven, dialog nabídne zadat klíč ručně.
    /// Získat na https://console.anthropic.com/
    /// </summary>
    public string AnthropicApiKey { get; set; } = "";

    /// <summary>Model pro AI Vision import (default: claude-sonnet-4-6).</summary>
    public string VisionModel { get; set; } = "claude-sonnet-4-6";
}
