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

    /// <summary>Poslední přihlašovací jméno — předvyplní se v login dialogu. Null = žádné.</summary>
    string? LastLoginName { get; set; }

    /// <summary>Zapamatované heslo posledního uživatele — DPAPI blob (viz <see cref="PasswordProtector"/>).
    /// Null = nezapamatováno. Předvyplní se jen když jméno uložené v blobu == <see cref="LastLoginName"/>.</summary>
    string? RememberedPassword { get; set; }

    bool ShowWindowsAsDialogs { get; set; }

    /// <summary>Uložený layout miniaplikací (AvalonDock XML) per IDFa. Null = žádný uložený.</summary>
    Dictionary<string, string>? MiniAppLayouts { get; set; }

    /// <summary>Uložená šířka stromu modulů (px) per IDFa. Null = žádná uložená.</summary>
    Dictionary<string, double>? TreeWidths { get; set; }

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
    public string? LastLoginName { get; set; }
    public string? RememberedPassword { get; set; }

    // Uživatel → Možnosti — ShowWindowsAsDialogs zatím bez behaviorálního efektu (uloženo pro budoucnost).
    public bool ShowWindowsAsDialogs { get; set; }
    public bool TryWindowsAutoLogin { get; set; }

    public Dictionary<string, string>? MiniAppLayouts { get; set; }
    public Dictionary<string, double>? TreeWidths { get; set; }

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
