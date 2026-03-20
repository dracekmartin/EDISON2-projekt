using System.IO;
using System.Text.Json;

namespace EDISONMAIN2.Services;

/// <summary>
/// Konfigurace aplikace načítaná z appsettings.json.
/// Soubor leží vedle spustitelného souboru (AppContext.BaseDirectory).
/// </summary>
public class SettingsLoader
{
    public ServerSettings Server { get; set; } = new();

    /// <summary>Singleton instance načtená při startu aplikace.</summary>
    public static SettingsLoader? Current { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    /// <summary>
    /// Načte appsettings.json ze složky vedle exe.
    /// Pokud existuje appsettings.local.json, přepíše hodnoty z něj (pro lokální konfiguraci).
    /// Pokud soubor neexistuje nebo je poškozený, vrátí výchozí instanci.
    /// </summary>
    public static SettingsLoader Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "appsettings.json");
        var localPath = Path.Combine(baseDir, "appsettings.local.json");

        SettingsLoader? settings = null;
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                settings = JsonSerializer.Deserialize<SettingsLoader>(json, JsonOptions);
            }
            catch { /* ignore */ }
        }

        settings ??= new SettingsLoader();

        if (File.Exists(localPath))
        {
            try
            {
                var localJson = File.ReadAllText(localPath, System.Text.Encoding.UTF8);
                var local = JsonSerializer.Deserialize<SettingsLoader>(localJson, JsonOptions);
                if (local is not null && !string.IsNullOrEmpty(local.Server.Host))
                    settings.Server = local.Server;
            }
            catch { /* ignore */ }
        }

        Current = settings;
        return Current;
    }
}

/// <summary>Nastavení připojení k Caché / IRIS serveru přes MLRest.</summary>
public class ServerSettings
{
    /// <summary>IP adresa nebo hostname serveru.</summary>
    public string Host { get; set; } = "192.168.16.1";

    /// <summary>Port MLRest serveru.</summary>
    public int Port { get; set; } = 57770;
}
