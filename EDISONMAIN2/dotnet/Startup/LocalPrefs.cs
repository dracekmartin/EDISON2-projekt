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

    // Uživatel → Možnosti (replikuje legacy frmNoMDI checkboxy). Pro EDISONMAIN2 zatím
    // bez behaviorálního efektu kromě LoadBoards (řídí viditelnost WebView2 s nástěnkou).
    public bool ShowWindowsAsDialogs { get; set; }
    public bool ShowMiniApps { get; set; }
    public bool LoadBoards { get; set; } = true;

    // Styl — user font. FontApplied=true → font se aplikuje na MainWindow při startu/apply.
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public bool FontApplied { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "EDISONMAIN2", "local.json");

    public static LocalPrefs Current { get; private set; } = new();

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
            // Poškozený soubor — držíme default instanci, příští Save() ji přepíše.
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
