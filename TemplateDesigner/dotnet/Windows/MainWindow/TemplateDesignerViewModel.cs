using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

/// <summary>
/// ViewModel hlavního okna návrháře. Rozdělený do částí podle oblastí (soubory
/// <c>TemplateDesignerViewModel.*.cs</c>: <c>Instances</c>, <c>DevFirm</c>, <c>PreviewData</c>, <c>Columns</c>, <c>Sections</c>, <c>Files</c>, <c>Launch</c>, <c>Editing</c>, <c>Parameters</c>, <c>Selection</c>, <c>SampleTemplate</c>, <c>StartPanel</c>); tady je stav, kolekce, vlastnosti, příkazy a konstruktor.
/// </summary>
public partial class TemplateDesignerViewModel : EDViewModel
{
    // ── Designer state (public - code-behind accesses canvas/resize directly) ─
    public DesignerState State { get; } = new();

    // ── File & path ───────────────────────────────────────────────────────────
    public string? CurrentFilePath { get; private set; }

    // ── Dirty tracking ──────────────────────────────────────────────────────
    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            // Uživatel začal kreslit - úvodní obrazovka už jen překáží.
            if (value) StartPanel.Hide();
            RequestTitleUpdate?.Invoke();
        }
    }

    // ── Providers ─────────────────────────────────────────────────────────────
    public  IDataSchemaProvider    SchemaProvider        { get; }
    public  CacheAgendaProvider?   CacheProvider         { get; }

    /// <summary>Datové MLRest připojení, které používá <see cref="CacheProvider"/> (a úložiště šablon).
    /// Drženo kvůli dev přepínání firem za běhu (<see cref="DevSwitchFirm"/>).</summary>
    private readonly MLRest? _cacheMlRest;
    public  Services.CacheTemplateService? TemplateStorage { get; }

    /// <summary>Pasivní AI průvodce (záložka v levém panelu). Čte živý kontext při každém dotazu.</summary>
    public  Windows.AiGuideDialog.AiGuidePanelViewModel AiGuide { get; }
    private readonly SectionTemplateManager _sectionTemplates;


    // ── Clipboard ─────────────────────────────────────────────────────────────
    private List<TemplateElement> _clipboard = [];

    // ── Observable collections (drive XAML data-binding) ──────────────────────
    public ObservableCollection<SectionVM>          Sections          { get; } = [];
    public ObservableCollection<SchemaGroupVM>      SchemaGroups      { get; } = [];
    public ObservableCollection<ParameterControlVM> PreviewParameters { get; } = [];

    // ── Bindable properties ───────────────────────────────────────────────────

    private bool _showFieldsEmptyState;
    /// <summary>Panel „Datová pole" nemá co zobrazit z agendy (bez agendy/DB nebo obecná šablona) → ukázat empty-state místo prázdna.</summary>
    public bool ShowFieldsEmptyState { get => _showFieldsEmptyState; private set => Set(ref _showFieldsEmptyState, value); }
    public bool CanUndo => State.CanUndo;
    public bool CanRedo => State.CanRedo;

    private string _statusText = "";
    /// <summary>Transientní „akce/progres" zóna status baru (uloženo / chyba / export N/N / migrace).
    /// Odděleno od <see cref="SelectionStatusText"/>, aby se navzájem nepřepisovaly - akční hláška
    /// zůstane viditelná i když uživatel mezitím klikne jinam a změní se stav výběru.</summary>
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    // ── Hlášky vpravo dole (`B178`) ─────────────────────────────────────────

    /// <summary>Odkud hláška přišla - každý zdroj se přepočítává v jinou chvíli, proto zvlášť.</summary>
    public enum ZdrojHlaseni { Data, Font, Preteceni, Orez, Sloupce, Sazba, Agenda }

    private readonly Dictionary<ZdrojHlaseni, IReadOnlyList<Hlaseni>> _hlaseniZeZdroju = [];

    /// <summary>
    /// Nastaví hlášky jednoho zdroje. Data (sloupce odjezdů, zmenšené písmo, malé kódy) se počítají
    /// při načtení dat, font při výměně šablony, přetečení / ořez / úzké sloupce při kreslení náhledu.
    /// </summary>
    public void NastavHlaseni(ZdrojHlaseni zdroj, IReadOnlyList<Hlaseni> hlaseni)
    {
        var stare = _hlaseniZeZdroju.GetValueOrDefault(zdroj) ?? [];
        if (stare.SequenceEqual(hlaseni)) return;
        _hlaseniZeZdroju[zdroj] = hlaseni;
        Hlaseni = [.. _hlaseniZeZdroju.Values.SelectMany(h => h)
            .OrderByDescending(h => h.Zavaznost)];
        OnPropertyChanged(nameof(Hlaseni));
        OnPropertyChanged(nameof(HasHlaseni));
        foreach (var n in new[] { nameof(PocetChyb), nameof(PocetUpozorneni), nameof(PocetInformaci),
                                  nameof(HasChyby), nameof(HasUpozorneni), nameof(HasInformace),
 })
            OnPropertyChanged(n);
        if (Hlaseni.Count == 0) IsHlaseniListOpen = false;
    }

    /// <summary>Všechny hlášky, nejvážnější první.</summary>
    public IReadOnlyList<Hlaseni> Hlaseni { get; private set; } = [];

    public bool HasHlaseni => Hlaseni.Count > 0;
    public bool HasChyby => Pocet(ZavaznostHlaseni.Chyba) > 0;
    public bool HasUpozorneni => Pocet(ZavaznostHlaseni.Upozorneni) > 0;
    public bool HasInformace => Pocet(ZavaznostHlaseni.Informace) > 0;

    // Jeden kompaktní blok (rozhodnutí Jan 24. 9.): ikony s počty podle závažnosti, celé znění
    // je v rozbaleném seznamu.
    public int PocetChyb => Pocet(ZavaznostHlaseni.Chyba);
    public int PocetUpozorneni => Pocet(ZavaznostHlaseni.Upozorneni);
    public int PocetInformaci => Pocet(ZavaznostHlaseni.Informace);

    private int Pocet(ZavaznostHlaseni z) => Hlaseni.Count(h => h.Zavaznost == z);

    private bool _hlaseniListOpen;
    private DateTime _hlaseniListZavrenoV = DateTime.MinValue;

    /// <summary>Je rozbalený seznam hlášek? Klik na odznak ho otevře, klik mimo zavře.</summary>
    public bool IsHlaseniListOpen
    {
        get => _hlaseniListOpen;
        set
        {
            if (_hlaseniListOpen == value) return;
            _hlaseniListOpen = value;
            if (!value) _hlaseniListZavrenoV = DateTime.UtcNow;
            OnPropertyChanged(nameof(IsHlaseniListOpen));
        }
    }

    /// <summary>Klik na řádek seznamu vybere prvek, ke kterému hláška patří. Host (okno) ho najde
    /// v šabloně a aktivuje jeho sekci.</summary>
    public Action<Guid>? RequestSelectElement { get; set; }

    private string _selectionStatusText = "";
    /// <summary>Klidová „stav výběru" zóna status baru (aktuální element / sekce). Píše ji jen
    /// <see cref="UpdateStatusForSelection"/>; mění se volně s výběrem, nepřepíše akční hlášku.</summary>
    public string SelectionStatusText
    {
        get => _selectionStatusText;
        set => Set(ref _selectionStatusText, value);
    }


    private string _agendaNameText = "Agenda: -";
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
    private RelayCommand? _undo, _redo, _delete, _copy, _paste, _cut, _copyStyle, _pasteStyle, _prevInstance, _nextInstance, _refreshData, _toggleParamsBar, _instanceList;
    private RelayCommand<int>? _selectInstance;
    private AsyncRelayCommand? _save, _saveAs, _open, _openFromCache, _saveToCache, _saveToCacheAs, _importJson, _exportJson, _new, _changeAgenda, _openInstancePicker;

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
    public AsyncRelayCommand OpenInstancePickerCommand => _openInstancePicker ??= CommandAsync(OpenInstancePickerAsync);
    // Šipka rozbaluje seznam vybraných instancí, jméno instance uprostřed chipu otevírá jejich
    // výběr jako dřív (rozhodnutí Jan 21. 9.) - dvě různé věci, dvě různá místa.
    public RelayCommand OpenInstanceListCommand => _instanceList ??= Command(() =>
    {
        if (DateTime.UtcNow - _instanceListZavrenoV < TimeSpan.FromMilliseconds(250)) return;
        IsInstanceListOpen = !IsInstanceListOpen;
    });
    public RelayCommand OpenHlaseniListCommand => _hlaseniList ??= Command(() =>
    {
        if (DateTime.UtcNow - _hlaseniListZavrenoV < TimeSpan.FromMilliseconds(250)) return;
        IsHlaseniListOpen = !IsHlaseniListOpen;
    });
    private RelayCommand? _hlaseniList;
    public RelayCommand<Hlaseni> VybratPrvekHlaseniCommand => _vybratPrvekHlaseni ??= Command<Hlaseni>(h =>
    {
        if (h?.PrvekId is not { } id) return;
        IsHlaseniListOpen = false;
        RequestSelectElement?.Invoke(id);
    });
    private RelayCommand<Hlaseni>? _vybratPrvekHlaseni;
    public RelayCommand<int> SelectInstanceCommand => _selectInstance ??= Command<int>(i =>
    {
        IsInstanceListOpen = false;
        ActiveInstanceIndex = i;
    });
    public RelayCommand PreviousInstanceCommand    => _prevInstance   ??= Command(() => ActiveInstanceIndex--, () => HasMultipleActiveInstances && _activeInstanceIndex > 0);
    public RelayCommand NextInstanceCommand        => _nextInstance   ??= Command(() => ActiveInstanceIndex++, () => HasMultipleActiveInstances && _activeInstanceIndex < _activeInstances.Count - 1);
    public RelayCommand RefreshDataCommand         => _refreshData    ??= Command(RefreshData);
    public RelayCommand ToggleParamsBarCommand     => _toggleParamsBar ??= Command(() => ParamsBarExpanded = !ParamsBarExpanded);
    public new RelayCommand DeleteCommand => _delete    ??= Command(DeleteSelected);
    public RelayCommand CopyCommand     => _copy      ??= Command(CopySelected);
    public RelayCommand CutCommand      => _cut       ??= Command(CutSelected);
    public RelayCommand PasteCommand    => _paste     ??= Command(PasteClipboard);
    public RelayCommand CopyStyleCommand  => _copyStyle  ??= Command(CopyStyle);
    public RelayCommand PasteStyleCommand => _pasteStyle ??= Command(PasteStyle, () => State.StyleClipboard is not null);

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Agenda, se kterou se návrhář otevře. Agenda ze shellu bez dalších hodnot (uzel stromu JŘ / MHD,
    /// tlačítko MHD2) říká jen, **z kterého modulu** uživatel přichází: otevře se agenda téže varianty
    /// (AD / MHD), se kterou pracoval naposledy, napoprvé ta ze shellu (`B194e` A2, Jan 27. 10.) - kdo
    /// dělá linkové JŘ, se dřív musel po každém spuštění přepínat. Spuštění s linkou (porovnání linek)
    /// určuje agendu přesně. Neznámý kód se přeskočí.
    /// </summary>
    /// <remarks>
    /// ⚠️ Poslední použitá agenda se obnoví jen když je ZVEŘEJNĚNÁ. Bez té podmínky se skrytá agenda
    /// vrátila každému, kdo ji kdy vyzkoušel (nález Jan 1. 9.). Kód ze shellu filtr nemá: je to příkaz
    /// zvenčí a skrytá agenda tam znamená chybu konfigurace shellu, ne zapamatovaný stav uživatele.
    /// </remarks>
    internal static string? StartovniAgenda(IReadOnlyList<AgendaRegistryEntry> agendy, string? zeShellu,
        bool zeShelluJenModul, string? posledni)
    {
        AgendaRegistryEntry? Najdi(string? kod, bool jenZverejnene) => kod is null ? null
            : agendy.FirstOrDefault(a => string.Equals(a.Code, kod, StringComparison.OrdinalIgnoreCase)
                                        && (!jenZverejnene || a.Released));
        var shell = Najdi(zeShellu, jenZverejnene: false);
        var minula = Najdi(posledni, jenZverejnene: true);
        if (shell is null) return minula?.Code;
        bool tyzModul = zeShelluJenModul && minula is not null && shell.Variant is { Length: > 0 }
                        && string.Equals(minula.Variant, shell.Variant, StringComparison.OrdinalIgnoreCase);
        return tyzModul ? minula!.Code : shell.Code;
    }

    /// <summary>
    /// Per-user state (zoom, grid, splittery, recent files, instance defaults).
    /// Null v testech / standalone bez Caché - get pro UI binding loaded files menu;
    /// service interní fallback na defaults i bez Caché.
    /// </summary>
    public TdPreferencesService? Preferences { get; }

    public TemplateDesignerViewModel(MLRest? mlRest = null,
                                     TdPreferencesService? preferences = null,
                                     IReadOnlyList<AgendaRegistryEntry>? agendas = null,
                                     string? startupAgendaCode = null,
                                     bool startupJenModul = false)
    {
        if (mlRest is not null) _services.MLRest = new MLRestAdapter(mlRest);
        StartPanel = new StartPanelViewModel(OpenRecentTemplateAsync, OpenFromCacheAsync,
            StartFromSystemTemplateAsync, ChangeAgendaFromStartPanelAsync, StartBlankFromStartPanelAsync);

        // Výměna šablony je jediný okamžik, kdy může přibýt font, který tenhle počítač nemá.
        // Psaní vlastního názvu do pole fontu se sem záměrně nenapojuje: nabídka jmenuje jen
        // nainstalované a záměna je vidět hned na plátně; před tiskem to dialog kontroluje znovu.
        State.TemplateSwapped = () =>
        {
            NastavHlaseni(ZdrojHlaseni.Font,
                FontWarnings.Hlaska(State.CurrentTemplate) is { } font ? [font] : []);
            ZkontrolujPrvkyAgendy();
            StartPanel.Hide();
        };

        Preferences = preferences;

        var settings   = AppSettings.Load();
        var schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");
        _sectionTemplates = new SectionTemplateManager();

        var resolvedMlRest = mlRest ?? CacheSchemaWiring.TryCreateMlRest(settings);
        _cacheMlRest = resolvedMlRest;
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

        // Startovní agenda: ze shellu (inputParam "Agenda=<kód>"), poslední použitá uživatele,
        // jinak fallback ze CreateSampleTemplate - pravidla viz StartovniAgenda.
        var startAgenda = StartovniAgenda(resolvedAgendas, startupAgendaCode, startupJenModul,
            preferences?.MainState?.LastAgendaCode);
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
    /// Volá se v konstruktoru a znovu po dokončení LoadAsync (event Loaded) - pro cold-start scénář
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
        ParamsBarExpanded        = pref.ParamsBarExpanded;

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
            LastAgendaCode:     State.CurrentTemplate.AgendaCode,
            ParamsBarExpanded:  ParamsBarExpanded);

        return Preferences.SaveMainStateAsync(snapshot);
    }

    // ── Execute wrapper ───────────────────────────────────────────────────────
    public void Execute(IDesignerCommand cmd)
    {
        State.Execute(cmd);
        IsDirty = true;
        // I nad už neuloženou šablonou (obrazovka otevřená z menu) - IsDirty se tu nezmění.
        StartPanel.Hide();
        NotifyUndoRedo();
        RequestPropertiesPanelUpdate?.Invoke();
        // Jediné místo, kde příkaz může pohnout definicí sloupců (dialog, přidání/smazání/vložení
        // prvku odjezdů) - definice se posílá do GetData, bez re-fetche by náhled ukazoval data
        // staré definice a tisk by vyjel jinak.
        RefetchIfColumnsDefinitionChanged();
        RequestParamsVisibilityRefresh?.Invoke();
        ZkontrolujPrvkyAgendy();
    }

    /// <summary>Vykoná seznam příkazů jako jeden undo krok (1 příkaz → přímo, víc → CompositeCommand).
    /// Prázdný seznam = no-op.</summary>
    private void ExecuteAsComposite(string label, List<IDesignerCommand> cmds)
    {
        if (cmds.Count == 0) return;
        Execute(cmds.Count == 1 ? cmds[0] : new CompositeCommand(label, cmds));
    }

    /// <summary>Příkaz jen pro hodnotu, která se opravdu změnila - OK okna beze změny nesmí přidat krok
    /// do Ctrl+Z ani označit šablonu jako změněnou (`B194d` E13d). Okno seznamy sloupců skládá znovu,
    /// proto se porovnává obsah, ne instance.</summary>
    private static void AddIfChanged<T>(List<IDesignerCommand> cmds, string label, T oldValue, T newValue,
        Action<object?> apply)
    {
        if (JsonSerializer.Serialize(oldValue) == JsonSerializer.Serialize(newValue)) return;
        cmds.Add(new SetPropertyCommand(label, oldValue, newValue, apply));
    }
}
