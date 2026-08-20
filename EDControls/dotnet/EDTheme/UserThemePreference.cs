using System;
using System.IO;
using System.Text.Json;

namespace EDControls.EDTheme;

/// <summary>
/// Sdílená volba vzhledu (zatím tmavý režim) napříč všemi m-line WPF nástroji -
/// uložená v <c>%APPDATA%\m-line\common\theme.json</c> (stejný vzor jako
/// <see cref="EDControls.UserFonts.UserFontPreference"/>). Při změně jedním procesem se přes
/// <see cref="FileSystemWatcher"/> propaguje do ostatních (shell ↔ TemplateDesigner) - viz <see cref="Changed"/>.
///
/// Storage je úmyslně v "common" (ne app-specific), aby dark mode nebyl vázaný na jednu aplikaci.
/// Registruje se přes <see cref="ThemeService.Initialize"/>.
/// </summary>
public class UserThemePreference
{
    /// <summary>Tmavý režim UI. Default false = světlý.</summary>
    public bool DarkMode { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Cíl persistence. Settable jen kvůli testům (jinak by šly proti reálnému %APPDATA%).</summary>
    internal static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "common", "theme.json");

    private static string Dir => Path.GetDirectoryName(FilePath)!;

    public static UserThemePreference Current { get; private set; } = new();

    /// <summary>Vyvolá se po změně <c>theme.json</c> z jiného procesu. Marshal na UI thread řeší subscriber (ThemeService).</summary>
    internal static event Action? Changed;

    internal static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<UserThemePreference>(json, JsonOptions) ?? new();
        }
        catch
        {
            // Poškozený soubor - držíme default, další Save() ho přepíše.
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static FileSystemWatcher? _watcher;

    /// <summary>Spustí watcher na <c>theme.json</c>; při změně načte a vyvolá <see cref="Changed"/>. Idempotentní.</summary>
    internal static void BeginListening()
    {
        if (_watcher != null) return;
        Directory.CreateDirectory(Dir);
        _watcher = new FileSystemWatcher(Dir, Path.GetFileName(FilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Krátká pauza - soubor může být v půlce zápisu (stejná heuristika jako UserFontPreference).
        try { System.Threading.Thread.Sleep(50); Load(); Changed?.Invoke(); }
        catch { /* sharing violation / poškozený JSON - příští event dožene */ }
    }
}
