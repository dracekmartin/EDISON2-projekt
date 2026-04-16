using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Konfigurace aplikace načítaná z appsettings.json.
/// Soubor leží vedle spustitelného souboru (AppContext.BaseDirectory).
/// </summary>
public class AppSettings
{
    public CacheConnectionSettings? Cache    { get; set; }
    public List<SchemaAgendaSettings> Agendas { get; set; } = [];
    public AiAssistantSettings AiAssistant   { get; set; } = new();

    /// <summary>Singleton instance načtená při startu aplikace.</summary>
    public static AppSettings? Current { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    /// <summary>
    /// Načte appsettings.json ze složky vedle exe.
    /// Pokud existuje appsettings.local.json, přepíše hodnoty z něj (pro lokální konfiguraci).
    /// Pokud soubor neexistuje nebo je poškozený, vrátí výchozí instanci (bez Caché).
    /// </summary>
    public static AppSettings Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "appsettings.json");
        var localPath = Path.Combine(baseDir, "appsettings.local.json");

        // Načíst hlavní config
        AppSettings? settings = null;
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            }
            catch { /* ignore */ }
        }

        settings ??= new AppSettings();

        // Merge s lokálním configem (přepíše hodnoty z appsettings.json)
        if (File.Exists(localPath))
        {
            try
            {
                var localJson = File.ReadAllText(localPath, System.Text.Encoding.UTF8);
                var local = JsonSerializer.Deserialize<AppSettings>(localJson, JsonOptions);
                if (local is not null)
                {
                    // Merge: lokální hodnoty přepíšou hlavní (jen pokud jsou v .local.json opravdu definované)
                    if (local.Cache is not null)
                        settings.Cache = local.Cache;
                    if (local.Agendas.Count > 0)
                        settings.Agendas = local.Agendas;
                    if (!string.IsNullOrEmpty(local.AiAssistant.AnthropicApiKey))
                        settings.AiAssistant.AnthropicApiKey = local.AiAssistant.AnthropicApiKey;
                    if (local.AiAssistant.Enabled != settings.AiAssistant.Enabled)
                        settings.AiAssistant.Enabled = local.AiAssistant.Enabled;
                }
            }
            catch { /* ignore */ }
        }

        Current = settings;
        return Current;
    }
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
/// Caché třída musí dědit z ALVA.TemplateDesigner.API.AgendaAPI
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
    /// např. "ALVA.TemplateDesigner.API.ZastavkoveJrMhdAPI".
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
}
