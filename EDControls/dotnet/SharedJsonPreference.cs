using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace EDControls;

/// <summary>
/// JSON preference v souboru, který čte a píše víc procesů najednou (shell, TemplateDesigner):
/// Load, Save a <see cref="FileSystemWatcher"/>, který po zápisu souboru znovu načte
/// <see cref="Current"/> a vyvolá <see cref="Changed"/>. Veřejné API nad ní drží tenké wrappery
/// <c>UserFontPreference</c> (font.json) a <c>UserThemePreference</c> (theme.json).
/// </summary>
internal sealed class SharedJsonPreference<T> : IDisposable where T : class, new()
{
    // Soubory čtou i samostatně nasazené starší exe - tvar na disku musí zůstat stejný.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Func<T, T> _afterLoad;
    private FileSystemWatcher? _watcher;

    /// <param name="afterLoad">Úprava načtené hodnoty (migrace schématu); běží uvnitř ochrany Load.</param>
    internal SharedJsonPreference(string filePath, Func<T, T>? afterLoad = null)
    {
        FilePath = filePath;
        _afterLoad = afterLoad ?? (value => value);
    }

    /// <summary>Settable jen kvůli testům (jinak by šly proti reálnému %APPDATA%).</summary>
    internal string FilePath { get; set; }

    internal T Current { get; private set; } = new();

    /// <summary>Po každém zápisu souboru (i vlastním procesem) z thread poolu - marshal na UI thread řeší subscriber.</summary>
    internal event Action? Changed;

    private string Dir => Path.GetDirectoryName(FilePath)!;

    internal void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            Current = _afterLoad(JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new());
        }
        catch
        {
            // Poškozený soubor - držíme poslední hodnotu, další Save() ho přepíše.
        }
    }

    internal void Save(T value)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(value, JsonOptions));
    }

    /// <summary>Spustí watcher na <see cref="FilePath"/>. Idempotentní - opakované volání nic neudělá.</summary>
    internal void BeginListening()
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

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Soubor může být ještě v půlce zápisu (truncate před content write) a jeden zápis hlásí
        // víc eventů; 50 ms je heuristika - nepovedený reload dožene další event v sekvenci.
        try { Thread.Sleep(50); Load(); Changed?.Invoke(); }
        catch { /* sharing violation nebo poškozený JSON - ignorovat, příští event dožene */ }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
