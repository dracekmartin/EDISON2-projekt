using System.Diagnostics;
using System.Text.Json;
using EDControls.UserPrefs;

namespace TemplateDesigner.Services;

/// <summary>
/// Per-user × IDFa state pro TemplateDesigner. Wrapper nad <see cref="IUserPrefsService"/>
/// s typed API. Schémata blobů a registr scope ownerships viz
/// <c>EDControls/DOCS/05-userdata-scopes.md</c>.
///
/// <para>
/// Scope rozdělení:
/// <list type="bullet">
///   <item><c>"TemplateDesigner"</c> — <see cref="MainState"/> (MainStatePref), <see cref="RecentFiles"/> (List&lt;string&gt;)</item>
///   <item><c>"TemplateDesigner.InstanceDefaults"</c> — per-template instance selection (key = template Guid)</item>
/// </list>
/// </para>
///
/// <para>
/// Lifecycle: <see cref="LoadAsync"/> jednou při startu (App.OnStartup, fire-and-forget +
/// krátký sync wait pro warm cache). Po dokončení vystřelí <see cref="Loaded"/> event —
/// subscribery musí dispatchnout na UI thread. Save je fire-and-forget — UI nesmí padnout
/// kvůli Caché chybě.
/// </para>
/// </summary>
public class TdPreferencesService
{
    private const string Scope                 = "TemplateDesigner";
    private const string MainStateKey          = "MainState";
    private const string RecentFilesKey        = "RecentFiles";
    private const string InstanceDefaultsScope = "TemplateDesigner.InstanceDefaults";

    private const int MaxRecentFiles = 10;

    private readonly IUserPrefsService _prefs;

    public TdPreferencesService(IUserPrefsService prefs) => _prefs = prefs;

    /// <summary>Aktuální MainState — defaults dokud neproběhl <see cref="LoadAsync"/>.</summary>
    public MainStatePref MainState { get; private set; } = MainStatePref.Default;

    /// <summary>Aktuální Recent Files — prázdný dokud neproběhl <see cref="LoadAsync"/>.</summary>
    public List<string> RecentFiles { get; private set; } = [];

    /// <summary>True jakmile LoadAsync dokončí (úspěch i fallback na defaults).</summary>
    public bool IsLoaded { get; private set; }

    private event Action? _loadedHandlers;
    private readonly object _loadedLock = new();

    /// <summary>
    /// Vystřelí po dokončení <see cref="LoadAsync"/> (z threadpool threadu — subscriber
    /// MUSÍ dispatchnout na UI thread). Subscribe po dokončení loadu vyvolá handler
    /// okamžitě (synchronní fast-path pro warm-cache scénář).
    /// </summary>
    public event Action Loaded
    {
        add
        {
            bool invokeNow;
            lock (_loadedLock)
            {
                invokeNow = IsLoaded;
                if (!invokeNow) _loadedHandlers += value;
            }
            if (invokeNow) value();
        }
        remove { lock (_loadedLock) _loadedHandlers -= value; }
    }

    /// <summary>Načte všechny scope/key bloby. Volat jednou při startu.</summary>
    public async Task LoadAsync()
    {
        MainState   = await LoadBlobAsync<MainStatePref>(Scope, MainStateKey)   ?? MainStatePref.Default;
        RecentFiles = await LoadBlobAsync<List<string>>(Scope, RecentFilesKey) ?? [];

        Action? handlers;
        lock (_loadedLock)
        {
            IsLoaded = true;
            handlers = _loadedHandlers;
            _loadedHandlers = null;
        }
        handlers?.Invoke();
    }

    private async Task<T?> LoadBlobAsync<T>(string scope, string key) where T : class
    {
        try
        {
            var node = await _prefs.LoadAsync(scope, key);
            return node?.Deserialize<T>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TdPreferences] Load failed ({scope}/{key}): {ex.Message}");
            return null;
        }
    }

    /// <summary>Uloží MainState (atomicky, celý blob). Fire-and-forget — chyby se logují, neházejí.</summary>
    public Task SaveMainStateAsync(MainStatePref state)
    {
        MainState = state;
        return SaveBlobAsync(Scope, MainStateKey, state);
    }

    /// <summary>
    /// Přidá soubor na začátek Recent Files (dedup case-insensitive, cap na <see cref="MaxRecentFiles"/>)
    /// a uloží. Volat z Open / SaveAs handlerů.
    /// </summary>
    public Task AddRecentFileAsync(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        return SaveBlobAsync(Scope, RecentFilesKey, RecentFiles);
    }

    /// <summary>Odebere soubor z Recent Files (volá se po pokusu otevřít missing soubor).</summary>
    public Task RemoveRecentFileAsync(string path)
    {
        int removed = RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        return removed > 0 ? SaveBlobAsync(Scope, RecentFilesKey, RecentFiles) : Task.CompletedTask;
    }

    /// <summary>Vrátí naposledy zvolené instance pro danou šablonu, nebo prázdný list.</summary>
    public async Task<List<string>> LoadInstanceDefaultsAsync(Guid templateId)
        => await LoadBlobAsync<List<string>>(InstanceDefaultsScope, templateId.ToString()) ?? [];

    /// <summary>Uloží vybrané instance pro šablonu.</summary>
    public Task SaveInstanceDefaultsAsync(Guid templateId, IReadOnlyList<string> instanceIds)
        => SaveBlobAsync(InstanceDefaultsScope, templateId.ToString(), instanceIds);

    private async Task SaveBlobAsync<T>(string scope, string key, T value)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(value);
            if (node is null) return;
            await _prefs.SaveAsync(scope, key, node);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TdPreferences] Save failed ({scope}/{key}): {ex.Message}");
        }
    }
}

/// <summary>
/// Hlavní per-user state. Všechny pole mají default — chybějící v starém blobu → použije se default
/// (System.Text.Json + record primary ctor). Při breaking change (rename / změna typu) přidat
/// <c>int Version</c> a migration logiku v LoadBlobAsync (viz 05-userdata-scopes.md §4).
/// </summary>
public record MainStatePref(
    float Zoom               = 1.0f,
    bool ShowGrid            = false,
    bool SnapToGrid          = false,
    bool SnapToElements      = true,
    bool ShowSectionHeaders  = true,
    bool ShowRuler           = false,
    float GridSize           = 5f,
    float SnapGapMm          = 2f,
    bool ShowPreviewPane     = true,
    double? LeftPanelWidthPx    = null,
    double? PreviewPanelWidthPx = null,
    int ActiveLeftTabIndex   = 0)
{
    public static readonly MainStatePref Default = new();
}
