using System;
using System.IO;
using System.Text.Json;

namespace EDISONMAIN2;

/// <summary>
/// Lokální preference per Windows-user, uložené v %APPDATA%\m-line\EDISONMAIN2\local.json.
/// Pro nové preference přidávej nullable property — chybějící klíče v existujícím souboru
/// zůstanou null, kompatibilita zachována. Změny se persistují přes <see cref="Save"/>.
/// </summary>
public class LocalPrefs
{
    public string? LastIdfa { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "EDISONMAIN2", "local.json");

    /// <summary>Singleton instance loaded při startu aplikace přes <see cref="Load"/>.</summary>
    public static LocalPrefs Current { get; private set; } = new();

    /// <summary>Načte preference ze souboru. Při chybě/absenci ponechá default instanci.</summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<LocalPrefs>(json, JsonOptions) ?? new();
        }
        catch
        {
            // Korruptovaný soubor — držíme default instanci, příští Save() ji přepíše.
        }
    }

    /// <summary>Uloží aktuální stav do souboru. Vytvoří složku pokud chybí.</summary>
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
