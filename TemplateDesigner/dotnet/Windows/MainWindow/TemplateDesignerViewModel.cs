using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Config;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using ALWA.SYS;

namespace TemplateDesigner;

// ── Align mode enum (public so code-behind can reference it) ─────────────────
public enum AlignMode { Left, Right, CenterH, Top, Bottom, MiddleV, DistributeH, DistributeV }
public enum DuplicateDirection { Up, Down, Left, Right }

// ─────────────────────────────────────────────────────────────────────────────
// Main ViewModel
// ─────────────────────────────────────────────────────────────────────────────

public class TemplateDesignerViewModel : EDViewModel
{
    // ── Designer state (public — code-behind accesses canvas/resize directly) ─
    public DesignerState State { get; } = new();

    // ── File & path ───────────────────────────────────────────────────────────
    public string? CurrentFilePath { get; private set; }

    // ── Dirty tracking ──────────────────────────────────────────────────────
    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set { if (_isDirty != value) { _isDirty = value; RequestTitleUpdate?.Invoke(); } }
    }

    // ── Providers ─────────────────────────────────────────────────────────────
    public  IDataSchemaProvider    SchemaProvider        { get; }
    public  CacheAgendaProvider?   CacheProvider         { get; }
    public  ITableTemplateProvider? TableTemplateProvider => CacheProvider;
    public  Services.CacheTemplateService? TemplateStorage { get; }

    /// <summary>Pasivní AI průvodce (záložka v levém panelu). Čte živý kontext při každém dotazu.</summary>
    public  Windows.AiGuideDialog.AiGuidePanelViewModel AiGuide { get; }
    private readonly SectionTemplateManager _sectionTemplates;

    // ── Preview params ────────────────────────────────────────────────────────
    private List<TemplateParameter>             _agendaParameters    = [];
    private readonly Dictionary<string,object?> _previewParams       = new();
    private Dictionary<string,object?>?         _cachedParamValues;
    // Poslední hodnota server-side parametru rozdělení sloupců — změna vyžaduje re-fetch dat z Caché
    // (grouping počítá server), na rozdíl od client-side parametrů, které jen překreslí.
    private string?                             _lastServerParamRozdeleni;
    private string?                             _lastParamsAgendaCode;

    // ── Active instances (linky pro MHD, faktury pro FAKTURY, ...) ────────────
    // Mimo Template — session state vázané na aktuální agendu. Preview rendruje vždy
    // jednu (aktivní index), tisk batch projíždí všechny.
    private List<AgendaInstance> _activeInstances = [];
    public IReadOnlyList<AgendaInstance> ActiveInstances => _activeInstances;

    /// <summary>Zvolená skupina linek (SKUPINA_LINEK_ID) — filtr instancí. Null = server default
    /// (první nezablokovaná). Session scope (mimo Template), reset při změně agendy. Propaguje se
    /// do pickeru i do GetData (aby se linka fetchla ze správné skupiny).</summary>
    private string? _activeSkupinaId;

    private int _activeInstanceIndex;
    public int ActiveInstanceIndex
    {
        get => _activeInstanceIndex;
        set
        {
            if (_activeInstances.Count == 0) { _activeInstanceIndex = 0; return; }
            var clamped = Math.Clamp(value, 0, _activeInstances.Count - 1);
            if (_activeInstanceIndex != clamped)
            {
                _activeInstanceIndex = clamped;
                OnActiveInstancesChanged(reloadData: true);
            }
        }
    }

    public AgendaInstance? CurrentActiveInstance =>
        _activeInstances.Count > 0 && _activeInstanceIndex < _activeInstances.Count
            ? _activeInstances[_activeInstanceIndex] : null;

    public string ActiveInstanceText
    {
        get
        {
            var lbl = CapitalizeFirst(CurrentInstanceLabel);
            if (_activeInstances.Count == 0) return $"{lbl}: — (klikni pro výběr)";
            var cur = CurrentActiveInstance;
            return _activeInstances.Count == 1
                ? $"{lbl}: {cur?.Display}"
                : $"{lbl}: {cur?.Display} ({_activeInstanceIndex + 1}/{_activeInstances.Count})";
        }
    }

    public bool HasMultipleActiveInstances => _activeInstances.Count > 1;

    /// <summary>Popisek zvolené skupiny linek pro status (prázdné = default/žádná skupina).</summary>
    private string? _activeSkupinaLabel;

    /// <summary>Status chip skupiny — viditelný jen když je skupina zvolená.</summary>
    public string ActiveSkupinaText => string.IsNullOrEmpty(_activeSkupinaLabel)
        ? "" : $"Skupina: {_activeSkupinaLabel}";

    public bool HasActiveSkupina => !string.IsNullOrEmpty(_activeSkupinaLabel);

    /// <summary>Vyčistí aktivní instance — volá se při změně agendy / načtení šablony s jinou agendou.</summary>
    private void ClearActiveInstances()
    {
        _activeInstances = [];
        _activeSkupinaId = null;
        _activeSkupinaLabel = null;
        OnPropertyChanged(nameof(ActiveSkupinaText));
        OnPropertyChanged(nameof(HasActiveSkupina));
        _activeInstanceIndex = 0;
        // reloadData:true záměrně i když instance byly prázdné: vyčistí i _realPreviewData
        // (vázaná na starou instanci/agendu — jinak se stará data renderují přes novou
        // šablonu = "zmizelé" preview) a bumpne generaci → invaliduje in-flight refresh.
        // RefreshDataForCurrentInstanceAsync sám hned vrátí (žádná aktivní instance).
        OnActiveInstancesChanged(reloadData: true);
    }

    /// <summary>Generace preview dat — chrání proti dosednutí zastaralého async GetData
    /// (přepnu agendu/instanci, zatímco běží předchozí fetch staré). Bump při každé změně.</summary>
    private int _previewDataGeneration;

    private void OnActiveInstancesChanged(bool reloadData)
    {
        _previewDataGeneration++;
        OnPropertyChanged(nameof(ActiveInstanceText));
        OnPropertyChanged(nameof(CurrentActiveInstance));
        OnPropertyChanged(nameof(HasMultipleActiveInstances));
        if (reloadData)
        {
            // Invalidate cached real data, fetch nového v pozadí
            _realPreviewData = null;
            _realPreviewIndex = 0;
            OnPropertyChanged(nameof(RealPreviewPageCount));
            OnPropertyChanged(nameof(RealPreviewIndex));
            OnPropertyChanged(nameof(PageNavigationText));
            OnPropertyChanged(nameof(HasMultiplePages));
            RequestPreviewRefresh?.Invoke();
            _ = RefreshDataForCurrentInstanceAsync();
        }
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Caché GetData pro aktuální instanci → _realPreviewData. Async, fire-and-forget.</summary>
    /// <summary>„Obnovit data" (F5 / tlačítko ↻ u instance chipu): vyčistí cache provideru a znovu
    /// načte schéma, parametry i tisková data aktuální instance — data změněná na serveru (značky,
    /// poznámky) se projeví bez restartu.</summary>
    private void RefreshData()
    {
        _previewDataGeneration++;               // invaliduje případný běžící fetch → guard proti dosednutí stale dat
        CacheProvider?.InvalidateCaches();
        RefreshFieldsList();                    // schéma polí + parametry (teď fresh)
        _ = RefreshDataForCurrentInstanceAsync(); // re-fetch GetData pro aktuální linku
    }

    private async Task RefreshDataForCurrentInstanceAsync()
    {
        var instance = CurrentActiveInstance;
        if (CacheProvider is null || instance is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        int gen = _previewDataGeneration;
        try
        {
            StatusText = $"Načítám data: {instance.Display}…";
            var paramName = InstanceParamName(agendaCode);
            // Print params (vč. server-side, např. ROZDELENI_SLOUPCU) → GetData; instance přebíjí klíč linky.
            var getDataParams = new Dictionary<string, object?>(_previewParams, StringComparer.OrdinalIgnoreCase)
            {
                [paramName] = instance.Value
            };
            // Skupina linek musí do GetData — u AD je linka klíčovaná (skupina, linka), jinak by se
            // fetchla z jiné/default skupiny než z které byla vybraná.
            if (!string.IsNullOrEmpty(_activeSkupinaId)) getDataParams["SKUPINA_LINEK_ID"] = _activeSkupinaId;
            var pages = await Task.Run(() => CacheProvider.GetData(agendaCode, getDataParams));

            // Mezitím se přepnula agenda/instance (nebo se vyčistilo) → tento výsledek je
            // zastaralý a nesmí dosednout do nové šablony.
            if (gen != _previewDataGeneration) return;

            if (pages is null || pages.Count == 0)
            {
                _realPreviewData = null;
                StatusText = $"Žádná data: {instance.Display}";
            }
            else
            {
                var culture = GetTemplateCulture();
                _realPreviewData = pages.Select(d => d.WithCulture(culture)).ToList();
                _realPreviewIndex = 0;
                StatusText = $"{instance.Display}: {pages.Count} stránek";
            }
            OnPropertyChanged(nameof(RealPreviewPageCount));
            OnPropertyChanged(nameof(RealPreviewIndex));
            OnPropertyChanged(nameof(PageNavigationText));
            OnPropertyChanged(nameof(HasMultiplePages));
            RequestPreviewRefresh?.Invoke();
        }
        catch (Exception ex)
        {
            if (gen != _previewDataGeneration) return;
            StatusText = $"⚠ Chyba načítání: {ex.Message}";
        }
    }

    /// <summary>Registr agend (capability metadata z Caché) — lookup InstanceParamName / ElementKind dle kódu.</summary>
    private readonly IReadOnlyList<AgendaRegistryEntry> _agendaEntries;

    private AgendaRegistryEntry? AgendaEntry(string? agendaCode) =>
        string.IsNullOrEmpty(agendaCode) ? null
        : _agendaEntries.FirstOrDefault(a => a.Code.Equals(agendaCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>Jméno IN parametru GetData pro danou agendu (z registru; default INSTANCE_ID) —
    /// místo hardcoded větvení podle názvu agendy.</summary>
    private string InstanceParamName(string agendaCode) => AgendaEntry(agendaCode)?.InstanceParamName ?? "INSTANCE_ID";

    /// <summary>Instance-param aktuální agendy (LINKA_ID / INSTANCE_ID) — čte code-behind (PrintDialog).</summary>
    public string CurrentInstanceParamName => InstanceParamName(State.CurrentTemplate.AgendaCode ?? "");

    /// <summary>Druh JŘ elementů aktuální agendy (STOP/LINE/"") — řídí kontextové menu + paletu okna.</summary>
    public string CurrentAgendaElementKind => AgendaEntry(State.CurrentTemplate.AgendaCode)?.ElementKind ?? "";

    /// <summary>Zobrazovací název aktuální konfigurace „Agenda · Varianta" (např. „Zastávkový JŘ · AD");
    /// fallback = syrový kód (registr nedostupný / neznámý kód). Čte titulek okna.</summary>
    public string CurrentAgendaDisplayName =>
        AgendaEntry(State.CurrentTemplate.AgendaCode)?.DisplayName ?? State.CurrentTemplate.AgendaCode ?? "";

    /// <summary>User-facing název „instance" aktuální agendy (JŘ = „linka", faktury = „faktura",
    /// jinak „instance") — z registru agend. Okno ho zobrazí místo obecného „instance".</summary>
    public string CurrentInstanceLabel       => AgendaEntry(State.CurrentTemplate.AgendaCode)?.InstanceLabel ?? "instance";
    public string CurrentInstanceLabelPlural => AgendaEntry(State.CurrentTemplate.AgendaCode)?.InstanceLabelPlural ?? "instance";

    /// <summary>Tooltipy navigace/výběru s doménovým názvem (např. „Předchozí linka", „…výběr — linky").</summary>
    public string PreviousInstanceTooltip => $"Předchozí {CurrentInstanceLabel}";
    public string NextInstanceTooltip     => $"Další {CurrentInstanceLabel}";
    public string InstancePickerHint      => $"Klikni pro výběr — {CurrentInstanceLabelPlural}";

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private void NotifyInstanceLabelsChanged()
    {
        OnPropertyChanged(nameof(CurrentInstanceLabel));
        OnPropertyChanged(nameof(CurrentInstanceLabelPlural));
        OnPropertyChanged(nameof(ActiveInstanceText));
        OnPropertyChanged(nameof(PreviousInstanceTooltip));
        OnPropertyChanged(nameof(NextInstanceTooltip));
        OnPropertyChanged(nameof(InstancePickerHint));
    }

    // ── Real preview data ────────────────────────────────────────────────────
    private List<DataContext>?                  _realPreviewData;
    private int                                _realPreviewIndex;

    /// <summary>Počet dostupných stránek reálných dat (0 = mock mód).</summary>
    public int RealPreviewPageCount => _realPreviewData?.Count ?? 0;

    /// <summary>Index aktuálně zobrazené stránky reálných dat (0-based).</summary>
    public int RealPreviewIndex
    {
        get => _realPreviewIndex;
        set
        {
            if (_realPreviewData is null || _realPreviewData.Count == 0) return;
            var clamped = Math.Clamp(value, 0, _realPreviewData.Count - 1);
            if (Set(ref _realPreviewIndex, clamped))
            {
                RequestPreviewRefresh?.Invoke();
                OnPropertyChanged(nameof(PageNavigationText));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Text stránkování "3 / 12" pro UI.</summary>
    public string PageNavigationText => RealPreviewPageCount > 0
        ? $"{RealPreviewIndex + 1} / {RealPreviewPageCount}"
        : "—";

    public bool HasMultiplePages => RealPreviewPageCount > 1;

    private RelayCommand? _prevPage, _nextPage;
    public RelayCommand PreviousPageCommand => _prevPage ??= Command(
        () => RealPreviewIndex--,
        () => RealPreviewIndex > 0);
    public RelayCommand NextPageCommand => _nextPage ??= Command(
        () => RealPreviewIndex++,
        () => RealPreviewIndex < RealPreviewPageCount - 1);

    /// <summary>Parametry agendy pro PrintDialog.</summary>
    public IReadOnlyList<TemplateParameter> AgendaParameters => _agendaParameters;

    /// <summary>
    /// Sdílený dictionary parametrů (preview lišta + PrintDialog operují nad stejnou instancí).
    /// Hodnoty se persistují přes <see cref="LoadPrintParamsAndApplyAsync"/> a <see cref="SavePrintParamsAsync"/>.
    /// </summary>
    public Dictionary<string, object?> PreviewParamValues => _previewParams;

    // ── Clipboard ─────────────────────────────────────────────────────────────
    private List<TemplateElement> _clipboard = [];

    // ── Observable collections (drive XAML data-binding) ──────────────────────
    public ObservableCollection<SectionVM>          Sections          { get; } = [];
    public ObservableCollection<SchemaGroupVM>      SchemaGroups      { get; } = [];
    public ObservableCollection<ParameterControlVM> PreviewParameters { get; } = [];

    // ── Bindable properties ───────────────────────────────────────────────────
    public bool CanUndo => State.CanUndo;
    public bool CanRedo => State.CanRedo;

    private string _statusText = "";
    /// <summary>Transientní „akce/progres" zóna status baru (uloženo / chyba / export N/N / migrace).
    /// Odděleno od <see cref="SelectionStatusText"/>, aby se navzájem nepřepisovaly — akční hláška
    /// zůstane viditelná i když uživatel mezitím klikne jinam a změní se stav výběru.</summary>
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private string _selectionStatusText = "";
    /// <summary>Klidová „stav výběru" zóna status baru (aktuální element / sekce). Píše ji jen
    /// <see cref="UpdateStatusForSelection"/>; mění se volně s výběrem, nepřepíše akční hlášku.</summary>
    public string SelectionStatusText
    {
        get => _selectionStatusText;
        set => Set(ref _selectionStatusText, value);
    }

    private bool _hasPreviewOverflow;
    /// <summary>True = obsah některé stránky preview se nevejde. Status bar zobrazí červený blok.</summary>
    public bool HasPreviewOverflow
    {
        get => _hasPreviewOverflow;
        set => Set(ref _hasPreviewOverflow, value);
    }

    private string _agendaNameText = "Agenda: —";
    public string AgendaNameText
    {
        get => _agendaNameText;
        set => Set(ref _agendaNameText, value);
    }

    private bool _hasParameters;
    public bool HasParameters
    {
        get => _hasParameters;
        private set => Set(ref _hasParameters, value);
    }

    public bool ShowGrid
    {
        get => State.ShowGrid;
        set { State.ShowGrid = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public bool ShowSectionHeaders
    {
        get => State.ShowSectionHeaders;
        set { State.ShowSectionHeaders = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); RequestCanvasSizeUpdate?.Invoke(); }
    }

    public bool ShowRuler
    {
        get => State.ShowRuler;
        set { State.ShowRuler = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public bool SnapToGrid
    {
        get => State.SnapToGrid;
        set { State.SnapToGrid = value; OnPropertyChanged(); }
    }

    public bool SnapToElements
    {
        get => State.SnapToElements;
        set { State.SnapToElements = value; OnPropertyChanged(); }
    }

    public float GridSize
    {
        get => State.GridSize;
        set { State.GridSize = value; OnPropertyChanged(); RequestCanvasRefresh?.Invoke(); }
    }

    public float SnapGapMm
    {
        get => State.SnapGapMm;
        set { State.SnapGapMm = value; OnPropertyChanged(); }
    }

    // ── Callbacks wired by code-behind ────────────────────────────────────────
    public Action? RequestCanvasRefresh      { get; set; }
    public Action? RequestTitleUpdate        { get; set; }
    public Action? RequestCanvasSizeUpdate   { get; set; }
    public Action? RequestPropertiesPanelUpdate { get; set; }
    public Action? RequestPreviewRefresh     { get; set; }
    /// <summary>Sync GridSize ComboBox v toolbaru po načtení šablony (ComboBox není bindovaný).</summary>
    public Action<float>? RequestGridSizeUiSync { get; set; }

    /// <summary>Nemodální hláška při chybě čtení z Caché (schema/params/mock/instance). Code-behind
    /// ji marshalne na UI thread (může fírnout z background Task.Run) a zobrazí ve status baru.</summary>
    public Action<string>? RequestReadErrorStatus { get; set; }

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired after Sections is rebuilt so code-behind can restore ListBox selection.</summary>
    public event Action? SectionsRebuilt;

    // ── Commands ──────────────────────────────────────────────────────────────
    private RelayCommand? _undo, _redo, _delete, _copy, _paste, _cut, _copyStyle, _pasteStyle, _openInstancePicker, _prevInstance, _nextInstance, _refreshData;
    private AsyncRelayCommand? _save, _saveAs, _open, _openFromCache, _saveToCache, _saveToCacheAs, _importJson, _exportJson, _exportPdf, _exportPng, _new, _changeAgenda;

    public RelayCommand UndoCommand          => _undo      ??= Command(Undo,          () => CanUndo);
    public RelayCommand RedoCommand          => _redo      ??= Command(Redo,          () => CanRedo);
    // DB je primární úložiště: Uložit / Otevřít / Uložit jako jdou do databáze. JSON = import/export.
    public AsyncRelayCommand SaveCommand     => _save      ??= CommandAsync(SaveTemplate);
    public AsyncRelayCommand SaveAsCommand   => _saveAs    ??= CommandAsync(SaveToCacheAsAsync, () => TemplateStorage is not null);
    public AsyncRelayCommand NewCommand      => _new       ??= CommandAsync(NewTemplateAsync);
    public AsyncRelayCommand OpenCommand     => _open      ??= CommandAsync(OpenFromCacheAsync, () => TemplateStorage is not null);
    public AsyncRelayCommand ImportJsonCommand => _importJson ??= CommandAsync(ImportFromJsonAsync);
    public AsyncRelayCommand ExportJsonCommand => _exportJson ??= CommandAsync(ExportToJsonAsync);
    public AsyncRelayCommand OpenFromCacheCommand  => _openFromCache  ??= CommandAsync(OpenFromCacheAsync,  () => TemplateStorage is not null);
    public AsyncRelayCommand SaveToCacheCommand    => _saveToCache    ??= CommandAsync(SaveToCacheAsync,    () => TemplateStorage is not null);
    public AsyncRelayCommand SaveToCacheAsCommand  => _saveToCacheAs  ??= CommandAsync(SaveToCacheAsAsync,  () => TemplateStorage is not null);
    public AsyncRelayCommand ChangeAgendaCommand   => _changeAgenda   ??= CommandAsync(ChangeAgendaAsync);
    public RelayCommand OpenInstancePickerCommand  => _openInstancePicker ??= Command(OpenInstancePicker);
    public RelayCommand PreviousInstanceCommand    => _prevInstance   ??= Command(() => ActiveInstanceIndex--, () => HasMultipleActiveInstances && _activeInstanceIndex > 0);
    public RelayCommand NextInstanceCommand        => _nextInstance   ??= Command(() => ActiveInstanceIndex++, () => HasMultipleActiveInstances && _activeInstanceIndex < _activeInstances.Count - 1);
    public RelayCommand RefreshDataCommand         => _refreshData    ??= Command(RefreshData);
    public AsyncRelayCommand ExportPdfCommand => _exportPdf ??= CommandAsync(ExportPreviewPdfAsync, () => !_exportRunning);
    public AsyncRelayCommand ExportPngCommand => _exportPng ??= CommandAsync(ExportPreviewPngAsync, () => !_exportRunning);
    public new RelayCommand DeleteCommand => _delete    ??= Command(DeleteSelected);
    public RelayCommand CopyCommand     => _copy      ??= Command(CopySelected);
    public RelayCommand CutCommand      => _cut       ??= Command(CutSelected);
    public RelayCommand PasteCommand    => _paste     ??= Command(PasteClipboard);
    public RelayCommand CopyStyleCommand  => _copyStyle  ??= Command(CopyStyle);
    public RelayCommand PasteStyleCommand => _pasteStyle ??= Command(PasteStyle, () => State.StyleClipboard is not null);

    // ── Constructor ───────────────────────────────────────────────────────────
    /// <summary>
    /// Per-user state (zoom, grid, splittery, recent files, instance defaults).
    /// Null v testech / standalone bez Caché — get pro UI binding loaded files menu;
    /// service interní fallback na defaults i bez Caché.
    /// </summary>
    public TdPreferencesService? Preferences { get; }

    public TemplateDesignerViewModel(MLRest? mlRest = null,
                                     TdPreferencesService? preferences = null,
                                     IReadOnlyList<AgendaRegistryEntry>? agendas = null,
                                     string? startupAgendaCode = null)
    {
        if (mlRest is not null) _services.MLRest = new MLRestAdapter(mlRest);

        Preferences = preferences;

        var settings   = AppSettings.Load();
        var schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
        _sectionTemplates = new SectionTemplateManager();

        var resolvedMlRest = mlRest ?? CacheSchemaWiring.TryCreateMlRest(settings);
        var resolvedAgendas = agendas ?? [];
        _agendaEntries = resolvedAgendas;
        CacheProvider = resolvedMlRest is not null && resolvedAgendas.Count > 0
            ? CacheSchemaWiring.TryCreate(resolvedMlRest, resolvedAgendas)
            : null;
        // Chyba čtení z Caché → nemodální hláška ve status baru (code-behind marshalne na UI thread).
        if (CacheProvider is not null)
            CacheProvider.OnReadError = msg => RequestReadErrorStatus?.Invoke(msg);
        TemplateStorage = resolvedMlRest is not null
            ? new Services.CacheTemplateService(resolvedMlRest)
            : null;

        SchemaProvider = CacheProvider is not null
            ? new CompositeDataSchemaProvider(CacheProvider, new JsonFileDataSchemaProvider(schemasDir))
            : (IDataSchemaProvider)new JsonFileDataSchemaProvider(schemasDir);

        State.CurrentTemplate = CreateSampleTemplate();

        // Startovní agenda — priorita: (1) EDISONMAIN2 kontrakt (inputParam "Agenda=<kód>" dle
        // uzlu stromu shellu / MHD2 tlačítka), (2) poslední použitá agenda uživatele (per-user
        // prefs), (3) fallback ze CreateSampleTemplate. Neznámý kód se přeskočí.
        string? ResolveKnown(string? code) => code is null ? null
            : resolvedAgendas.FirstOrDefault(a =>
                string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase))?.Code;
        var startAgenda = ResolveKnown(startupAgendaCode)
                          ?? ResolveKnown(preferences?.MainState?.LastAgendaCode);
        if (startAgenda is not null) State.CurrentTemplate.AgendaCode = startAgenda;

        RebuildSections();

        AiGuide = new Windows.AiGuideDialog.AiGuidePanelViewModel(() =>
        {
            var code = State.CurrentTemplate.AgendaCode;
            var name = code is null ? null
                : SchemaProvider.GetAvailableAgendas()
                      .FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase))?.Name;
            var selType = State.SelectedElements.Count == 1
                ? State.SelectedElements[0].GetType().Name : null;
            return new Windows.AiGuideDialog.AiGuideContext(
                code, name, selType, State.CurrentTemplate.Sections.Count, CurrentAgendaElementKind);
        });

        // Aplikuj loaded MainState na State + VM properties. Volá se po inicializaci State,
        // aby loaded hodnoty přepsaly defaults z DesignerState ctoru.
        ApplyLoadedPreferences();
    }

    /// <summary>
    /// Aplikuje MainStatePref z <see cref="Preferences"/> na DesignerState + bindovatelné VM properties.
    /// Volá se v konstruktoru a znovu po dokončení LoadAsync (event Loaded) — pro cold-start scénář
    /// kdy LoadAsync nestihl dokončit před Show().
    /// </summary>
    public void ApplyLoadedPreferences()
    {
        var pref = Preferences?.MainState;
        if (pref is null) return;

        State.Zoom               = pref.Zoom;
        State.ShowGrid           = pref.ShowGrid;
        State.SnapToGrid         = pref.SnapToGrid;
        State.SnapToElements     = pref.SnapToElements;
        State.ShowSectionHeaders = pref.ShowSectionHeaders;
        State.ShowRuler          = pref.ShowRuler;
        State.GridSize           = pref.GridSize;
        State.SnapGapMm          = pref.SnapGapMm;

        // Property notifications pro bindings (toolbar toggle buttony)
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(SnapToGrid));
        OnPropertyChanged(nameof(SnapToElements));
        OnPropertyChanged(nameof(ShowSectionHeaders));
        OnPropertyChanged(nameof(ShowRuler));
        OnPropertyChanged(nameof(GridSize));
        OnPropertyChanged(nameof(SnapGapMm));
    }

    /// <summary>
    /// Snapshot aktuálního state z DesignerState do MainStatePref a uloží do Caché.
    /// Volá se z MainWindow.OnClosing. Splitter šířky a tab index doplňuje code-behind
    /// (VM nezná konkrétní WPF kontrolky).
    /// </summary>
    public Task SaveMainStateAsync(double? leftPanelPx, double? previewPanelPx, int activeTabIndex, bool showPreviewPane)
    {
        if (Preferences is null) return Task.CompletedTask;

        var snapshot = new MainStatePref(
            Zoom:               State.Zoom,
            ShowGrid:           State.ShowGrid,
            SnapToGrid:         State.SnapToGrid,
            SnapToElements:     State.SnapToElements,
            ShowSectionHeaders: State.ShowSectionHeaders,
            ShowRuler:          State.ShowRuler,
            GridSize:           State.GridSize,
            SnapGapMm:          State.SnapGapMm,
            ShowPreviewPane:    showPreviewPane,
            LeftPanelWidthPx:   leftPanelPx,
            PreviewPanelWidthPx: previewPanelPx,
            ActiveLeftTabIndex: activeTabIndex,
            LastAgendaCode:     State.CurrentTemplate.AgendaCode);

        return Preferences.SaveMainStateAsync(snapshot);
    }

    // ── Execute wrapper ───────────────────────────────────────────────────────
    public void Execute(IDesignerCommand cmd)
    {
        State.Execute(cmd);
        IsDirty = true;
        NotifyUndoRedo();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    /// <summary>Vykoná seznam příkazů jako jeden undo krok (1 příkaz → přímo, víc → CompositeCommand).
    /// Prázdný seznam = no-op.</summary>
    private void ExecuteAsComposite(string label, List<IDesignerCommand> cmds)
    {
        if (cmds.Count == 0) return;
        Execute(cmds.Count == 1 ? cmds[0] : new CompositeCommand(label, cmds));
    }

    // ── Aplikace výsledků editorů sloupců (dialog otevírá code-behind; sestavení
    //    a vykonání příkazů = business logika, patří sem do VM). Metody neznají WPF. ──

    /// <summary>Odjezdová mřížka: tab „Obecné" (EDInputForm) + per-column styly + styl sloupce hodin
    /// jako jeden undo krok.</summary>
    public void ApplyDepartureGridEdit(DepartureGridElement element, IDesignerCommand? generalCommand,
        IEnumerable<IDesignerCommand> styleCommands, List<DepartureGroupColumnStyle> resultGroupColumns,
        bool hasGroups, ColumnVisualStyle? resultHourColumnStyle)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.AddRange(styleCommands);
        if (hasGroups)
            cmds.Add(new SetPropertyCommand("Skupiny dnů", element.GroupColumns, resultGroupColumns,
                v => element.GroupColumns = (List<DepartureGroupColumnStyle>)v!));
        cmds.Add(new SetPropertyCommand("Styl sloupce hodin", element.HourColumnStyle, resultHourColumnStyle,
            v => element.HourColumnStyle = (ColumnVisualStyle?)v));
        ExecuteAsComposite("Nastavení odjezdů", cmds);
    }

    /// <summary>Zastávkový StopList: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    /// <summary>Zvýraznění řádků (seznam pravidel) — jeden undoable krok. Prázdný seznam → null (čistý JSON).</summary>
    public void ApplyHighlightRulesEdit(IRowHighlightable element, List<HighlightRule> resultRules)
    {
        List<HighlightRule>? newVal = resultRules.Count > 0 ? resultRules : null;
        Execute(new SetPropertyCommand("Zvýraznění řádků", element.HighlightRules, newVal,
            v => element.HighlightRules = (List<HighlightRule>?)v));
    }

    public void ApplyStopColumnsEdit(StopListElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce zastávek", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!));
        ExecuteAsComposite("Nastavení zastávek", cmds);
    }

    /// <summary>Linkový JŘ LineTable: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyLineTableColumnsEdit(LineTableElement element, IDesignerCommand? generalCommand, List<StopColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce linkového JŘ", element.Columns, resultColumns,
            v => element.Columns = (List<StopColumn>)v!));
        ExecuteAsComposite("Nastavení linkového JŘ", cmds);
    }

    /// <summary>Dynamická tabulka: tab „Obecné" + sloupce jako jeden undo krok.</summary>
    public void ApplyDynamicTableEdit(DynamicTableElement element, IDesignerCommand? generalCommand, List<TableColumn> resultColumns)
    {
        var cmds = new List<IDesignerCommand>();
        if (generalCommand is not null) cmds.Add(generalCommand);
        cmds.Add(new SetPropertyCommand("Sloupce tabulky", element.Columns, resultColumns,
            v => element.Columns = (List<TableColumn>)v!));
        ExecuteAsComposite("Nastavení tabulky", cmds);
    }

    /// <summary>Statická tabulka (Fixed/Dynamic): sloupce + řádky (Fixed) + souhrnné řádky jako jeden undo krok.</summary>
    public void ApplyTableEdit(TableElement element, List<TableColumn> resultColumns, List<TableRow>? resultRows, List<TableRow>? resultSummaryRows)
    {
        var cmds = new List<IDesignerCommand>
        {
            new SetPropertyCommand("Sloupce tabulky", element.Columns, resultColumns,
                v => element.Columns = (List<TableColumn>)v!)
        };
        if (element.Mode == TableMode.Fixed)
            cmds.Add(new SetPropertyCommand("Řádky tabulky", element.Rows, resultRows,
                v => element.Rows = (List<TableRow>?)v));
        cmds.Add(new SetPropertyCommand("Souhrnné řádky", element.SummaryRows, resultSummaryRows,
            v => element.SummaryRows = (List<TableRow>?)v));
        ExecuteAsComposite("Upravit tabulku", cmds);
    }

    private void Undo()
    {
        State.Undo();
        IsDirty = true;
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    private void Redo()
    {
        State.Redo();
        IsDirty = true;
        CleanupSelection();
        NotifyUndoRedo();
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    /// <summary>
    /// Odebere z výběru elementy, které už nejsou v žádné sekci šablony
    /// (např. po undo AddElement nebo undo Split).
    /// </summary>
    private void CleanupSelection()
    {
        var allElements = State.CurrentTemplate.Sections.SelectMany(s => s.Elements).ToHashSet();
        State.SelectedElements.RemoveAll(e => !allElements.Contains(e));
    }

    private void NotifyUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    // ── Sections ──────────────────────────────────────────────────────────────
    public void RebuildSections()
    {
        // Sync ambientního dokumentového defaultu (font…) na aktuální šablonu. RebuildSections se volá
        // po každém (re)loadu šablony → ambient vždy odpovídá aktuální šabloně (i reset na null u nové).
        DocumentDefaults.Style = State.CurrentTemplate.DefaultTextStyle;

        Sections.Clear();
        foreach (var s in State.CurrentTemplate.Sections)
            Sections.Add(new SectionVM(s, this));
        SectionsRebuilt?.Invoke();
    }

    public void ActivateSection(Section? section)
    {
        State.ActiveSection = section;
        RebuildSections();
        RequestCanvasRefresh?.Invoke();
    }

    public void AddSection(Section section)
    {
        var commands = new List<IDesignerCommand>();

        // Uvolnit místo pro novou sekci — zmenšit existující fixed-height sekce
        float needed = section.DesignerHeight ?? 30f;
        var resizable = State.CurrentTemplate.Sections
            .Where(s => s.DesignerHeight.HasValue && s.DesignerHeight.Value > 5f)
            .ToList();

        if (resizable.Count > 0 && needed > 0)
        {
            float totalResizable = resizable.Sum(s => s.DesignerHeight!.Value);
            if (totalResizable > needed)
            {
                foreach (var s in resizable)
                {
                    float oldH = s.DesignerHeight!.Value;
                    float share = oldH / totalResizable * needed;
                    float newH = MathF.Max(5f, oldH - share);
                    if (Math.Abs(oldH - newH) > 0.01f)
                        commands.Add(new SetPropertyCommand(
                            $"Výška sekce {s.Name}", oldH, newH,
                            v => s.DesignerHeight = (float)v!));
                }
            }
        }

        commands.Add(new AddSectionCommand(State.CurrentTemplate, section));

        if (commands.Count > 1)
            Execute(new CompositeCommand("Přidat sekci", commands));
        else
            Execute(commands[0]);

        ActivateSection(section);
        RequestCanvasSizeUpdate?.Invoke();
    }

    public void MoveSection(Section section, int direction)
    {
        int idx    = State.CurrentTemplate.Sections.IndexOf(section);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= State.CurrentTemplate.Sections.Count) return;

        Execute(new MoveSectionCommand(State.CurrentTemplate, section, direction));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void ReorderSection(Section section, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || toIndex < 0) return;
        if (toIndex >= State.CurrentTemplate.Sections.Count) return;

        Execute(new ReorderSectionCommand(State.CurrentTemplate, section, fromIndex, toIndex));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void DeleteSection(Section section)
    {
        if (State.CurrentTemplate.Sections.Count <= 1)
        {
            PopupDisplayer.Info("Šablona musí mít alespoň jednu sekci.");
            return;
        }

        int elementCount = section.Elements.Count;
        if (elementCount > 0)
        {
            if (!PopupDisplayer.Confirm($"Opravdu smazat sekci „{section.Name}“ s {elementCount} elementy?"))
                return;
        }

        State.SelectedElements.RemoveAll(el => section.Elements.Contains(el));

        if (State.ActiveSection == section)
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault(s => s != section);

        var commands = new List<IDesignerCommand>();
        commands.Add(new DeleteSectionCommand(State.CurrentTemplate, section));

        // Redistribuovat uvolněnou výšku mezi zbývající fixed-height sekce
        float freed = section.DesignerHeight ?? 0f;
        var remaining = State.CurrentTemplate.Sections
            .Where(s => s != section && s.DesignerHeight.HasValue)
            .ToList();

        if (freed > 0 && remaining.Count > 0)
        {
            float totalRemaining = remaining.Sum(s => s.DesignerHeight!.Value);
            foreach (var s in remaining)
            {
                float oldH = s.DesignerHeight!.Value;
                float share = totalRemaining > 0 ? oldH / totalRemaining * freed : freed / remaining.Count;
                float newH = oldH + share;
                if (Math.Abs(newH - oldH) > 0.01f)
                    commands.Add(new SetPropertyCommand(
                        $"Výška sekce {s.Name}", oldH, newH,
                        v => s.DesignerHeight = (float)v!));
            }
        }

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Smazat sekci", commands));
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    public void SaveSectionTemplate(Section section, string name)
    {
        try
        {
            _sectionTemplates.Save(section, name);
            PopupDisplayer.Info($"Šablona „{name}“ byla uložena.");
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    public IReadOnlyList<string> GetCustomSectionTemplateNames() =>
        _sectionTemplates.GetAvailableTemplates();

    public Section? LoadCustomSectionTemplate(string name) =>
        _sectionTemplates.Load(name)?.Clone();

    // ── File operations ───────────────────────────────────────────────────────
    private async Task NewTemplateAsync()
    {
        if (!await ConfirmUnsavedChangesAsync()) return;
        ReplaceWithBlankTemplate(agendaCode: null);
    }

    /// <summary>
    /// Sdílený helper pro &quot;clean slate&quot;: nahradí aktivní šablonu prázdnou
    /// (volitelně se zadanou agendou) a resetuje veškerý designový stav.
    /// Volající MUSÍ předtím zavolat <see cref="ConfirmUnsavedChanges"/>.
    /// </summary>
    private void ReplaceWithBlankTemplate(string? agendaCode, Action<Template>? configure = null)
    {
        var t = new Template();
        if (agendaCode is not null) t.AgendaCode = agendaCode;
        // Volitelná konfigurace před přidáním výchozí sekce (např. převzetí velikosti/okrajů
        // stránky při resetu z editoru vlastností stránky) — sekce se přidá až s finální PageSize.
        configure?.Invoke(t);
        State.CurrentTemplate = t;
        // Výchozí sekce, ať uživatel nezačíná na prázdné šabloně bez plochy k editaci.
        State.CurrentTemplate.Sections.Add(CreateDefaultSection(State.CurrentTemplate.PageSize));
        CurrentFilePath = null;
        _loadedFromCache = false;
        IsDirty = false;
        // Aktivní instance jsou agenda-specifické — při změně agendy ztrácí smysl.
        ClearActiveInstances();
        State.SelectedElements.Clear();
        State.UndoStack.Clear();
        State.RedoStack.Clear();
        NotifyUndoRedo();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        RebuildSections();
        RefreshFieldsList();
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Potvrzení destruktivní změny velikosti/orientace stránky. Změna vždy vymaže obsah a založí
    /// čisté plátno, proto se ptáme i u uložené šablony (jinak by ConfirmUnsavedChanges u čisté
    /// šablony vrátil true bez ptaní → tichá ztráta obsahu). Neuložené změny → Save/Discard/Cancel.
    /// </summary>
    public async Task<bool> ConfirmPageResetAsync()
    {
        if (IsDirty)
            return await ConfirmUnsavedChangesAsync();
        return PopupDisplayer.Confirm(
            "Změna velikosti nebo orientace stránky vymaže obsah šablony a založí čisté plátno. Pokračovat?");
    }

    /// <summary>
    /// Reset šablony po změně velikosti/orientace stránky: potvrzení → čisté plátno s novou
    /// velikostí. Zachová agendu (PageEdit ji needituje); převezme velikost/okraje/kulturu/font
    /// z hodnot dialogu (klíč = property path na Template). Vrátí false pokud uživatel reset zrušil.
    /// </summary>
    public async Task<bool> ResetForPageChangeAsync(IReadOnlyDictionary<string, string> pageFields)
    {
        if (!await ConfirmPageResetAsync()) return false;

        var agenda = State.CurrentTemplate.AgendaCode;
        ReplaceWithBlankTemplate(agenda, configure: t =>
        {
            foreach (var (key, val) in pageFields)
                if (ElementPropertyAccessor.Exists(t, key))
                    ElementPropertyAccessor.Set(t, key, val);
        });
        return true;
    }

    private void OpenInstancePicker()
    {
        if (CacheProvider is null)
        {
            PopupDisplayer.Info("Databáze není nakonfigurována — výběr instancí není dostupný.");
            return;
        }
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode))
        {
            PopupDisplayer.Info("Nejprve vyberte agendu (Ctrl+Shift+A).");
            return;
        }

        // Skupiny linek pro filtr v pickeru (prázdné = agenda bez skupin → filtr skrytý). Rychlý lookup.
        var skupiny = CacheProvider.GetAvailableSkupiny(agendaCode);
        var vm = new InstancePickerDialogViewModel(agendaCode, _activeInstances, skupiny, _activeSkupinaId, CurrentInstanceLabelPlural);
        if (Dialogs?.ShowInstancePicker(vm) != true) return;

        _activeSkupinaId = vm.SelectedSkupinaId;
        _activeSkupinaLabel = vm.SelectedSkupinaLabel;
        _activeInstances = vm.SelectedInstances.ToList();
        _activeInstanceIndex = 0;
        OnActiveInstancesChanged(reloadData: true);
        OnPropertyChanged(nameof(ActiveSkupinaText));
        OnPropertyChanged(nameof(HasActiveSkupina));

        // Persist výběr pro tuto šablonu — při příštím otevření se předvyplní.
        if (Preferences is not null)
        {
            var ids = _activeInstances.Select(i => i.Value).ToList();
            _ = Preferences.SaveInstanceDefaultsAsync(State.CurrentTemplate.Id, ids);
        }
    }

    private async Task ChangeAgendaAsync()
    {
        // Registr agend (dvouosý: agenda × varianta) — dialog staví seskupený výběr jen ze
        // ZVEŘEJNĚNÝCH agend (Released; pilotní jako Linkový JŘ / Faktury se uživatelům nenabízí).
        // Aktuální agenda šablony se ukáže vždy — i nezveřejněná (interní/vývojová šablona).
        var current = State.CurrentTemplate.AgendaCode;
        var visible = _agendaEntries
            .Where(a => a.Released || a.Code.Equals(current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (visible.Count == 0)
        {
            PopupDisplayer.Info("Žádné agendy nejsou nakonfigurované.");
            return;
        }

        var vm = new ChangeAgendaDialogViewModel(visible, current);
        if (Dialogs?.ShowChangeAgenda(vm) != true || vm.ChosenCode is null) return;

        // Stejná agenda — nic neměním (vyhne se zbytečnému potvrzování neuložených změn).
        if (string.Equals(State.CurrentTemplate.AgendaCode, vm.ChosenCode, StringComparison.OrdinalIgnoreCase))
            return;

        if (!await ConfirmUnsavedChangesAsync()) return;
        ReplaceWithBlankTemplate(vm.ChosenCode);
    }

    /// <summary>Import šablony z JSON souboru. Načtená šablona se chová jako nová neuložená (původ
    /// = není v DB), takže následný Uložit ji založí do databáze.</summary>
    private async Task ImportFromJsonAsync()
    {
        if (!await ConfirmUnsavedChangesAsync()) return;
        var path = Dialogs?.PickOpenFile("Importovat šablonu z JSON", TemplateSerializer.FileFilter);
        if (path is null) return;
        await OpenTemplateFromFileAsync(path);
    }

    /// <summary>
    /// Otevře šablonu ze zadané cesty. Společná implementace pro File→Open a Recent Files menu.
    /// </summary>
    public async Task OpenTemplateFromFileAsync(string filePath)
    {
        try
        {
            var oldAgenda = State.CurrentTemplate.AgendaCode;
            State.CurrentTemplate = await TemplateSerializer.LoadAsync(filePath);
            // Pokud nová šablona má jinou agendu, vyčistit aktivní instance — jejich
            // IDs patří staré agendě a v nové by GetData padl.
            if (!string.Equals(oldAgenda, State.CurrentTemplate.AgendaCode, StringComparison.OrdinalIgnoreCase))
                ClearActiveInstances();
            CurrentFilePath = filePath;
            _loadedFromCache = false;
            IsDirty = false;
            State.SelectedElements.Clear();
            State.UndoStack.Clear();
            State.RedoStack.Clear();
            NotifyUndoRedo();
            ApplyEditorPreferencesFromTemplate();
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
            RebuildSections();
            RefreshFieldsList();
            await TryLoadInstanceDefaultsAsync();
            await LoadPrintParamsAndApplyAsync();
            if (Preferences is not null) await Preferences.AddRecentFileAsync(filePath);
            NotifyMigrationIfNeeded();
            RequestTitleUpdate?.Invoke();
            RequestCanvasSizeUpdate?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        catch (TemplateDeserializationException ex)
        {
            PopupDisplayer.Error(ex);
        }
        catch (FileNotFoundException)
        {
            PopupDisplayer.Info($"Soubor nenalezen: {filePath}");
            if (Preferences is not null) await Preferences.RemoveRecentFileAsync(filePath);
        }
    }

    /// <summary>
    /// Načte uložený výběr instancí pro aktuální šablonu (pokud existují a CacheProvider je k dispozici).
    /// </summary>
    private async Task TryLoadInstanceDefaultsAsync()
    {
        if (Preferences is null || CacheProvider is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        var savedIds = await Preferences.LoadInstanceDefaultsAsync(State.CurrentTemplate.Id);
        if (savedIds.Count == 0) return;

        try
        {
            var available = await Task.Run(() => CacheProvider.GetAvailableInstances(agendaCode!, null));
            _activeInstances = available.Where(i => savedIds.Contains(i.Value)).ToList();
            _activeInstanceIndex = 0;
            OnActiveInstancesChanged(reloadData: true);
        }
        catch
        {
            // Saved instances nejsou kompatibilní s aktuálním stavem DB — ignorujeme tiše
        }
    }

    /// <summary>
    /// Pokud byla aktuální šablona při loadu auto-migrována (Template.WasMigrated=true),
    /// zobrazí warning ve status baru. Uložením se přepíše stará verze v souboru/Caché.
    /// </summary>
    private void NotifyMigrationIfNeeded()
    {
        if (State.CurrentTemplate.WasMigrated)
        {
            StatusText = $"⚠ Šablona migrována na nový formát (V{State.CurrentTemplate.Version}). Uložením přepíšete starou verzi.";
            IsDirty = true; // user musí explicitně uložit, aby se nová verze propsala
        }
    }

    /// <summary>Aplikuje grid/snap nastavení uložené v šabloně. Null hodnoty → ponechat aktuální.</summary>
    private void ApplyEditorPreferencesFromTemplate()
    {
        var prefs = State.CurrentTemplate.EditorPreferences;
        if (prefs is null) return;
        // VM proxy settery firnou PropertyChanged → bound toggle buttony (ShowGrid, SnapToGrid,
        // SnapToElements) se aktualizují v toolbaru.
        if (prefs.GridSize       is { } gs) GridSize       = gs;
        if (prefs.SnapToGrid     is { } sg) SnapToGrid     = sg;
        if (prefs.SnapToElements is { } se) SnapToElements = se;
        if (prefs.ShowGrid       is { } sh) ShowGrid       = sh;
        // CmbGridSize není bindovaný (event-driven dropdown) — explicit sync přes callback.
        RequestGridSizeUiSync?.Invoke(State.GridSize);
    }

    /// <summary>Zaznamená aktuální grid/snap nastavení do šablony před uložením.</summary>
    private void CaptureEditorPreferencesToTemplate()
    {
        State.CurrentTemplate.EditorPreferences = new TemplateEditorPreferences
        {
            GridSize       = State.GridSize,
            SnapToGrid     = State.SnapToGrid,
            SnapToElements = State.SnapToElements,
            ShowGrid       = State.ShowGrid
        };
    }

    /// <summary>Loads a template that was generated externally (e.g. AI assistant).</summary>
    public void LoadGeneratedTemplate(Template template)
    {
        State.CurrentTemplate = template;
        State.SelectedElements.Clear();
        State.UndoStack.Clear();
        State.RedoStack.Clear();
        NotifyUndoRedo();
        State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
        CurrentFilePath = null;
        IsDirty = true;

        RebuildSections();
        InvalidateAll();
        RefreshFieldsList();
    }

    private void InvalidateAll()
    {
        RequestTitleUpdate?.Invoke();
        RequestCanvasSizeUpdate?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
        RequestPreviewRefresh?.Invoke();
    }

    private async Task SaveTemplate()
    {
        // DB je primární úložiště. DB šablona → update; nová/importovaná → SaveAs do DB
        // (SaveToCacheAsync interně řeší new→SaveAs). Bez Caché → popup s nabídkou JSON exportu (U5).
        if (TemplateStorage is null) { await OfferJsonExportFallbackAsync(); return; }
        await SaveToCacheAsync();
    }

    /// <summary>Fallback když DB není dostupná (U5): explicitní popup s nabídkou uložit jako JSON,
    /// místo tichého status baru. Vrátí true pokud se reálně exportovalo.</summary>
    private async Task<bool> OfferJsonExportFallbackAsync()
    {
        if (!PopupDisplayer.Confirm(
                "Databáze není dostupná — nelze uložit do ní. Uložit šablonu jako soubor JSON?"))
            return false;
        return await ExportToJsonAsync();
    }

    /// <summary>Export šablony do JSON souboru. NEmění původ (šablona dál patří do DB) — je to
    /// kopie ven, obdoba PDF/PNG exportu, ne „save target". Vrátí true pokud se soubor uložil.</summary>
    private async Task<bool> ExportToJsonAsync()
    {
        var path = Dialogs?.PickSaveFile("Exportovat šablonu do JSON", TemplateSerializer.FileFilter,
            State.CurrentTemplate.Name);
        if (path is null) return false;
        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        await TemplateSerializer.SaveAsync(State.CurrentTemplate, path);
        StatusText = $"Vyexportováno do {System.IO.Path.GetFileName(path)}";
        return true;
    }

    // ── Caché storage ─────────────────────────────────────────────────────────

    /// <summary>True pokud aktuální šablona byla naposledy načtená/uložená do Caché.</summary>
    private bool _loadedFromCache;

    /// <summary>True = šablona je uložená v databázi (Ctrl+S ji aktualizuje). False = nová/importovaná,
    /// ještě není v DB (Ctrl+S ji založí). Pro indikátor původu v titulku.</summary>
    public bool IsCacheBacked => _loadedFromCache;

    private async Task OpenFromCacheAsync()
    {
        if (TemplateStorage is null) return;

        // Confirmace neuložených změn AŽ po explicitním Otevřít — dialog funguje i jako
        // správce (smazat / přejmenovat) bez intent načíst do editoru.
        var vm = new CacheTemplatePickerDialogViewModel(TemplateStorage, _agendaEntries);
        if (Dialogs?.ShowCacheTemplatePicker(vm) != true || vm.SelectedTemplateId is null) return;

        if (!await ConfirmUnsavedChangesAsync()) return;

        try
        {
            var oldAgenda = State.CurrentTemplate.AgendaCode;
            // Systémová (M-line) šablona se čte z MLINE větve; jinak z firmy (default).
            var sourceIdFa = vm.SelectedIsSystem ? Services.CacheTemplateService.MlineIdFa : null;
            var (_, template) = await TemplateStorage.LoadAsync(vm.SelectedTemplateId.Value, sourceIdFa);
            State.CurrentTemplate = template;
            // Pokud nová šablona má jinou agendu, vyčistit aktivní instance — jejich
            // IDs patří staré agendě a v nové by GetData padl.
            if (!string.Equals(oldAgenda, State.CurrentTemplate.AgendaCode, StringComparison.OrdinalIgnoreCase))
                ClearActiveInstances();
            CurrentFilePath = null;
            // Systémová = read-only originál → otevřít jako „kopie": Ctrl+S vynutí „Uložit jako"
            // do firmy (nový GUID). Vlastní firma šablona → normální update.
            _loadedFromCache = !vm.SelectedIsSystem;
            IsDirty = false;
            State.SelectedElements.Clear();
            State.UndoStack.Clear();
            State.RedoStack.Clear();
            NotifyUndoRedo();
            ApplyEditorPreferencesFromTemplate();
            State.ActiveSection = State.CurrentTemplate.Sections.FirstOrDefault();
            RebuildSections();
            RefreshFieldsList();
            await TryLoadInstanceDefaultsAsync();
            await LoadPrintParamsAndApplyAsync();
            NotifyMigrationIfNeeded();
            RequestTitleUpdate?.Invoke();
            RequestCanvasSizeUpdate?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Uloží do DB (nová/z disku → Save As dialog). Vrátí true při úspěchu, false při
    /// zrušení (Save As dialog) nebo chybě — volající (confirm neuložených změn) podle toho pokračuje.</summary>
    private async Task<bool> SaveToCacheAsync()
    {
        if (TemplateStorage is null) return false;
        // Pokud šablona ještě v Caché není (z disku nebo nová) → spustit Save As
        if (!_loadedFromCache) return await SaveToCacheAsAsync();

        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        try
        {
            await TemplateStorage.SaveAsync(State.CurrentTemplate);
            IsDirty = false;
            RequestTitleUpdate?.Invoke();
            PopupDisplayer.Info($"Šablona „{State.CurrentTemplate.Name}\" byla uložena do databáze.");
            return true;
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); return false; }
    }

    private async Task<bool> SaveToCacheAsAsync()
    {
        if (TemplateStorage is null) return false;

        var vm = new SaveToCacheDialogViewModel(
            State.CurrentTemplate.Name,
            State.CurrentTemplate.AgendaCode);
        if (Dialogs?.ShowSaveToCache(vm) != true) return false;

        // Kolize názvu: bez kontroly by vznikla tichá duplicita (dvě šablony stejného jména,
        // v přehledu nerozlišitelné). Best-effort — když list selže, uložení neblokujeme.
        try
        {
            var existing = await TemplateStorage.ListAsync(vm.AgendaCode);
            var clash = existing.FirstOrDefault(t =>
                string.Equals(t.Name, vm.Name, StringComparison.OrdinalIgnoreCase)
                && t.TemplateId != State.CurrentTemplate.Id);
            if (clash is not null && !PopupDisplayer.Confirm(
                    $"Šablona „{vm.Name}\" už v databázi existuje (upravena {clash.ModifiedAt}).\n" +
                    "Uložit i tak jako další šablonu se stejným názvem?"))
                return false;
        }
        catch (CacheTemplateException) { /* kontrola je best-effort */ }

        // Pro "Save As" → nový TemplateID, aby nepřepsal originál v Caché
        State.CurrentTemplate.Id = Guid.NewGuid();
        State.CurrentTemplate.Name = vm.Name;
        State.CurrentTemplate.AgendaCode = vm.AgendaCode;

        SyncParametersToTemplate();
        CaptureEditorPreferencesToTemplate();
        try
        {
            await TemplateStorage.SaveAsync(State.CurrentTemplate);
            _loadedFromCache = true;
            CurrentFilePath = null;
            IsDirty = false;
            RequestTitleUpdate?.Invoke();
            PopupDisplayer.Info($"Šablona „{State.CurrentTemplate.Name}\" byla uložena do databáze.");
            return true;
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); return false; }
    }

    private void SyncParametersToTemplate()
    {
        State.CurrentTemplate.Parameters = _agendaParameters.Count > 0 ? [.. _agendaParameters] : null;
    }

    /// <summary>True během běžícího exportu — blokuje druhý souběžný export (PDF i PNG),
    /// jinak uživatel může dál normálně pracovat (export renderuje snapshot šablony).</summary>
    private bool _exportRunning;

    private async Task ExportPreviewPdfAsync()
    {
        var defaultName = (Path.GetFileNameWithoutExtension(CurrentFilePath)
                         ?? (string.IsNullOrWhiteSpace(State.CurrentTemplate.Name)
                             ? "sablona" : State.CurrentTemplate.Name)) + ".pdf";
        var path = Dialogs?.PickSaveFile("Exportovat náhled jako PDF", "PDF soubory (*.pdf)|*.pdf", defaultName, "pdf");
        if (path is null) return;
        if (RenderPdfToFile is null) return;

        // Imposice ze sdílených exportních polí (persistované per-agenda, editují se v PrintDialogu) —
        // zachytit na UI threadu spolu se snapshotem. Chybná mřížka = uživatelská konfigurace → status bar.
        SheetLayoutOptions? sheets;
        try { sheets = SheetComposer.FromParamValues(PreviewParamValues, State.CurrentTemplate.PageSize); }
        catch (ArgumentException ex) { StatusText = $"⚠ {ex.Message}"; return; }

        // Fetch (N× GetData přes MLRest) i render běží mimo UI thread (R1). UI zůstává
        // plně interaktivní — render jede nad SNAPSHOTEM šablony (stav v okamžiku kliknutí),
        // takže editace během exportu výstup neovlivní. Progress jde do status baru.
        _exportRunning = true;
        try
        {
            var snapshot = TemplateSerializer.DeepClone(State.CurrentTemplate);
            var fetchProgress = new Progress<string>(s => StatusText = s);
            var pages = await BuildAllPreviewDataPagesAsync(fetchProgress);

            var renderProgress = new Progress<(int done, int total)>(p =>
                StatusText = $"Generuji PDF: {p.done}/{p.total} datových stránek…");
            await Task.Run(() => RenderPdfToFile(path, pages, snapshot, renderProgress, sheets));

            StatusText = $"PDF uloženo: {Path.GetFileName(path)} ({pages.Count} datových stránek)"
                       + (sheets is null ? "" : $" · {sheets.ShortLabel}");
            OpenExportedFile(path);
        }
        finally { _exportRunning = false; }
    }

    /// <summary>View vrstva (code-behind) napojí WPF implementaci — modální/souborové dialogy + schránka.
    /// Přes tuto abstrakci VM nereferencuje WPF (R4). Null v testech → dialogové akce se tiše přeskočí.</summary>
    public IDesignerDialogService? Dialogs { get; set; }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPdf. Voláno z background threadu
    /// se snapshot šablonou (živá se může mezitím editovat). Poslední parametr = imposice (null = vypnutá).</summary>
    public Action<string, IReadOnlyList<DataContext>, Template, IProgress<(int done, int total)>, SheetLayoutOptions?>? RenderPdfToFile { get; set; }

    private async Task ExportPreviewPngAsync()
    {
        var defaultName = (Path.GetFileNameWithoutExtension(CurrentFilePath)
                         ?? (string.IsNullOrWhiteSpace(State.CurrentTemplate.Name)
                             ? "sablona" : State.CurrentTemplate.Name)) + ".png";
        var path = Dialogs?.PickSaveFile("Exportovat náhled jako PNG", "PNG soubory (*.png)|*.png", defaultName, "png");
        if (path is null) return;
        if (RenderPngToFile is null) return;

        _exportRunning = true;
        try
        {
            var snapshot = TemplateSerializer.DeepClone(State.CurrentTemplate);
            var fetchProgress = new Progress<string>(s => StatusText = s);
            var pages = await BuildAllPreviewDataPagesAsync(fetchProgress);

            var renderProgress = new Progress<(int done, int total)>(p =>
                StatusText = $"Generuji PNG: {p.done}/{p.total} stránek…");
            int count = await Task.Run(() => RenderPngToFile(path, pages, snapshot, renderProgress));

            StatusText = count == 1
                ? $"PNG uloženo: {Path.GetFileName(path)}"
                : $"PNG uloženo: {count} souborů ({Path.GetFileNameWithoutExtension(path)}_001.png …)";
            if (count == 1) OpenExportedFile(path);
        }
        finally { _exportRunning = false; }
    }

    /// <summary>Code-behind napojí na PreviewCanvas.RenderMultiDataToPng. Voláno z background threadu
    /// se snapshot šablonou (živá se může mezitím editovat).</summary>
    public Func<string, IReadOnlyList<DataContext>, Template, IProgress<(int done, int total)>, int>? RenderPngToFile { get; set; }

    private static void OpenExportedFile(string filePath)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch { /* viewer nemusí být nainstalován */ }
    }

    /// <summary>Potvrzení neuložených změn před destruktivní akcí (Nový/Otevřít/změna agendy/zavření).
    /// Ano → uloží do DB (stejná cesta jako Ctrl+S), Ne → pokračovat bez uložení, Storno → zrušit.
    /// Async, protože uložení do DB je async (MLRest). Vrátí true = lze pokračovat.</summary>
    public async Task<bool> ConfirmUnsavedChangesAsync()
    {
        if (!IsDirty) return true;

        var result = PopupDisplayer.ConfirmYesNoCancel(
            "Šablona obsahuje neuložené změny. Uložit je?",
            title: "Neuložené změny");

        return result switch
        {
            true  => await SaveAndContinueAsync(),  // Ano = uložit (do DB) a pokračovat
            false => true,                           // Ne = pokračovat bez uložení (zahodit změny)
            null  => false                           // Storno = zrušit
        };
    }

    /// <summary>Uloží do DB (jako Ctrl+S), ne do JSON souboru — DB je primární úložiště. Bez DB →
    /// popup s nabídkou JSON exportu (U5). Vrátí true pokud se reálně uložilo (jinak nepokračovat).</summary>
    private async Task<bool> SaveAndContinueAsync()
    {
        if (TemplateStorage is null) return await OfferJsonExportFallbackAsync();
        return await SaveToCacheAsync();
    }

    // ── Clipboard ─────────────────────────────────────────────────────────────
    public void CopySelected()
    {
        if (State.SelectedElements.Count == 0) return;
        _clipboard = State.SelectedElements.Select(el => el.Clone()).ToList();
    }

    public void CutSelected()
    {
        if (State.SelectedElements.Count == 0) return;
        CopySelected();
        DeleteSelected();
    }

    public void SelectAll()
    {
        if (State.ActiveSection is null) return;
        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(State.ActiveSection.Elements);
    }

    public void PasteClipboard()
    {
        if (_clipboard.Count == 0) return;
        if (State.ActiveSection is null)
        {
            PopupDisplayer.Info("Nejprve vyberte sekci.");
            return;
        }

        // Mapování starý GroupId → nový GroupId (klony nedědí skupinu originálu)
        var groupIdMap = new Dictionary<string, string>();
        var newElements = _clipboard.Select(el =>
        {
            var clone = el.Clone();
            clone.X = Math.Min(clone.X + 5f,
                State.CurrentTemplate.PageSize.PrintableWidth - clone.Width);
            clone.Y = Math.Min(clone.Y + 5f,
                State.ActiveSection.EffectiveDesignerHeight - clone.EffectiveDesignerHeight);
            RemapGroupId(clone, groupIdMap);
            return clone;
        }).ToList();

        var commands = newElements
            .Select(el => (IDesignerCommand)new AddElementCommand(State.ActiveSection, el))
            .ToList();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Vložit elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Style clipboard ──────────────────────────────────────────────────────

    public void CopyStyle()
    {
        var el = State.SelectedElements.FirstOrDefault();
        if (el is null) return;
        var textStyle = el is TextElement te ? te.Style.Clone() : null;
        State.StyleClipboard = new StyleClipboard(textStyle, el.BorderColor, el.BorderWidth, el.BackgroundColor);
    }

    public void PasteStyle()
    {
        var clip = State.StyleClipboard;
        if (clip is null || State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements)
        {
            if (el.BorderColor != clip.BorderColor)
                commands.Add(new SetPropertyCommand("Barva rámečku", el.BorderColor, clip.BorderColor,
                    v => { el.BorderColor = (string)v!; }));
            if (MathF.Abs(el.BorderWidth - clip.BorderWidth) > 0.001f)
                commands.Add(new SetPropertyCommand("Šířka rámečku", el.BorderWidth, clip.BorderWidth,
                    v => { el.BorderWidth = (float)v!; }));
            if (el.BackgroundColor != clip.BackgroundColor)
                commands.Add(new SetPropertyCommand("Barva pozadí", el.BackgroundColor, clip.BackgroundColor,
                    v => { el.BackgroundColor = (string)v!; }));

            if (clip.TextStyle is not null && el is TextElement te)
            {
                var oldStyle = te.Style;
                var newStyle = clip.TextStyle.Clone();
                commands.Add(new SetPropertyCommand("Styl textu", oldStyle, newStyle,
                    v => { te.Style = (TextStyle)v!; }));
            }
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Vložit styl", commands));
        RequestCanvasRefresh?.Invoke();
    }

    public void DuplicateSelected() => DuplicateSelected(direction: null);

    public void DuplicateSelected(DuplicateDirection? direction)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = State.ActiveSection.EffectiveDesignerHeight;
        float gap = State.SnapGapMm;

        var newElements = new List<TemplateElement>();
        var commands = new List<IDesignerCommand>();
        var groupIdMap = new Dictionary<string, string>();

        foreach (var orig in State.SelectedElements)
        {
            var clone = orig.Clone();
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            float cloneH = clone.Height ?? clone.EffectiveDesignerHeight;

            if (direction is null)
            {
                clone.X = Math.Clamp(orig.X + 5f, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(orig.Y + 5f, 0f, Math.Max(0f, sectionH - cloneH));
            }
            else
            {
                (clone.X, clone.Y) = direction.Value switch
                {
                    DuplicateDirection.Down  => (orig.X, orig.Y + origH + gap),
                    DuplicateDirection.Up    => (orig.X, orig.Y - cloneH - gap),
                    DuplicateDirection.Right => (orig.X + orig.Width + gap, orig.Y),
                    DuplicateDirection.Left  => (orig.X - clone.Width - gap, orig.Y),
                    _ => (orig.X + 5f, orig.Y + 5f)
                };
                clone.X = Math.Clamp(clone.X, 0f, Math.Max(0f, sectionW - clone.Width));
                clone.Y = Math.Clamp(clone.Y, 0f, Math.Max(0f, sectionH - cloneH));
            }

            // Přemapovat GroupId — klony dostanou nový společný GroupId
            RemapGroupId(clone, groupIdMap);

            newElements.Add(clone);
            commands.Add(new AddElementCommand(State.ActiveSection, clone));
        }

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Duplikovat elementy", commands));

        State.SelectedElements.Clear();
        State.SelectedElements.AddRange(newElements);
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Split element ─────────────────────────────────────────────────────────

    /// <summary>
    /// Rozdělí vybrané elementy na poloviny. Horizontal = nad/pod, Vertical = vedle sebe.
    /// Originál se zmenší na polovinu, klon se umístí do druhé poloviny.
    /// </summary>
    public void SplitSelected(bool horizontal)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var commands = new List<IDesignerCommand>();
        var newSelection = new List<TemplateElement>();

        foreach (var orig in State.SelectedElements.ToList())
        {
            float origH = orig.Height ?? orig.EffectiveDesignerHeight;
            var clone = orig.Clone();

            if (horizontal)
            {
                // Rozdělit výšku na dvě poloviny
                float halfH = MathF.Round(origH / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, orig.Width, halfH));
                clone.Y = orig.Y + halfH;
                clone.Height = MathF.Round(origH - halfH, 2);
            }
            else
            {
                // Rozdělit šířku na dvě poloviny
                float halfW = MathF.Round(orig.Width / 2f, 2);
                commands.Add(new ResizeElementCommand(orig,
                    orig.X, orig.Y, orig.Width, orig.Height,
                    orig.X, orig.Y, halfW, orig.Height));
                clone.X = orig.X + halfW;
                clone.Width = MathF.Round(orig.Width - halfW, 2);
            }

            commands.Add(new AddElementCommand(State.ActiveSection, clone));
            newSelection.Add(orig);
            newSelection.Add(clone);
        }

        if (commands.Count > 0)
        {
            Execute(new CompositeCommand(
                horizontal ? "Rozdělit horizontálně" : "Rozdělit vertikálně", commands));
            State.SelectedElements.Clear();
            State.SelectedElements.AddRange(newSelection);
            RequestPropertiesPanelUpdate?.Invoke();
            RequestCanvasRefresh?.Invoke();
        }
    }

    // ── Element factory (shared by palette drag + toolbar menu) ────────────────

    public static (float Width, float Height) GetDefaultElementSize(string type) => type switch
    {
        ElementTypes.Text          => (80f, 8f),
        ElementTypes.Rectangle     => (60f, 20f),
        ElementTypes.Line          => (190f, 1f),
        ElementTypes.Image         => (40f, 30f),
        // ElementTypes.Table (Fixed) je spící — nevytváří se z UI (viz TableElement docstring)
        ElementTypes.DynamicTable  => (190f, 30f),
        ElementTypes.Barcode       => (30f, 30f),
        ElementTypes.StopList      => (60f, 60f),
        ElementTypes.DepartureGrid => (120f, 60f),
        ElementTypes.NotesLegend   => (190f, 25f),
        ElementTypes.LineTable     => (190f, 100f),
        _                          => (80f, 8f)
    };

    public static TemplateElement CreateElementByType(string type, float x, float y)
    {
        var (w, h) = GetDefaultElementSize(type);
        var el = type switch
        {
            ElementTypes.Text          => (TemplateElement)new TextElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Rectangle     => new RectangleElement { X = x, Y = y, Width = w, Height = h, StrokeWidth = 0.5f },
            ElementTypes.Line          => new LineElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Image         => new ImageElement     { X = x, Y = y, Width = w, Height = h },
            // ElementTypes.Table (Fixed) je spící — nevytváří se z UI (viz TableElement docstring)
            ElementTypes.DynamicTable  => new DynamicTableElement { X = x, Y = y, Width = w, Height = h },
            ElementTypes.Barcode       => new BarcodeElement   { X = x, Y = y, Width = w, Height = h },
            ElementTypes.StopList      => new StopListElement      { X = x, Y = y, Width = w, Height = h },
            ElementTypes.DepartureGrid => new DepartureGridElement { X = x, Y = y, Width = w, Height = h },
            ElementTypes.NotesLegend   => new NotesLegendElement   { X = x, Y = y, Width = w, Height = h },
            ElementTypes.LineTable     => new LineTableElement     { X = x, Y = y, Width = w },   // Height null = dynamická
            _                          => (TemplateElement)new TextElement      { X = x, Y = y, Width = w, Height = h }
        };
        // Výchozí rámeček pro nové elementy (na modelu je default bez rámečku kvůli zpětné kompatibilitě)
        el.BorderColor = "#FF000000";
        el.BorderWidth = 0.1f;
        return el;
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    public void DeleteSelected()
    {
        if (State.SelectedElements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in State.SelectedElements.ToList())
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            if (section is null || el.IsLocked) continue;
            commands.Add(new DeleteElementCommand(section, el));
        }

        if (commands.Count == 0) return;

        State.SelectedElements.Clear();

        if (commands.Count == 1) Execute(commands[0]);
        else Execute(new CompositeCommand("Smazat elementy", commands));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Group / Ungroup ──────────────────────────────────────────────────────

    /// <summary>Přemapuje GroupId klonu na nový sdílený GroupId (pro paste/duplicate).</summary>
    private static void RemapGroupId(TemplateElement clone, Dictionary<string, string> groupIdMap)
    {
        if (clone.GroupId is null) return;
        if (!groupIdMap.TryGetValue(clone.GroupId, out var newGid))
            groupIdMap[clone.GroupId] = newGid = ElementGroupHelper.NewGroupId();
        clone.GroupId = newGid;
    }

    public void GroupSelected()
    {
        if (State.SelectedElements.Count < 2) return;
        var groupId = ElementGroupHelper.NewGroupId();
        Execute(new GroupCommand(State.SelectedElements, groupId));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    public void UngroupSelected()
    {
        var grouped = State.SelectedElements.Where(e => e.GroupId is not null).ToList();
        if (grouped.Count == 0) return;
        Execute(new UngroupCommand(grouped));
        RequestCanvasRefresh?.Invoke();
        RequestPropertiesPanelUpdate?.Invoke();
    }

    // ── ZIndex ────────────────────────────────────────────────────────────────
    public void ChangeZIndex(int sign, bool toExtreme)
    {
        if (State.SelectedElements.Count == 0 || State.ActiveSection is null) return;

        var section  = State.ActiveSection;
        var selected = State.SelectedElements
            .Where(el => section.Elements.Contains(el))
            .ToList();
        if (selected.Count == 0) return;

        var commands = new List<IDesignerCommand>();

        if (toExtreme && sign > 0)
        {
            int baseZ = section.Elements
                .Where(el => !selected.Contains(el))
                .Select(el => el.ZIndex)
                .DefaultIfEmpty(-1).Max() + 1;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = baseZ++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else if (toExtreme && sign < 0)
        {
            int shift = selected.Count;
            foreach (var el in section.Elements.Where(e => !selected.Contains(e)))
            {
                int oldZ = el.ZIndex, newZ = oldZ + shift;
                commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
            int z = 0;
            foreach (var el in selected.OrderBy(e => e.ZIndex))
            {
                int oldZ = el.ZIndex, newZ = z++;
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }
        else
        {
            foreach (var el in selected)
            {
                int oldZ = el.ZIndex, newZ = Math.Max(0, oldZ + sign);
                if (oldZ != newZ) commands.Add(new SetPropertyCommand("ZIndex", oldZ, newZ, v => el.ZIndex = (int)v!));
            }
        }

        if (commands.Count == 0) return;

        string desc = (sign > 0, toExtreme) switch
        {
            (true,  true)  => "Přenést zcela dopředu",
            (true,  false) => "Přenést dopředu",
            (false, true)  => "Odeslat zcela dozadu",
            _              => "Odeslat dozadu"
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Alignment ─────────────────────────────────────────────────────────────
    public void AlignElements(AlignMode mode)
    {
        // X-align pracuje napříč sekcemi (sekce sdílejí levý margin → X je přímo porovnatelné).
        // Y-align zůstává per-aktivní-sekce (Y je relativní k hornímu okraji sekce).
        bool isXMode = mode is AlignMode.Left or AlignMode.Right or AlignMode.CenterH or AlignMode.DistributeH;

        List<TemplateElement> els;
        if (isXMode)
        {
            els = State.SelectedElements.ToList();
        }
        else
        {
            if (State.ActiveSection is null) return;
            els = State.SelectedElements
                .Where(el => State.ActiveSection.Elements.Contains(el))
                .ToList();
        }

        int minCount = mode is AlignMode.DistributeH or AlignMode.DistributeV ? 3 : 2;
        if (els.Count < minCount) return;

        var commands = new List<IDesignerCommand>();

        // Zaokrouhlení na celé mm — align cílí na "čistou" hodnotu konzistentní se soft-snapem při dragu.
        static float Rnd(float v) => MathF.Round(v);

        switch (mode)
        {
            case AlignMode.Left:
            {
                float t = Rnd(els.Min(e => e.X));
                foreach (var el in els)
                { if (Math.Abs(el.X - t) > 0.001f) commands.Add(MakeXCmd(el, t)); }
                break;
            }
            case AlignMode.Right:
            {
                float t = Rnd(els.Max(e => e.X + e.Width));
                foreach (var el in els)
                { float nx = t - el.Width; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.CenterH:
            {
                float cx = Rnd((els.Min(e => e.X) + els.Max(e => e.X + e.Width)) / 2f);
                foreach (var el in els)
                { float nx = cx - el.Width / 2f; if (Math.Abs(el.X - nx) > 0.001f) commands.Add(MakeXCmd(el, nx)); }
                break;
            }
            case AlignMode.Top:
            {
                float t = Rnd(els.Min(e => e.Y));
                foreach (var el in els)
                { if (Math.Abs(el.Y - t) > 0.001f) commands.Add(MakeYCmd(el, t)); }
                break;
            }
            case AlignMode.Bottom:
            {
                float t = Rnd(els.Max(e => e.Y + e.EffectiveDesignerHeight));
                foreach (var el in els)
                { float ny = t - el.EffectiveDesignerHeight; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.MiddleV:
            {
                float cy = Rnd((els.Min(e => e.Y) + els.Max(e => e.Y + e.EffectiveDesignerHeight)) / 2f);
                foreach (var el in els)
                { float ny = cy - el.EffectiveDesignerHeight / 2f; if (Math.Abs(el.Y - ny) > 0.001f) commands.Add(MakeYCmd(el, ny)); }
                break;
            }
            case AlignMode.DistributeH:
            {
                var sorted = els.OrderBy(e => e.X).ToList();
                float totalW = sorted.Sum(e => e.Width);
                float span   = sorted.Last().X + sorted.Last().Width - sorted.First().X;
                float gap    = MmMath.Round((span - totalW) / (sorted.Count - 1));
                float cur    = sorted.First().X;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float x = MmMath.Round(cur);
                    if (Math.Abs(el.X - x) > 0.01f) commands.Add(MakeXCmd(el, x));
                    cur += el.Width + gap;
                }
                break;
            }
            case AlignMode.DistributeV:
            {
                var sorted = els.OrderBy(e => e.Y).ToList();
                float totalH = sorted.Sum(e => e.EffectiveDesignerHeight);
                float span   = sorted.Last().Y + sorted.Last().EffectiveDesignerHeight - sorted.First().Y;
                float gap    = MmMath.Round((span - totalH) / (sorted.Count - 1));
                float cur    = sorted.First().Y;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var el = sorted[i];
                    float y = MmMath.Round(cur);
                    if (Math.Abs(el.Y - y) > 0.01f) commands.Add(MakeYCmd(el, y));
                    cur += el.EffectiveDesignerHeight + gap;
                }
                break;
            }
        }

        if (commands.Count == 0) return;

        string desc = mode switch
        {
            AlignMode.Left        => "Zarovnat vlevo",
            AlignMode.Right       => "Zarovnat vpravo",
            AlignMode.CenterH     => "Zarovnat na střed (vodorovně)",
            AlignMode.Top         => "Zarovnat nahoru",
            AlignMode.Bottom      => "Zarovnat dolů",
            AlignMode.MiddleV     => "Zarovnat na střed (svisle)",
            AlignMode.DistributeH => "Rozložit vodorovně",
            AlignMode.DistributeV => "Rozložit svisle",
            _                     => "Zarovnat"
        };

        Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(desc, commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    private static IDesignerCommand MakeXCmd(TemplateElement el, float nx)
        => new SetPropertyCommand("X", el.X, nx, v => el.X = (float)v!);
    private static IDesignerCommand MakeYCmd(TemplateElement el, float ny)
        => new SetPropertyCommand("Y", el.Y, ny, v => el.Y = (float)v!);

    // ── Normalize positions ───────────────────────────────────────────────────
    public void NormalizePositions(IReadOnlyList<TemplateElement> elements, float step)
    {
        if (elements.Count == 0) return;

        var commands = new List<IDesignerCommand>();
        foreach (var el in elements)
        {
            float nx = MmMath.Snap(el.X, step), ny = MmMath.Snap(el.Y, step);
            float nw = MmMath.Snap(el.Width, step);
            float? nh = el.Height is not null ? MmMath.Snap(el.Height.Value, step) : null;

            bool posChanged  = Math.Abs(nx - el.X) > 0.001f || Math.Abs(ny - el.Y) > 0.001f;
            bool sizeChanged = Math.Abs(nw - el.Width) > 0.001f
                            || (nh is not null && Math.Abs(nh.Value - el.Height!.Value) > 0.001f);

            if (posChanged || sizeChanged)
                commands.Add(new ResizeElementCommand(el, el.X, el.Y, el.Width, el.Height, nx, ny, nw, nh));
        }

        if (commands.Count == 0) return;
        Execute(new CompositeCommand("Normalizovat pozice", commands));
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Template scaling ─────────────────────────────────────────────────────
    public void ScaleTemplate(float scale)
    {
        if (Math.Abs(scale - 1f) < 0.001f) return;
        var cmd = new ScaleTemplateCommand(State.CurrentTemplate, scale);
        Execute(cmd);
        RebuildSections();
        RequestCanvasSizeUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Key movement ──────────────────────────────────────────────────────────
    /// <summary>
    /// Arrow key nudging. Kroky:
    ///   fineStep=true (Ctrl)   → 0.5 mm (jemné doladění)
    ///   largeStep=true (Shift) → GridSize (rychlý skok)
    ///   bez modifikátorů       → 1 mm (default)
    /// </summary>
    public void MoveByKey(Key key, bool largeStep, bool fineStep = false)
    {
        float step = fineStep ? 0.5f : (largeStep ? State.GridSize : 1f);
        float dx   = key switch { Key.Left => -step, Key.Right => step, _ => 0f };
        float dy   = key switch { Key.Up   => -step, Key.Down  => step, _ => 0f };

        var initPositions = State.SelectedElements.ToDictionary(el => el, el => (el.X, el.Y));
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float clampedDx = dx, clampedDy = dy;

        foreach (var kvp in initPositions)
        {
            var el      = kvp.Key;
            float elH   = el.EffectiveDesignerHeight;
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(el));
            float sectionH = section?.EffectiveDesignerHeight ?? float.MaxValue;
            float maxDx = sectionW - el.Width - kvp.Value.X;
            float maxDy = sectionH - elH    - kvp.Value.Y;
            // Element může být širší/vyšší než sekce (např. background image) — přeskočit clamp
            if (maxDx >= -kvp.Value.X)
                clampedDx = Math.Clamp(clampedDx, -kvp.Value.X, maxDx);
            if (maxDy >= -kvp.Value.Y)
                clampedDy = Math.Clamp(clampedDy, -kvp.Value.Y, maxDy);
        }

        if (clampedDx == 0f && clampedDy == 0f) return;

        foreach (var kvp in initPositions)
        { kvp.Key.X = kvp.Value.X + clampedDx; kvp.Key.Y = kvp.Value.Y + clampedDy; }

        var moves = initPositions
            .Where(kvp => kvp.Key.X != kvp.Value.X || kvp.Key.Y != kvp.Value.Y)
            .Select(kvp => (IDesignerCommand)new MoveElementCommand(
                kvp.Key, kvp.Value.X, kvp.Value.Y, kvp.Key.X, kvp.Key.Y))
            .ToList();

        if (moves.Count == 1) Execute(moves[0]);
        else if (moves.Count > 1) Execute(new CompositeCommand("Přesun šipkou", moves));

        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema / parameters ───────────────────────────────────────────────────
    public void RefreshFieldsList()
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;
        NotifyInstanceLabelsChanged();   // agenda se mohla změnit → přepočítat „linka/faktura" v chipu + tooltipech

        if (string.IsNullOrWhiteSpace(agendaCode))
        {
            AgendaNameText = "Agenda: — (šablona bez agendy)";
            UpdateCacheStatus(null, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        // R1: první (uncached) GetSchema je pomalý HTTP dotaz — na cold cestě odbav na pozadí
        // a strom polí doplň po návratu; jinak zamrzne UI při otevření šablony cizí agendy na
        // pomalé síti. Opakované (cachované) volání jde rovnou synchronně (rychlé).
        if (CacheProvider is not null && !CacheProvider.IsSchemaCached(agendaCode))
        {
            var code = agendaCode;
            AgendaNameText = $"Agenda: {code} (načítám schéma…)";
            _ = Task.Run(() => SchemaProvider.GetSchema(code))
                .ContinueWith(_ => RefreshFieldsListCore(code),
                              TaskScheduler.FromCurrentSynchronizationContext());
            return;
        }

        RefreshFieldsListCore(agendaCode);
    }

    private void RefreshFieldsListCore(string agendaCode)
    {
        var schema = SchemaProvider.GetSchema(agendaCode);   // po prewarmu už cached → rychlé

        if (schema is null)
        {
            AgendaNameText = $"Agenda: {agendaCode} (schéma nenalezeno)";
            UpdateCacheStatus(agendaCode, null);
            LoadAgendaParameters(agendaCode);
            RebuildSchemaTree(null);
            return;
        }

        // Explicitní dvouosý formát: „Agenda: Zastávkový JŘ · Varianta: AD" (učí nové názvosloví);
        // bez varianty jen „Agenda: Faktury". Fallback = jméno ze schématu (registr nezná kód).
        var entry = AgendaEntry(agendaCode);
        AgendaNameText = entry is { Variant.Length: > 0 }
            ? $"Agenda: {entry.EffectiveAgendaLabel} · Varianta: {entry.Variant}"
            : $"Agenda: {entry?.EffectiveAgendaLabel ?? schema.AgendaName}";
        var cacheSchema = CacheProvider?.GetSchema(agendaCode);
        UpdateCacheStatus(agendaCode, cacheSchema);
        LoadAgendaParameters(agendaCode);
        RebuildSchemaTree(schema);
    }

    public void RebuildSchemaTree(DataSchema? schema)
    {
        SchemaGroups.Clear();

        // Paleta zobrazuje JEN skalární pole (draggable jako {POLE}). Kolekce (Zastávky,
        // Odjezdy, …) konzumují dedikované elementy (StopList/DepartureGrid/NotesLegend),
        // ne ad-hoc tabulky — jako draggable pole jen matou. Parametry se nastavují
        // v preview liště / tiskovém dialogu, ne přetažením. (Rozhodnuto 2026-06-16.)
        var fields = schema?.Fields ?? [];
        if (fields.Count > 0)
            SchemaGroups.Add(new SchemaGroupVM(
                $"📋 Pole agendy ({fields.Count})",
                fields.Select(f => new SchemaFieldVM(f.FieldName, f.Label))));
    }

    public void LoadAgendaParameters(string? agendaCode)
    {
        var code = string.IsNullOrEmpty(agendaCode) ? null : agendaCode;
        if (code == _lastParamsAgendaCode) return;
        _lastParamsAgendaCode = code;

        _agendaParameters = code is not null && CacheProvider is not null
            ? [.. CacheProvider.GetParameters(code)]
            : [.. State.CurrentTemplate.Parameters ?? []];

        _previewParams.Clear();
        _cachedParamValues = null;
        foreach (var p in _agendaParameters)
            _previewParams[p.Name] = p.GetDefaultTyped();

        RebuildParametersPanel();
        // Reálná data řídí výhradně Instance Picker (RefreshDataForCurrentInstanceAsync).
        // Bez vybrané instance preview používá mock data — parametry sestavy už linku neurčují.
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>
    /// Načte uložené PrintParams pro aktuální agendu a aplikuje je nad <see cref="_previewParams"/>.
    /// Volá se po opening šablony (po LoadAgendaParameters) a z OnPreferencesLoaded (cold-start fallback).
    /// Idempotentní — pokud žádná saved data, nic nemění. Per-agenda granularita: nová šablona
    /// v rámci stejné agendy zdědí naposledy použité hodnoty.
    /// </summary>
    public async Task LoadPrintParamsAndApplyAsync()
    {
        if (Preferences is null) return;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return;

        var saved = await Preferences.LoadPrintParamsAsync(agendaCode);
        if (saved.Count == 0) return;

        bool changed = false;
        foreach (var p in _agendaParameters)
        {
            if (!saved.TryGetValue(p.Name, out var node) || node is null) continue;
            try
            {
                object? newValue = p.Type switch
                {
                    "bool"   => node.GetValue<bool>(),
                    "number" => node.GetValue<float>(),
                    _        => node.GetValue<string>()
                };
                if (!Equals(_previewParams.GetValueOrDefault(p.Name), newValue))
                {
                    _previewParams[p.Name] = newValue;
                    changed = true;
                }
            }
            catch { /* malformed value pro tento param — ponecháme default */ }
        }

        // Export pole (členění/název souboru) nejsou agenda parametry → obnovit zvlášť (string).
        // Bez tohoto by se uložená volba po reopenu šablony ztratila (write-only).
        foreach (var key in PrintDialogViewModel.ExportKeys)
        {
            if (!saved.TryGetValue(key, out var node) || node is null) continue;
            try { _previewParams[key] = node.GetValue<string>(); }
            catch { /* malformed — ponecháme default */ }
        }

        if (changed) OnExternalParamsChanged();
    }

    /// <summary>
    /// Externí konzument (typicky PrintDialog) změnil hodnotu v sdíleném <see cref="_previewParams"/>.
    /// Cross-notify preview lišta VMs + invalidate cache + refresh preview canvas. Bez tohoto
    /// volání preview lišta neukáže změnu, protože její <c>ParameterControlVM</c> jsou jiné instance
    /// než PrintDialogovy a vlastní PropertyChanged events se nepropagují automaticky.
    /// </summary>
    public void OnExternalParamsChanged()
    {
        _cachedParamValues = null;
        foreach (var vm in PreviewParameters) vm.NotifyValueChanged();
        RequestPreviewRefresh?.Invoke();
    }

    /// <summary>Uloží aktuální <see cref="_previewParams"/> pro tuto agendu (fire-and-forget).</summary>
    public Task SavePrintParamsAsync()
    {
        if (Preferences is null) return Task.CompletedTask;
        var agendaCode = State.CurrentTemplate.AgendaCode;
        if (string.IsNullOrEmpty(agendaCode)) return Task.CompletedTask;
        return Preferences.SavePrintParamsAsync(agendaCode, _previewParams);
    }

    public void RebuildParametersPanel()
    {
        ParameterControlVM.Init(
            target:     PreviewParameters,
            parameters: _agendaParameters,
            values:     _previewParams,
            onChanged:  () =>
            {
                _cachedParamValues = null;
                // Server-side param „rozdělení sloupců" (grouping počítá Caché) → změna vyžaduje re-fetch dat.
                // Ostatní (client-side) parametry jen překreslí. (Zatím jediný server param — hardcoded.)
                var rozd = _previewParams.TryGetValue("ROZDELENI_SLOUPCU", out var rv) ? rv?.ToString() : null;
                if (CurrentActiveInstance is not null && rozd != _lastServerParamRozdeleni)
                {
                    _lastServerParamRozdeleni = rozd;
                    _ = RefreshDataForCurrentInstanceAsync();
                }
                else
                {
                    RequestPreviewRefresh?.Invoke();
                }
            },
            commitFactory: _ => null);

        // Výchozí hodnota server-paramu, ať první změna client-paramu nespustí falešný re-fetch.
        _lastServerParamRozdeleni = _previewParams.TryGetValue("ROZDELENI_SLOUPCU", out var rv0) ? rv0?.ToString() : null;
        HasParameters = PreviewParameters.Count > 0;
    }

    private void UpdateCacheStatus(string? agendaCode, DataSchema? cacheSchema)
    {
        if (CacheProvider is null)
        { StatusText = "Databáze: není nakonfigurována — pole načtena z JSON"; return; }

        if (agendaCode is null)
        { StatusText = "Databáze: nakonfigurována"; return; }

        StatusText = cacheSchema is not null
            ? $"● Databáze: připojeno — {agendaCode} ({cacheSchema.Fields.Count} polí, {cacheSchema.FieldCollections?.Count ?? 0} kolekcí)"
            : $"○ Databáze: nedostupná — záloha JSON ({agendaCode})";
    }

    public CultureInfo GetTemplateCulture()
    {
        try { return CultureInfo.GetCultureInfo(State.CurrentTemplate.CultureCode ?? "cs-CZ"); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("cs-CZ"); }
    }

    // ── Preview mock data (called by code-behind preview renderer) ────────────

    /// <summary>
    /// Sestaví DataContext pro preview. Priorita:
    /// 1. Reálná data z Caché pro vybranou instanci (Instance Picker → _realPreviewData)
    /// 2. Mock data z Caché GetMockData
    /// 3. Syntetická mock data ze schématu
    /// Volitelný parametr overrideParams nahrazuje interní _previewParams (pro PrintDialog).
    /// </summary>
    /// <summary>
    /// Vrátí všechny dostupné DataContexty pro export — všechny reálné stránky pokud jsou
    /// načtené, jinak jeden mock context. Použito pro PDF/PNG export, aby se exportovaly
    /// všechny zastávky/směry, ne jen aktuálně zobrazená. Fetch (N× GetData přes MLRest)
    /// běží na threadpoolu (R1 — u velkých batchů jednotky sekund).
    /// </summary>
    public async Task<IReadOnlyList<DataContext>> BuildAllPreviewDataPagesAsync(IProgress<string>? progress = null)
    {
        // VEŠKERÝ UI stav zachytit na UI threadu před přechodem na threadpool — uživatel může
        // během exportu dál pracovat (měnit instance v pickeru, parametry, přepnout preview data),
        // background smyčka proto nesmí číst živé VM kolekce/pole.
        var culture         = GetTemplateCulture();
        var agendaCode      = State.CurrentTemplate.AgendaCode;
        var instances       = _activeInstances.ToList();
        var skupinaId       = _activeSkupinaId;
        var realPreviewData = _realPreviewData;   // reference swap-only pole — lokální kopie reference stačí
        var suppression     = CurrentNoteSuppression();
        var hideAgg         = CurrentHideAggBezbarier();
        var hideNaZnameni   = CurrentHideNaZnameniLegend();

        return await Task.Run(() => BuildAllPreviewDataPagesCore(
            culture, agendaCode, instances, skupinaId, realPreviewData, suppression, hideAgg, hideNaZnameni, progress));
    }

    private IReadOnlyList<DataContext> BuildAllPreviewDataPagesCore(
        CultureInfo culture, string? agendaCode, List<AgendaInstance> instances, string? skupinaId,
        IReadOnlyList<DataContext?>? realPreviewData,
        NoteRangeSuppression suppression, bool hideAgg, bool hideNaZnameni,
        IProgress<string>? progress)
    {
        // Multi-instance batch: pro každou aktivní instanci fetch GetData a sloučit do flat seznamu.
        // PDF export tak vygeneruje N×K stránek (N instancí × K stránek per instance, např. zastávka×směr).
        if (instances.Count > 0 && CacheProvider is not null && !string.IsNullOrEmpty(agendaCode))
        {
            var paramName = InstanceParamName(agendaCode);
            var all = new List<DataContext>(instances.Count * 4);
            for (int i = 0; i < instances.Count; i++)
            {
                var inst = instances[i];
                progress?.Report($"Načítám data {i + 1}/{instances.Count}: {inst.Display}…");
                var getParams = new Dictionary<string, object?> { [paramName] = inst.Value };
                if (!string.IsNullOrEmpty(skupinaId)) getParams["SKUPINA_LINEK_ID"] = skupinaId;
                var pages = CacheProvider.GetData(agendaCode, getParams);
                if (pages is null) continue;
                foreach (var p in pages) all.Add(p.WithCulture(culture));
            }
            if (all.Count > 0) return NoteVisibilityFilter.Apply(all, suppression, hideAgg, hideNaZnameni);
        }

        if (realPreviewData is { Count: > 0 })
            return NoteVisibilityFilter.Apply(
                realPreviewData.Select(d => d?.WithCulture(culture) ?? new DataContext()).ToList(),
                suppression, hideAgg, hideNaZnameni);

        // Pro šablony bez DB vazby (GENERAL / nová bez agendy) BuildPreviewMockData vrátí null;
        // export musí přesto projít — vrátíme prázdný DataContext, aby se vyrenderovala
        // jedna stránka s placeholdery beze změny.
        return [BuildPreviewMockData() ?? new DataContext()];
    }

    public DataContext? BuildPreviewMockData(Dictionary<string, object?>? overrideParams = null)
    {
        var agendaCode = State.CurrentTemplate.AgendaCode;
        var culture = GetTemplateCulture();
        DataContext? mockData = null;

        // Priorita 1: reálná data (pokud jsou k dispozici)
        if (_realPreviewData is { Count: > 0 })
        {
            var idx = Math.Clamp(_realPreviewIndex, 0, _realPreviewData.Count - 1);
            mockData = _realPreviewData[idx]?.WithCulture(culture);
        }

        // Priorita 2: mock data z Caché
        if (mockData is null && agendaCode is not null && CacheProvider is not null)
            mockData = CacheProvider.GetMockData(agendaCode)?.WithCulture(culture);

        // Priorita 3: syntetická mock data ze schématu
        if (mockData is null)
        {
            var schema = SchemaProvider.GetSchema(agendaCode ?? "");
            if (schema is not null)
            {
                var collections = schema.FieldCollections ?? [];
                var values = schema.Fields
                    .Concat(collections.SelectMany(c => c.Fields))
                    .DistinctBy(f => f.FieldName)
                    .ToDictionary(f => f.FieldName, f => (object?)(f.Example ?? MockDefault(f)));

                var rowsDict = new Dictionary<string, IReadOnlyList<DataContext>>();
                foreach (var collection in collections)
                {
                    var mockRows = Enumerable.Range(1, 3).Select(i =>
                    {
                        var rowData = collection.Fields.ToDictionary(
                            f => f.FieldName,
                            f => (object?)MakeMockValue(f.Example ?? MockDefault(f), f.DataType, i));
                        return new DataContext(rowData, culture: culture);
                    }).ToList();
                    rowsDict[collection.CollectionKey] = mockRows;
                }

                mockData = new DataContext(values, rowsDict, culture);
            }
        }

        // Použij overrideParams (z PrintDialogu) nebo interní _previewParams
        var paramValues = overrideParams;
        if (paramValues is null && _previewParams.Count > 0)
        {
            _cachedParamValues ??= _previewParams.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);
            paramValues = _cachedParamValues;
        }

        if (paramValues is { Count: > 0 })
        {
            var stringParams = paramValues.ToDictionary(
                kv => kv.Key,
                kv => (object?)kv.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

            mockData = mockData is not null
                ? mockData.WithExtraValues(stringParams)
                : new DataContext(stringParams, culture: culture);
        }

        var src = overrideParams ?? _previewParams;
        var suppressionMode = NoteVisibilityFilter.ParseMode(src.GetValueOrDefault(NoteVisibilityFilter.ParamName));
        var hideAgg = NoteVisibilityFilter.ParseBool(src.GetValueOrDefault(NoteVisibilityFilter.ParamAggBezbarier));
        return mockData is null ? null : NoteVisibilityFilter.Apply(mockData, suppressionMode, hideAgg, CurrentHideNaZnameniLegend());
    }

    /// <summary>Režim schování poznámek v rozsahu „nepoznámkovat" z aktuálního parametru sestavy.</summary>
    private NoteRangeSuppression CurrentNoteSuppression() =>
        NoteVisibilityFilter.ParseMode(_previewParams.GetValueOrDefault(NoteVisibilityFilter.ParamName));

    /// <summary>Skrýt agregátní bezbariér poznámku (#7161 D6) z aktuálního parametru sestavy.</summary>
    private bool CurrentHideAggBezbarier() =>
        NoteVisibilityFilter.ParseBool(_previewParams.GetValueOrDefault(NoteVisibilityFilter.ParamAggBezbarier));

    /// <summary>Skrýt legendu „× na znamení" když StopList netiskne značku (#7161 D5) — ze šablony.</summary>
    private bool CurrentHideNaZnameniLegend() =>
        NoteVisibilityFilter.ShouldHideNaZnameniLegend(State.CurrentTemplate);

    /// <summary>Výchozí mock hodnota podle typu pole — použije se když Example chybí.</summary>
    private static string MockDefault(DataField f) => f.DataType switch
    {
        FieldDataType.Number   => "100",
        FieldDataType.Date     => "01.01.2024",
        FieldDataType.Time     => "08:00",
        FieldDataType.DateTime => "01.01.2024 08:00",
        FieldDataType.Boolean  => "true",
        _                      => f.Label  // text — fallback na label
    };

    private static string MakeMockValue(string baseValue, FieldDataType type, int rowIndex)
    {
        if (rowIndex <= 1) return baseValue;
        return type switch
        {
            FieldDataType.Number => int.TryParse(baseValue, out var n) ? (n + rowIndex - 1).ToString() : baseValue,
            FieldDataType.Time   => TimeSpan.TryParse(baseValue, out var t)
                ? (t + TimeSpan.FromMinutes(10 * (rowIndex - 1))).ToString(@"hh\:mm") : baseValue,
            FieldDataType.Date   => DateTime.TryParse(baseValue, out var d)
                ? d.AddDays(rowIndex - 1).ToString("dd.MM.yyyy") : baseValue,
            _ => string.IsNullOrEmpty(baseValue) ? baseValue : $"{baseValue} #{rowIndex}"
        };
    }

    // ── Available fields (for ElementPropertiesWindow) ────────────────────────
    public IReadOnlyList<DataField>? GetAvailableFields()
    {
        var schemaFields = SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.Fields;
        if (schemaFields is null && _agendaParameters.Count == 0) return null;
        var paramFields  = _agendaParameters.Select(p => new DataField
            { FieldName = p.Name, Label = p.Label + " (parametr)", DataType = FieldDataType.Text });
        return [.. (schemaFields ?? []), .. paramFields];
    }

    public IReadOnlyList<DataFieldCollection>? GetAvailableGroups()
        => SchemaProvider.GetSchema(State.CurrentTemplate.AgendaCode ?? "")?.FieldCollections;

    // ── Status text for selection ──────────────────────────────────────────────

    /// <summary>Aktualizuje SelectionStatusText podle aktuálního výběru (klidová zóna status baru).</summary>
    public void UpdateStatusForSelection()
    {
        if (State.SelectedElements.Count == 1)
        {
            var el = State.SelectedElements[0];
            string type = el.GetType().Name.Replace("Element", "");
            SelectionStatusText = $"{type}  |  X: {el.X:0.#}  Y: {el.Y:0.#}  |  Šířka: {el.Width:0.#} mm" +
                         (el.IsLocked ? "  [ZAMČENO]" : "") +
                         "  |  2× klik nebo Enter = vlastnosti";
        }
        else if (State.SelectedElements.Count > 1)
        {
            SelectionStatusText = $"Vybráno {State.SelectedElements.Count} elementů  |  Ctrl+klik pro odebrání z výběru";
        }
        else if (State.ActiveSection is not null)
        {
            SelectionStatusText = $"Sekce: {State.ActiveSection.Name}  |  " +
                         $"Výška: {State.ActiveSection.DesignerHeight?.ToString("0.#") ?? "dynamická"} mm  |  " +
                         $"Elementy: {State.ActiveSection.Elements.Count}";
        }
        else
        {
            SelectionStatusText = "Vyberte element nebo sekci.";
        }
    }

    // ── Tree selection ──────────────────────────────────────────────────────────

    /// <summary>Zpracuje výběr z template tree — element, skupina nebo sekce (dříve v code-behind).</summary>
    public void SelectFromTree(object? tag)
    {
        if (tag is TemplateElement element)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(element));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.Add(element);
            }
        }
        else if (tag is string groupId)
        {
            var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Any(el => el.GroupId == groupId));
            if (section is not null)
            {
                ActivateSection(section);
                State.SelectedElements.Clear();
                State.SelectedElements.AddRange(section.Elements.Where(el => el.GroupId == groupId));
            }
        }
        else if (tag is Section section)
        {
            ActivateSection(section);
            State.SelectedElements.Clear();
        }
        else return;

        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Schema field insert ─────────────────────────────────────────────────────

    /// <summary>Vloží placeholder do vybraného TextElementu nebo zkopíruje do schránky (dříve v code-behind).</summary>
    public void InsertOrCopyPlaceholder(string placeholder)
    {
        if (State.SelectedElements.Count == 1 && State.SelectedElements[0] is TextElement textEl)
        {
            string oldText = textEl.Text;
            string newText = oldText + placeholder;
            Execute(new SetPropertyCommand(
                $"Vložit {placeholder}",
                oldText, newText,
                v => textEl.Text = (string)v!));
            RequestCanvasRefresh?.Invoke();
            RequestPropertiesPanelUpdate?.Invoke();
        }
        else
        {
            Dialogs?.SetClipboardText(placeholder);
            StatusText = $"Zkopírováno do schránky: {placeholder}";
        }
    }

    // ── Element drop (adding) ───────────────────────────────────────────────────

    /// <summary>Připojí placeholder k existujícímu TextElementu (field drag na element).</summary>
    public void AppendPlaceholderToElement(TextElement textEl, string fieldName)
    {
        string placeholder = $"{{{fieldName}}}";
        string oldText = textEl.Text;
        string newText = oldText + placeholder;
        Execute(new SetPropertyCommand(
            $"Vložit {placeholder}",
            oldText, newText,
            v => textEl.Text = (string)v!));

        var section = State.CurrentTemplate.Sections.FirstOrDefault(s => s.Elements.Contains(textEl));
        if (section is not null) ActivateSection(section);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(textEl);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    /// <summary>Vytvoří a umístí nový element na danou pozici v sekci (dříve v EndAddingElement code-behind).</summary>
    public void AddElementAtPosition(Section targetSection, float mmX, float mmY, string? elementType, string? fieldName)
    {
        var (defW, defH) = GetDefaultElementSize(elementType ?? ElementTypes.Text);

        // Centrovat element na pozici kurzoru
        mmX -= defW / 2f;
        mmY -= defH / 2f;

        // Clamp do hranic sekce
        float sectionW = State.CurrentTemplate.PageSize.PrintableWidth;
        float sectionH = targetSection.EffectiveDesignerHeight;
        mmX = Math.Clamp(mmX, 0, Math.Max(0, sectionW - defW));
        mmY = Math.Clamp(mmY, 0, Math.Max(0, sectionH - defH));

        // Snap na mřížku
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            mmX = MathF.Round(mmX / g) * g;
            mmY = MathF.Round(mmY / g) * g;
        }

        // Vytvoření elementu
        TemplateElement element;
        if (fieldName is not null)
        {
            // Přes CreateElementByType, ať DB-pole dostane stejné defaulty jako element z palety
            // (výchozí rámeček, rozměry) — dřív se tvořil holý TextElement bez rámečku.
            var textEl = (TextElement)CreateElementByType(ElementTypes.Text, mmX, mmY);
            textEl.Text = $"{{{fieldName}}}";
            element = textEl;
        }
        else
        {
            element = CreateElementByType(elementType!, mmX, mmY);
        }

        // Snap i rozměrů — výchozí velikosti (80×8, 190×25…) nejsou násobky kroku mřížky,
        // takže bez tohoto spodní/pravá hrana nikdy neseděla na mřížce a element působil
        // „mimo grid" hned po přidání. Zaokrouhlení na 0 (element menší než krok) → původní rozměr.
        if (State.SnapToGrid)
        {
            float g = State.GridSize;
            static float SnapSize(float v, float step)
            {
                float snapped = MathF.Round(v / step) * step;
                return snapped > 0 ? snapped : v;
            }
            element.Width = SnapSize(element.Width, g);
            if (element.Height is { } h) element.Height = SnapSize(h, g);
        }

        Execute(new AddElementCommand(targetSection, element));
        ActivateSection(targetSection);
        State.SelectedElements.Clear();
        State.SelectedElements.Add(element);
        UpdateStatusForSelection();
        RequestPropertiesPanelUpdate?.Invoke();
        RequestCanvasRefresh?.Invoke();
    }

    // ── Hit handling (element selection logic from mouse down) ────────────────

    /// <summary>Zpracuje klik na element — výběr/toggle s Ctrl/Alt/Group logikou (dříve v OnCanvasMouseDown).</summary>
    public void HandleElementHit(Section section, TemplateElement element, bool ctrl, bool alt)
    {
        if (ctrl)
        {
            // Ctrl+klik: toggle element (nebo celou skupinu)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                if (State.SelectedElements.Contains(element))
                    foreach (var m in groupMembers) State.SelectedElements.Remove(m);
                else
                    foreach (var m in groupMembers.Where(m2 => !State.SelectedElements.Contains(m2)))
                        State.SelectedElements.Add(m);
            }
            else
            {
                if (State.SelectedElements.Contains(element))
                    State.SelectedElements.Remove(element);
                else
                    State.SelectedElements.Add(element);
            }
        }
        else if (!State.SelectedElements.Contains(element))
        {
            State.SelectedElements.Clear();
            // Klik na člen skupiny → vyber celou skupinu (Alt = jen jeden element)
            if (element.GroupId is not null && !alt)
            {
                var groupMembers = section.Elements.Where(e => e.GroupId == element.GroupId).ToList();
                State.SelectedElements.AddRange(groupMembers);
            }
            else
            {
                State.SelectedElements.Add(element);
            }
        }

        ActivateSection(section);
    }

    /// <summary>Zpracuje klik do prázdné oblasti — zruší výběr (pokud ne Ctrl), aktivuje sekci.</summary>
    public void HandleEmptyAreaHit(Section? section, bool ctrl)
    {
        if (!ctrl)
            State.SelectedElements.Clear();
        ActivateSection(section);
        UpdateStatusForSelection();
    }

    // ── Sample template ───────────────────────────────────────────────────────
    /// <summary>Fallback startovní šablona — agendu typicky přepíše EDISON kontrakt nebo
    /// poslední použitá agenda uživatele (viz ctor); AD je default pro první spuštění.</summary>
    public static Template CreateSampleTemplate()
    {
        var t = new Template { Name = "Nová šablona", AgendaCode = "ZASTAVKOVY_JR_AD" };
        t.Sections.Add(CreateDefaultSection(t.PageSize));
        return t;
    }

    /// <summary>Výchozí sekce „Obsah" přes celou tiskovou plochu stránky — sdílená novou i sample šablonou.</summary>
    private static Section CreateDefaultSection(PageSize pageSize) => new()
    {
        Name = "Obsah",
        PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
        DesignerHeight = pageSize.PrintableHeight
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// SectionVM
// ─────────────────────────────────────────────────────────────────────────────

public class SectionVM : INotifyPropertyChanged
{
    private readonly TemplateDesignerViewModel _vm;
    public Section Section { get; }

    /// <summary>Global callback for showing section properties dialog (wired by code-behind).</summary>
    public static Action<Section>? ShowPropertiesGlobal   { get; set; }
    public static Action<Section>? SaveTemplateGlobal     { get; set; }

    public SectionVM(Section section, TemplateDesignerViewModel vm)
    {
        Section = section;
        _vm     = vm;
    }

    public string  Name      => Section.Name;
    public bool    IsDynamic => Section.DesignerHeight is null;
    public bool    IsActive  => _vm.State.ActiveSection == Section;
    public bool    HasSubtitle => Subtitle is not null;

    public string? Subtitle
    {
        get
        {
            string? sub = null;
            if (Section.PrintBehavior != SectionPrintBehavior.Once)
                sub = Section.PrintBehavior switch
                {
                    SectionPrintBehavior.OnEveryPage            => "každá stránka",
                    SectionPrintBehavior.OnFirstPageOnly        => "jen 1. stránka",
                    SectionPrintBehavior.OnLastPageOnly         => "jen posl. stránka",
                    SectionPrintBehavior.OnEveryPageExceptFirst => "kromě 1. stránky",
                    _ => null
                };
            if (Section.PrintBehavior == SectionPrintBehavior.PerDataRow && !string.IsNullOrEmpty(Section.DataSource))
                sub = (sub is null ? "" : sub + " · ") + Section.DataSource;
            return sub;
        }
    }

    public bool CanMoveUp   => _vm.State.CurrentTemplate.Sections.IndexOf(Section) > 0;
    public bool CanMoveDown => _vm.State.CurrentTemplate.Sections.IndexOf(Section)
                               < _vm.State.CurrentTemplate.Sections.Count - 1;

    private RelayCommand? _moveUp, _moveDown, _delete, _saveTemplate, _activate, _showProps;

    public RelayCommand MoveUpCommand       => _moveUp       ??= new(() => _vm.MoveSection(Section, -1),   () => CanMoveUp);
    public RelayCommand MoveDownCommand     => _moveDown     ??= new(() => _vm.MoveSection(Section, +1),   () => CanMoveDown);
    public RelayCommand DeleteCommand       => _delete       ??= new(() => _vm.DeleteSection(Section));
    public RelayCommand SaveTemplateCommand => _saveTemplate ??= new(() => SaveTemplateGlobal?.Invoke(Section));
    public RelayCommand ActivateCommand     => _activate     ??= new(() => _vm.ActivateSection(Section));
    public RelayCommand ShowPropertiesCommand => _showProps  ??= new(() => ShowPropertiesGlobal?.Invoke(Section));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ─────────────────────────────────────────────────────────────────────────────
// Schema ViewModels
// ─────────────────────────────────────────────────────────────────────────────

public class SchemaGroupVM
{
    public string                     Header { get; }
    public IReadOnlyList<SchemaFieldVM> Fields { get; }

    public SchemaGroupVM(string header, IEnumerable<SchemaFieldVM> fields)
    {
        Header = header;
        Fields = [.. fields];
    }
}

public class SchemaFieldVM
{
    public string FieldName   { get; }
    public string Label       { get; }
    public string Placeholder => $"{{{FieldName}}}";
    public string LabelDisplay => string.IsNullOrEmpty(Label) ? "" : "  " + Label;

    public SchemaFieldVM(string fieldName, string label)
    {
        FieldName = fieldName;
        Label     = label;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ParameterControlVM
// ─────────────────────────────────────────────────────────────────────────────

public class ParameterControlVM : INotifyPropertyChanged
{
    private readonly TemplateParameter           _param;
    private readonly Dictionary<string, object?> _values;
    private readonly Action?                     _onChanged;
    private readonly Action?                     _onCommit;
    private bool                                 _isVisible = true;

    public ParameterControlVM(TemplateParameter param, Dictionary<string,object?> values,
        Action? onChanged, Action? onCommit = null)
    {
        _param     = param;
        _values    = values;
        _onChanged = onChanged;
        _onCommit  = onCommit;
    }

    /// <summary>
    /// Naplní kolekci VM pro daný seznam parametrů + sdílený <paramref name="values"/> slovník.
    /// Auto-zapojuje re-vyhodnocení viditelnosti všech VM po každé změně (volá se PŘED <paramref name="onChanged"/>)
    /// a provede initial visibility check po naplnění. Sjednocuje wiring mezi preview lištou (MainWindow) a PrintDialog.
    /// </summary>
    /// <param name="target">Cílová kolekce (typicky ObservableCollection napojená na ItemsControl).</param>
    /// <param name="parameters">Definice parametrů agendy/šablony.</param>
    /// <param name="values">Slovník hodnot — sdílený mezi VM (každá změna ho aktualizuje).</param>
    /// <param name="onChanged">Volitelný callback po změně hodnoty (např. invalidace cache + překreslení).</param>
    /// <param name="commitFactory">Volitelný factory — pro každý parametr může vrátit commit akci (Načíst tlačítko).</param>
    public static void Init(
        ICollection<ParameterControlVM>      target,
        IEnumerable<TemplateParameter>       parameters,
        Dictionary<string, object?>          values,
        Action?                              onChanged     = null,
        Func<TemplateParameter, Action?>?    commitFactory = null)
    {
        target.Clear();
        foreach (var p in parameters)
        {
            // Auto-refresh visibility všech VM před externím callbackem — VisibleWhen rule ve VisualWhen
            // se musí přepočítat dřív, než UI dostane signál k překreslení.
            Action vmOnChanged = () =>
            {
                foreach (var vm in target) vm.UpdateVisibility();
                onChanged?.Invoke();
            };
            target.Add(new ParameterControlVM(p, values, vmOnChanged, commitFactory?.Invoke(p)));
        }

        // Initial visibility check — proběhne až po naplnění kolekce, aby VisibleWhen vidělo všechny VM.
        foreach (var vm in target) vm.UpdateVisibility();
    }

    public string                    Label         => _param.Label;
    public string                    ParameterType => _param.Type;
    public IReadOnlyList<string>     Options       => _param.Options ?? [];

    /// <summary>Hodnota parametru — setter volá onChanged (live refresh) při každé změně.</summary>
    public object? Value
    {
        get => _values.TryGetValue(_param.Name, out var v) ? v : _param.GetDefaultTyped();
        set { _values[_param.Name] = value; OnPropertyChanged(); _onChanged?.Invoke(); }
    }

    /// <summary>
    /// Notifikuje UI binding po externí změně <c>_values[Name]</c> (např. po loaded PrintParams).
    /// Bez volání setteru se neaktivuje OnPropertyChanged — proto explicit notifikace.
    /// </summary>
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));

    /// <summary>Viditelnost parametru — řízena dependency rule VisibleWhen z TemplateParameter.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }

    /// <summary>Vyhodnotí VisibleWhen rule proti aktuálním hodnotám. Volá se po každé změně libovolného parametru.</summary>
    public void UpdateVisibility() => IsVisible = _param.IsVisible(_values);

    /// <summary>Má se volat při potvrzení hodnoty (Enter/LostFocus) — spustí těžší operace (reload dat).</summary>
    public bool HasCommit => _onCommit is not null;

    private RelayCommand? _commitCmd;
    public RelayCommand CommitCommand => _commitCmd ??= new(() => _onCommit?.Invoke());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Selector přesunut do Controls/ParameterTemplateSelector.cs (sdílený mezi MainWindow a PrintDialog).
