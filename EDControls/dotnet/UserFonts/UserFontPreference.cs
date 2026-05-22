using System;
using System.IO;
using System.Text.Json;

namespace EDControls.UserFonts;

/// <summary>
/// User-zvolený font (FontFamily + FontSize + toggle Apply) sdílený napříč všemi
/// m-line WPF nástroji — uložen v <c>%APPDATA%\m-line\common\font.json</c>.
/// Při změně shellem se přes <see cref="FileSystemWatcher"/> propaguje do otevřených
/// procesů (TemplateDesigner atd.) — viz <see cref="Changed"/> event.
///
/// Storage je úmyslně v "common" složce (ne EDISONMAIN2-specific), aby preference
/// nezůstala vázaná na konkrétní aplikaci. Pro novou m-line apku stačí zavolat
/// <see cref="Load"/> + <see cref="FontService.Apply"/> + <see cref="FontService.BeginListening"/>.
/// </summary>
public class UserFontPreference
{
    /// <summary>Pokud true, <see cref="FontFamily"/> a <see cref="FontSize"/> se aplikují přes <see cref="FontService"/>.</summary>
    public bool FontApplied { get; set; }

    /// <summary>Název rodiny fontu, např. "Calibri". Null nebo prázdný = nepoužít user override.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Velikost základního UI (Body) v pt. Ostatní velikosti (Small/Caption/Header/Large) se škálují proporcionálně.</summary>
    public double? FontSize { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "common");

    private static readonly string FilePath = Path.Combine(Dir, "font.json");

    public static UserFontPreference Current { get; private set; } = new();

    /// <summary>
    /// Vyvolá se po změně <c>font.json</c> z jiného procesu (typicky shell změnil user font).
    /// Marshal na UI thread je odpovědnost subscriberu — viz <see cref="FontService.Initialize"/>.
    /// </summary>
    internal static event Action? Changed;

    internal static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<UserFontPreference>(json, JsonOptions) ?? new();
        }
        catch
        {
            // Poškozený soubor — držíme default, další Save() ho přepíše.
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static FileSystemWatcher? _watcher;

    /// <summary>
    /// Spustí <see cref="FileSystemWatcher"/> na adresáři. Při změně <c>font.json</c> načte aktuální
    /// hodnoty a vyvolá <see cref="Changed"/>. Idempotentní — opakované volání nic neudělá.
    /// Listener je odpovědný za marshal na UI thread (FileSystemWatcher používá thread pool).
    /// </summary>
    internal static void BeginListening()
    {
        if (_watcher != null) return;
        Directory.CreateDirectory(Dir);
        _watcher = new FileSystemWatcher(Dir, "font.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Krátká pauza — soubor může být ještě v půlce zápisu (truncate před content write).
        // 50ms je heuristika; pokud reload selže, další event v sekvenci to dožene.
        try { System.Threading.Thread.Sleep(50); Load(); Changed?.Invoke(); }
        catch { /* sharing violation nebo poškozený JSON — ignorovat, příští event dožene */ }
    }
}
