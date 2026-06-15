using System.IO;
using System.Text.Json;

namespace EDISONMAIN2;

/// <summary>Konfigurace ze <c>appsettings.json</c> vedle exe, případně přepsaná z <c>appsettings.local.json</c>.</summary>
public class SettingsLoader
{
    public ServerSettings Server { get; set; } = new();

    public static SettingsLoader? Current { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string MainPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// First-launch indikátor (feature #5): chybí <c>appsettings.json</c> → stanice není
    /// nakonfigurovaná → ukázat setup wizard. Klíčuje na existenci souboru, ne na hodnotě
    /// (<see cref="ServerSettings.Host"/> má dev default, takže „prázdný host" nikdy nenastane).
    /// </summary>
    public static bool Exists() => File.Exists(MainPath);

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

    /// <summary>Zapíše <c>appsettings.json</c> vedle exe (setup wizard). UTF-8, bez BOM.</summary>
    public void Save()
    {
        File.WriteAllText(MainPath, JsonSerializer.Serialize(this, WriteOptions), new System.Text.UTF8Encoding(false));
        Current = this;
    }
}

public class ServerSettings
{
    public string Host { get; set; } = "192.168.16.1";
    public int    Port { get; set; } = 57770;
}
