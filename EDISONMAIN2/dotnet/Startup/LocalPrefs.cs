using System.IO;
using System.Text.Json;

namespace EDISONMAIN2;

/// <summary>
/// Lokální preference per Windows-user. Injectable abstrakce — produkce
/// <see cref="LocalPrefs"/> persistuje do <c>%APPDATA%\m-line\EDISONMAIN2\local.json</c>,
/// testy substituují in-memory implementaci.
/// </summary>
public interface ILocalPrefs
{
    string? LastIdfa { get; set; }
    bool ShowWindowsAsDialogs { get; set; }
    bool LoadBoards { get; set; }

    /// <summary>
    /// Per-stanice: zkusit při startu Windows SSO auto-login (jméno = <c>Environment.UserName</c>,
    /// prázdné heslo) před zobrazením login dialogu. Zapnout na stanicích s LDAP-AUTO uživateli;
    /// default false (EDISON/LDAP deploymenty → rovnou dialog, žádný zbytečný failed-login pokus).
    /// </summary>
    bool TryWindowsAutoLogin { get; set; }
    void Save();
}

/// <summary>
/// Diskové persistované preference. Pro nové preference přidávej nullable property
/// (chybějící klíče v existujícím souboru zůstanou null). Composition root
/// (<see cref="AppBootstrapper"/>) vytvoří jednu instanci přes <see cref="Load"/>
/// a propaguje ji do consumerů (CompanySwitcher, EdisonMainViewModel).
/// </summary>
public class LocalPrefs : ILocalPrefs
{
    public string? LastIdfa { get; set; }

    // Uživatel → Možnosti (replikuje legacy frmNoMDI checkboxy). Pro EDISONMAIN2 zatím
    // bez behaviorálního efektu kromě LoadBoards (řídí viditelnost WebView2 s nástěnkou).
    public bool ShowWindowsAsDialogs { get; set; }
    public bool LoadBoards { get; set; } = true;
    public bool TryWindowsAutoLogin { get; set; }

    // FontApplied/FontFamily/FontSize byly přesunuty do EDControls.UserFonts.UserFontPreference
    // (shared %APPDATA%\m-line\common\font.json) aby je TemplateDesigner mohl sdílet a reagovat
    // na změnu fontu v shellu přes FileSystemWatcher.

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "EDISONMAIN2", "local.json");

    /// <summary>Načte preference z disku. Poškozený / chybějící soubor → defaultní instance.</summary>
    public static LocalPrefs Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<LocalPrefs>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
