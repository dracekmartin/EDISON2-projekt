using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Config;

/// <summary>
/// Konfigurace aplikace.
/// Pořadí načítání (každá další vrstva přepíše předchozí):
///  1. Embedded resource (zabudovaný v .exe - baseline, aby aplikace fungovala self-contained)
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
    public AiAssistantSettings AiAssistant   { get; set; } = new();

    /// <summary>Kanál, kterým M-line doručuje systémové šablony (úložiště na helpdesku).</summary>
    public TemplateChannelSettings TemplateChannel { get; set; } = new();

    /// <summary>Vývojářské nástroje. Výchozí vypnuté - zapíná se configem u toho, kdo testuje.</summary>
    public DevSettings Dev { get; set; } = new();

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
    /// <remarks>
    /// ⚠️ Slučuje se <b>vyjmenovaně</b>, ne celý objekt - kdo přidá nový oddíl konfigurace, musí ho
    /// přidat i sem, jinak se hodnota z lokálního souboru **tiše zahodí** a featura se tváří jako
    /// vypnutá (stalo se 19. 8. klíči k distribučnímu kanálu: nastavený klíč, a přehled šablon nic
    /// nenabídl). Hlídá to <c>AppSettingsLocalMergeTests</c>.
    /// </remarks>
    internal static void MergeLocal(AppSettings target, AppSettings local)
    {
        if (local.Cache is not null)
            target.Cache = local.Cache;
        if (!string.IsNullOrEmpty(local.AiAssistant.AnthropicApiKey))
            target.AiAssistant.AnthropicApiKey = local.AiAssistant.AnthropicApiKey;
        if (local.AiAssistant.Enabled != target.AiAssistant.Enabled)
            target.AiAssistant.Enabled = local.AiAssistant.Enabled;

        if (!string.IsNullOrWhiteSpace(local.TemplateChannel.ApiKey))
            target.TemplateChannel.ApiKey = local.TemplateChannel.ApiKey;
        if (!string.IsNullOrWhiteSpace(local.TemplateChannel.BaseUrl))
            target.TemplateChannel.BaseUrl = local.TemplateChannel.BaseUrl;
        if (local.TemplateChannel.Enabled != target.TemplateChannel.Enabled)
            target.TemplateChannel.Enabled = local.TemplateChannel.Enabled;

        if (local.Dev.FirmSwitch) target.Dev.FirmSwitch = true;
        if (local.Dev.PdfImport) target.Dev.PdfImport = true;
    }
}

/// <summary>Odkud byl primární config načten (pro diagnostiku).</summary>
public enum ConfigSource
{
    None,           // Nic se nepodařilo načíst (chyba při buildu)
    Embedded,       // Z embedded resource v .exe
    ExternalFile    // Z externího souboru vedle .exe
}

/// <summary>
/// Vývojářské nástroje, které se zákazníkovi ukázat nesmí.
/// </summary>
/// <remarks>
/// ⚠️ Zapíná se <b>vlastním klíčem</b>, ne přítomností klíče k AI. Do 27. 8. viselo přepínání firmy
/// na neprázdném <c>aiAssistant.anthropicApiKey</c>, takže se z publikovaného .exe nešlo přepnout
/// nikdy: klíč žije v lokálním (gitignored) configu vedle buildu ve zdrojích, ne v C:\Edison\.
/// Testovací linky přitom leží ve víc firmách, takže to blokovalo procházení.
/// </remarks>
public class DevSettings
{
    /// <summary>Zobrazit v Nástrojích přepínání datové firmy (Idfa).</summary>
    public bool FirmSwitch { get; set; }

    /// <summary>
    /// Zobrazit v Nástrojích import šablony z PDF. Je to interní nástroj M-line pro převod předloh
    /// zákazníků (rozhodnutí Jan 9. 10.): polohy prvků zatím odhaduje model, takže výsledek kolísá
    /// a zákazník, který ho našel, ztrácel čas ručním opravováním (KAD #11373).
    /// </summary>
    public bool PdfImport { get; set; }
}

/// <summary>Nastavení připojení k Caché / IRIS serveru přes MLRest.</summary>
public class CacheConnectionSettings
{
    /// <summary>IP adresa nebo hostname serveru.</summary>
    public string Server    { get; set; } = "127.0.0.1";

    /// <summary>Port MLRest serveru (výchozí 57772).</summary>
    public int    Port      { get; set; } = 57772;

    /// <summary>Namespace Caché - třetí parametr konstruktoru MLRest.</summary>
    public string Namespace { get; set; } = "ALWA";

    /// <summary>IDFA identifikátor - čtvrtý parametr konstruktoru MLRest.</summary>
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
/// Distribuční kanál systémových šablon. U zákazníka je **jen pro čtení** (úložiště → klient);
/// nahrávat a měnit smíme jen my ve vývoji, nikdy ne z buildu, který jde ven - proto tady žádná
/// cesta k zápisu není. Klíč smí být zadrátovaný v aplikaci, ale musí být omezený na čtení:
/// `.exe` je balík spravovaných knihoven, takže řetězec z něj jde vytáhnout, a jeden klíč platí
/// pro všechny zákazníky naráz (rozhodnutí Jan 19. 8.).
/// </summary>
public class TemplateChannelSettings
{
    /// <summary>Vypnuto = aplikace se kanálu neptá a chová se jako dřív (šablony jen z .exe).</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://podpora.m-line.cz";

    /// <summary>Klíč pro ČTENÍ. Prázdný = kanál se přeskočí (tichá degradace, ne chyba).</summary>
    public string ApiKey { get; set; } = "";
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

    /// <summary>Model pro AI Vision import PDF (default: claude-sonnet-5, adaptivní myšlení).
    /// Temperature se u Sonnet 5+ neposílá (guard v AnthropicApiClient.ModelSupportsTemperature).</summary>
    public string VisionModel { get; set; } = "claude-sonnet-5";

    /// <summary>Model pro AI pomocníka (průvodce) a generátor šablon (default: claude-sonnet-5).
    /// Pomocník volá s vypnutým myšlením (rychlé odpovědi), generátor s adaptivním (kvalita JSON).</summary>
    public string AssistantModel { get; set; } = "claude-sonnet-5";
}
