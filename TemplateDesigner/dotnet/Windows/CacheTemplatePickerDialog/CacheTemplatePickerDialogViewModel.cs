using EDControls.EDList;
using EDControls.EDViewModel;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// ViewModel okna šablon v databázi (Ctrl+O). Vlevo strom podle oblasti práce, agendy a varianty
/// (<c>ALVA.TemplateDesigner.API.StromSablon</c>, `B190`), vpravo seznam šablon vybraného uzlu (EDList nad
/// <c>ALVA.TemplateDesigner.EDList.Sablony</c>). VM drží strom, CRUD akce, výchozí šablonu pro tisk a OK output.
/// </summary>
public class CacheTemplatePickerDialogViewModel : EDViewModel
{
    private readonly CacheTemplateService _service;
    private TemplateCatalog _catalog = TemplateCatalog.Empty;

    /// <summary>Uzly stromu; do načtení ze serveru jen Všechny šablony.</summary>
    public IReadOnlyList<TemplateCatalogNodeVM> Nodes { get; private set; } = TemplateCatalogNodeVM.Build(TemplateCatalog.Empty);

    private string _selectedNodeKey;
    /// <summary>Klíč vybraného uzlu; volající si ho zapamatuje pro příští otevření.</summary>
    public string SelectedNodeKey => _selectedNodeKey;

    private TemplateCatalogNodeVM? _selectedNode;
    /// <summary>Code-behind plní ze stromu. Jiný uzel = nový filtr seznamu.</summary>
    public TemplateCatalogNodeVM? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedNode)) return;
            bool changed = value.Key != _selectedNodeKey;
            _selectedNode = value;
            _selectedNodeKey = value.Key;
            OnPropertyChanged();
            NotifySelectionTexts();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            if (changed) RequestReload?.Invoke();
        }
    }

    /// <summary>Pruh nad seznamem: co je ve stromu vybrané, ať uživatel ví, ve které agendě je.</summary>
    public string ListTitle => TitleFor(Nodes, _selectedNode);

    /// <summary>Řádek pod seznamem: co uživatel nad výběrem může udělat - jak založit novou šablonu, která jde jen ve
    /// vybrané agendě a variantě, a proč šablonu M-line nejde upravit ani odstranit.</summary>
    public string Hint => HintFor(Nodes, _selectedNode, IsSelectedSystem) + PrintDefaultHint(_selectedNode, ShowsPrintDefault);

    /// <summary>Proč je šablona tučně a jak výchozí přesunout - jen u uzlu jedné agendy se zapnutým tiskem z okna JŘ.</summary>
    public static string PrintDefaultHint(TemplateCatalogNodeVM? node, bool showsPrintDefault) =>
        showsPrintDefault && node?.DefaultName is { Length: > 0 } nazev
            ? $" Tučně je výchozí šablona pro tisk z okna jízdních řádů ({nazev}); jinou nastavíte akcí Nastavit jako výchozí pro tisk."
            : "";

    /// <summary>Varianta s agendou („Linkový JŘ · AD"), jinak název uzlu.</summary>
    public static string TitleFor(IReadOnlyList<TemplateCatalogNodeVM> nodes, TemplateCatalogNodeVM? node) =>
        node is null ? "Všechny šablony"
        : node.Kind == TemplateCatalogNodeKind.Variant
            ? string.Join(" · ", TemplateCatalogNodeVM.PathTo(nodes, node).Skip(1).Select(n => n.Label))
            : node.Label;

    public static string HintFor(IReadOnlyList<TemplateCatalogNodeVM> nodes, TemplateCatalogNodeVM? node,
        bool selectedIsMline = false) =>
        selectedIsMline
            ? "Šablona M-line je jen ke čtení, upravit ani odstranit ji nejde. Otevřete ji jako kopii (OK nebo dvojklik) "
              + "a uložte jako svou."
        : node?.NewTemplateAgendaCode is null
            ? "Šablonu otevřete tlačítkem OK nebo dvojklikem. Novou šablonu založíte, až vlevo vyberete agendu, "
              + "u jízdních řádů i AD nebo MHD."
        : node.Count == 0
            ? $"{TitleFor(nodes, node)} zatím nemá žádnou šablonu. Prázdnou šablonu založíte ikonou Nový (Ctrl+N) nad seznamem."
        : "Šablonu otevřete tlačítkem OK nebo dvojklikem, šablona M-line se otevře jako kopie a uložíte ji jako svou. "
          + $"Prázdnou šablonu pro {TitleFor(nodes, node)} založíte ikonou Nový (Ctrl+N) nad seznamem.";

    private void NotifySelectionTexts()
    {
        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(Hint));
    }

    /// <summary>Filtr seznamu šablon podle vybraného uzlu; Všechny šablony = bez filtru.</summary>
    public IReadOnlyDictionary<string, string> CurrentFilter =>
        _selectedNode is { AgendaCodes.Count: > 0 } node
            ? new Dictionary<string, string> { ["agendaCodes"] = node.FilterValue }
            : new Dictionary<string, string>();

    private EDListRow? _selected;
    /// <summary>Code-behind plní z <c>EDList.SelectionChanged</c>.</summary>
    public EDListRow? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                OnPropertyChanged(nameof(Hint));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>OK spodní lišty otevře vybranou šablonu; bez výběru jen poradí (lišta OK nevypíná).</summary>
    protected override void OnOk()
    {
        if (Selected is null) { PopupDisplayer.Info("Vyberte šablonu, kterou chcete otevřít."); return; }
        Open();
    }

    /// <summary>Vybraná šablona po stisku OK nebo dvojkliku. Null = dialog zrušen.</summary>
    public Guid? SelectedTemplateId { get; private set; }

    /// <summary>Agenda nové prázdné šablony po stisku Nová šablona. Null = otevírá se šablona nebo dialog zrušen.</summary>
    public string? NewTemplateAgendaCode { get; private set; }

    private readonly Dictionary<Guid, string> _renamed = [];
    /// <summary>Šablony přejmenované v okně (i když bylo zrušeno). Otevřená šablona si z něj vezme nový název,
    /// jinak by ho Ctrl+S přepsal zpátky.</summary>
    public IReadOnlyDictionary<Guid, string> Renamed => _renamed;

    /// <summary>Po Otevřít: byla vybraná šablona systémová (M-line)? Řídí načtení z MLINE větve
    /// + otevření jako „kopie" (Ctrl+S → Uložit jako do firmy).</summary>
    public bool SelectedIsSystem { get; private set; }

    /// <summary>Aktuálně vybraný řádek je systémová (M-line) šablona → read-only (nejde mazat/přejmenovat).</summary>
    private bool IsSelectedSystem => string.Equals(Selected?["zdroj"], "M-line", StringComparison.Ordinal);

    public RelayCommand      OpenCommand   { get; }
    public RelayCommand      NewTemplateCommand { get; }
    public AsyncRelayCommand DeleteTemplateCommand { get; }
    public AsyncRelayCommand EditTemplateCommand { get; }
    public AsyncRelayCommand SetDefaultCommand { get; }

    // ── Výchozí šablona pro tisk ──────────────────────────────────────────────

    /// <summary>Okno ukazuje výchozí šablonu pro tisk (tučně, akce, věta pod seznamem) jen se zapnutým tiskem z okna
    /// jízdních řádů - bez něj by nastavení nemělo účinek a mátlo (Jan 3. 11.).</summary>
    public bool ShowsPrintDefault => _catalog.PrintFromModule;

    /// <summary>Šablona jde nastavit jako výchozí pro tisk: agenda se tiskne z modulu a šablona výchozí ještě není
    /// (sloupec <c>vychozi</c> prázdný). Výchozí je vždy právě jedna a jen se přesouvá, zrušit ji nejde (Jan 3. 11.).</summary>
    public static bool CanSetDefault(string? vychozi, bool canPrint) => canPrint && string.IsNullOrEmpty(vychozi);

    private bool CanPrint(string? agendaCode) =>
        _catalog.Areas.SelectMany(a => a.Agendas).SelectMany(a => a.Variants)
            .Any(v => v.Code == agendaCode && v.CanPrint);

    private async Task SetDefaultAsync()
    {
        if (Selected is null || !Guid.TryParse(Selected.Id, out var id)) return;
        var agendaCode = Selected["agendaCode"];
        if (string.IsNullOrEmpty(agendaCode)) return;
        try
        {
            await _service.SetDefaultTemplateAsync(agendaCode, id);
            await LoadCatalogAsync();
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Nový, Upravit, Odstranit a se zapnutým tiskem z okna JŘ za oddělovačem výchozí šablona pro tisk.</summary>
    private void BuildActions()
    {
        List<EDAction> akce = [StandardActions.Add(NewTemplateCommand), StandardActions.Edit(EditTemplateCommand),
                               StandardActions.Delete(DeleteTemplateCommand)];
        if (ShowsPrintDefault)
            akce.AddRange([EDAction.Separator,
                           StandardActions.Custom("Nastavit jako výchozí pro tisk", SetDefaultCommand, "EDIcons.Star")]);
        Actions = akce;
        OnPropertyChanged(nameof(Actions));
    }

    /// <summary>
    /// Načte strom ze serveru a vybere naposledy vybraný uzel (jinak Všechny šablony). Seznam nenačítá - volající
    /// pak zavolá <see cref="RequestReload"/>, aby se seznam načetl jednou. Bez stromu (chyba serveru) zůstanou
    /// Všechny šablony a seznam funguje jako dřív.
    /// </summary>
    public async Task LoadCatalogAsync()
    {
        try { _catalog = await _service.GetCatalogAsync(); }
        catch (CacheTemplateException ex)
        {
            _catalog = TemplateCatalog.Empty;
            PopupDisplayer.Error(ex);
        }
        Nodes = TemplateCatalogNodeVM.Build(_catalog);
        OnPropertyChanged(nameof(Nodes));
        OnPropertyChanged(nameof(ShowsPrintDefault));
        BuildActions();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();

        // Uzel se vybere dřív, než ho strom ukáže; strom pak výběr ohlásí stejným uzlem a seznam se znovu nenačte.
        // Předvybraná agenda platí jen při otevření - po smazání nebo přejmenování zůstane uzel, kde uživatel je.
        var target = TemplateCatalogNodeVM.FindAgenda(Nodes, _preselectAgendaCode)
                     ?? TemplateCatalogNodeVM.Find(Nodes, _selectedNodeKey) ?? Nodes[0];
        _preselectAgendaCode = null;
        foreach (var node in TemplateCatalogNodeVM.PathTo(Nodes, target).SkipLast(1))
            node.IsExpanded = true;
        _selectedNode = target;
        _selectedNodeKey = target.Key;
        target.IsSelected = true;
        OnPropertyChanged(nameof(SelectedNode));
        NotifySelectionTexts();
    }

    // ── Nové šablony od M-line (distribuční kanál) ────────────────────────────

    private readonly SystemTemplateChannelSync _channel;
    private SystemTemplateChannelSync.Available? _available;

    /// <summary>Je co stáhnout? Řídí viditelnost nabídky nad seznamem.</summary>
    public bool HasAvailable => _available?.Any == true;

    /// <summary>Věta pro uživatele - kolik a čeho je k dispozici, se správným skloňováním.</summary>
    public string AvailableText
    {
        get
        {
            if (_available is not { Any: true } a) return "";

            var casti = new List<string>();
            if (a.New.Count > 0) casti.Add(Pocet(a.New.Count, "nová", "nové", "nových"));
            if (a.Updated.Count > 0) casti.Add(Pocet(a.Updated.Count, "aktualizovaná", "aktualizované", "aktualizovaných"));

            // Sloveso se řídí celkem: 1 → „je", 2-4 → „jsou", 5 a víc → zase „je".
            string sloveso = a.Count is >= 2 and <= 4 ? "jsou" : "je";
            return $"K dispozici {sloveso} {string.Join(" a ", casti)} od M-line.";
        }
    }

    /// <summary>Počet se šablonami v české deklinaci: 1 šablona, 2-4 šablony, 5+ šablon.</summary>
    internal static string Pocet(int n, string jedna, string dve, string vic) => n switch
    {
        1 => $"1 {jedna} šablona",
        >= 2 and <= 4 => $"{n} {dve} šablony",
        _ => $"{n} {vic} šablon"
    };

    private AsyncRelayCommand? _download;
    public AsyncRelayCommand DownloadCommand => _download ??= CommandAsync(DownloadAsync);

    /// <summary>
    /// Zeptá se kanálu, co je k dispozici. Běží na pozadí a <b>nikdy neblokuje</b> - bez sítě, bez
    /// klíče nebo při výpadku helpdesku se nabídka prostě neukáže a přehled funguje jako dřív.
    /// </summary>
    public async Task CheckChannelAsync()
    {
        try { _available = await _channel.CheckAsync(); }
        catch (Exception) { _available = null; }
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(AvailableText));
    }

    /// <summary>
    /// Stáhne vše, co je k dispozici, do sdílené větve - tedy <b>pro celou firmu naráz</b>, ne jen
    /// pro toho, kdo klikl. Vlastní kopie, které si kdokoli ze systémové šablony udělal, zůstávají
    /// nedotčené (jsou to samostatné záznamy).
    /// </summary>
    private async Task DownloadAsync()
    {
        if (_available is not { Any: true } a) return;

        int ulozeno = await _channel.DownloadAsync([.. a.New, .. a.Updated]);
        _available = null;
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(AvailableText));

        await LoadCatalogAsync();
        RequestReload?.Invoke();
        PopupDisplayer.Info(ulozeno > 0
            ? $"Staženo šablon: {ulozeno}. Najdete je v přehledu mezi systémovými (M-line)."
            : "Šablony se nepodařilo stáhnout - zkuste to prosím později.");
    }

    /// <summary>Code-behind napojí - VM žádá o načtení seznamu s <see cref="CurrentFilter"/> (nový uzel, Delete, Rename).</summary>
    public Action? RequestReload { get; set; }

    /// <summary>Code-behind napojí - VM žádá o formulář šablony (název, agenda, kód agendy) a dostane nový název,
    /// null = zrušeno. VM nezná WPF.</summary>
    public Func<string, string, string, string?>? RequestEditInput { get; set; }

    private string? _preselectAgendaCode;

    /// <param name="lastNodeKey">Uzel stromu vybraný při posledním otevření; null = Všechny šablony.</param>
    /// <param name="preselectAgendaCode">Agenda, jejíž uzel se vybere místo posledního (změna agendy); když ve stromu
    /// není, platí poslední uzel.</param>
    public CacheTemplatePickerDialogViewModel(CacheTemplateService service, string? lastNodeKey = null,
        string? preselectAgendaCode = null)
    {
        _service = service;
        _channel = new SystemTemplateChannelSync(service);
        _selectedNodeKey = lastNodeKey ?? TemplateCatalogNodeVM.AllKey;
        _preselectAgendaCode = preselectAgendaCode;

        // Systémové (M-line) jsou read-only - otevřít lze, upravit a odstranit ne.
        OpenCommand           = Command(Open,             () => Selected is not null);
        NewTemplateCommand    = Command(NewTemplate,      () => SelectedNode?.NewTemplateAgendaCode is not null);
        DeleteTemplateCommand = CommandAsync(DeleteAsync, () => Selected is not null && !IsSelectedSystem);
        EditTemplateCommand   = CommandAsync(EditAsync,   () => Selected is not null && !IsSelectedSystem);
        SetDefaultCommand     = CommandAsync(SetDefaultAsync,
            () => Selected is not null && CanSetDefault(Selected["vychozi"], CanPrint(Selected["agendaCode"])));

        // Ikony v pruhu nad seznamem a zkratky Ctrl+N/E/D jako seznamy legacy EDISONu.
        BuildActions();
    }

    private void Open()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        SelectedTemplateId = id;
        SelectedIsSystem = IsSelectedSystem;
        CloseDialog(true);
    }

    private void NewTemplate()
    {
        if (SelectedNode?.NewTemplateAgendaCode is not { } code) return;
        NewTemplateAgendaCode = code;
        CloseDialog(true);
    }

    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        var name = Selected["name"] ?? "(bez názvu)";
        if (!PopupDisplayer.Confirm($"Opravdu smazat šablonu „{name}\"?")) return;
        try
        {
            await _service.DeleteAsync(id);
            await LoadCatalogAsync();
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Formulář šablony (dnes název, agenda jen ke čtení); uloží se jen změněný název.</summary>
    private async Task EditAsync()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        var oldName = Selected["name"] ?? "";

        var newName = RequestEditInput?.Invoke(oldName, AgendaLabelOf(Selected), Selected["agendaCode"] ?? "");
        if (newName is null || newName == oldName) return;
        try
        {
            await _service.RenameAsync(id, newName);
            _renamed[id] = newName;
            await LoadCatalogAsync();
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    /// <summary>Agenda řádku, jak ji ukazuje seznam: „Linkový JŘ · AD", agenda bez variant jen názvem.</summary>
    internal static string AgendaLabelOf(EDListRow row) =>
        string.IsNullOrEmpty(row["varianta"]) ? row["agenda"] ?? "" : $"{row["agenda"]} · {row["varianta"]}";
}
