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
            bool actionsChange = (value.Kind == TemplateCatalogNodeKind.Received) != IsReceivedNode;
            _selectedNode = value;
            _selectedNodeKey = value.Key;
            OnPropertyChanged();
            NotifySelectionTexts();
            if (actionsChange)
            {
                BuildActions();
                OnPropertyChanged(nameof(ListSelectionMode));
            }
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            if (changed) RequestReload?.Invoke();
        }
    }

    /// <summary>Pruh nad seznamem: co je ve stromu vybrané, ať uživatel ví, ve které agendě je.</summary>
    public string ListTitle => TitleFor(Nodes, _selectedNode);

    /// <summary>Řádek pod seznamem: co uživatel nad výběrem může udělat - jak založit novou šablonu, která jde jen ve
    /// vybrané agendě a variantě, a proč šablonu M-line nejde upravit ani odstranit.</summary>
    public string Hint => IsReceivedNode
        ? ReceivedHint(Selected, _selectedNode?.Count, _checkedRows.Count)
        : HintFor(Nodes, _selectedNode, IsSelectedSystem) + PrintDefaultHint(_selectedNode, ShowsPrintDefault);

    /// <summary>Nápověda v Přijatých šablonách: bez výběru co uzel ukazuje, u zásilky od koho je, co obsahuje a zpráva;
    /// se zaškrtnutými zásilkami, že akce platí pro ně.</summary>
    public static string ReceivedHint(EDListRow? row, int? count, int checkedCount = 0)
    {
        if (row is null || row["prijata"] != "1")
            return count == 0
                ? "Žádná jiná firma vám teď šablony neposlala. Až nějaké pošle, najdete je tady."
                : "Šablony, které vám poslaly jiné firmy. Zaškrtnuté zásilky převezmete akcí Převzít a šablony pak najdete "
                  + "mezi vlastními šablonami ve své agendě. Zásilky, které nechcete, odmítněte.";
        var odeslal = string.IsNullOrEmpty(row["odeslal"]) ? "" : $" (poslal {row["odeslal"]})";
        var zprava = string.IsNullOrWhiteSpace(row["zprava"]) ? "" : $" Zpráva: {row["zprava"]}";
        var akce = checkedCount > 0
            ? $" Zaškrtnuté zásilky ({checkedCount}) převezmete akcí Převzít, nebo je odmítnete."
            : " Převezměte ji akcí Převzít, nebo ji odmítněte.";
        return $"Od firmy {row["zdroj"]}{odeslal}: {row["sablony"]}.{zprava}{akce}";
    }

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

    /// <summary>Filtr seznamu šablon podle vybraného uzlu; Všechny šablony = bez filtru, Přijaté šablony = zásilky.</summary>
    public IReadOnlyDictionary<string, string> CurrentFilter =>
        IsReceivedNode ? new Dictionary<string, string> { ["prijate"] = "1" }
        : _selectedNode is { AgendaCodes.Count: > 0 } node
            ? new Dictionary<string, string> { ["agendaCodes"] = node.FilterValue }
            : new Dictionary<string, string>();

    private bool IsReceivedNode => _selectedNode?.Kind == TemplateCatalogNodeKind.Received;

    /// <summary>Přijaté šablony mají zaškrtávání, aby šlo převzít nebo odmítnout víc zásilek naráz (Jan 3. 11.).</summary>
    public ListSelectionMode ListSelectionMode => IsReceivedNode ? ListSelectionMode.Check : ListSelectionMode.Single;

    /// <summary>Vybraný řádek je zásilka od jiné firmy, ne šablona - otevřít, upravit ani odstranit nejde.</summary>
    private bool IsSelectedReceived => Selected?["prijata"] == "1";

    private IReadOnlyList<EDListRow> _checkedRows = [];

    /// <summary>Code-behind plní z <c>EDList.CheckedRowsChanged</c>; nové načtení seznamu zaškrtnutí smaže.</summary>
    public void SetCheckedRows(IReadOnlyList<EDListRow> rows)
    {
        _checkedRows = rows;
        OnPropertyChanged(nameof(Hint));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Zásilky, kterých se týká Převzít a Odmítnout: zaškrtnuté, bez zaškrtnutí vybraná.</summary>
    internal static IReadOnlyList<Shipment> ShipmentsOf(IReadOnlyList<EDListRow> checkedRows, EDListRow? selected) =>
        (checkedRows.Count > 0 ? checkedRows : selected is null ? [] : [selected])
            .Where(r => r["prijata"] == "1")
            .Select(r => new Shipment(r.Id, r["name"] ?? "", r["zdroj"] ?? ""))
            .ToList();

    private IReadOnlyList<Shipment> ShipmentsToHandle => ShipmentsOf(_checkedRows, Selected);

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
        if (IsSelectedReceived)
        {
            PopupDisplayer.Info("Zásilku od jiné firmy převezmete akcí Převzít. Šablony pak otevřete z jejich agendy.");
            return;
        }
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

    /// <summary>Nový, Upravit, Odstranit, se zapnutým tiskem z okna JŘ za oddělovačem výchozí šablona pro tisk
    /// a se zapnutým sdílením za dalším odeslání šablon jiné firmě. V Přijatých šablonách jen Převzít a Odmítnout -
    /// řádek je zásilka, ne šablona.</summary>
    private void BuildActions()
    {
        if (IsReceivedNode)
        {
            Actions = [StandardActions.Custom("Převzít", AcceptShipmentCommand, "EDIcons.Save"),
                       StandardActions.Custom("Odmítnout", RejectShipmentCommand, "EDIcons.Delete")];
            OnPropertyChanged(nameof(Actions));
            return;
        }
        List<EDAction> akce = [StandardActions.Add(NewTemplateCommand), StandardActions.Edit(EditTemplateCommand),
                               StandardActions.Delete(DeleteTemplateCommand)];
        if (ShowsPrintDefault)
            akce.AddRange([EDAction.Separator,
                           StandardActions.Custom("Nastavit jako výchozí pro tisk", SetDefaultCommand, "EDIcons.Star")]);
        if (ShowsSharing)
            akce.AddRange([EDAction.Separator,
                           StandardActions.Custom("Odeslat jiné firmě…", SendTemplatesCommand, "EDIcons.Company")]);
        Actions = akce;
        OnPropertyChanged(nameof(Actions));
    }

    // ── Sdílení šablon mezi firmami (`B189`) ──────────────────────────────────

    /// <summary>Okno nabízí posílání šablon jiným firmám; do vydání jen na vývojovém serveru.</summary>
    public bool ShowsSharing => _catalog.Sharing;

    public AsyncRelayCommand SendTemplatesCommand { get; }

    /// <summary>Code-behind napojí - otevře okno pro odeslání. VM nezná WPF.</summary>
    public Action<SendTemplatesDialogViewModel>? RequestSendDialog { get; set; }

    /// <summary>Firma bez povoleného sdílení dostane důvod a okno se neotevře; jinak okno s vlastními šablonami firmy,
    /// šablona vybraná v seznamu je předem zaškrtnutá.</summary>
    private async Task SendTemplatesAsync()
    {
        try
        {
            var stav = await _service.GetSharingStateAsync();
            if (!stav.Active)
            {
                PopupDisplayer.Info(stav.Reason);
                return;
            }
            var vlastni = await _service.ListAsync();
            if (vlastni.Count == 0)
            {
                PopupDisplayer.Info("Firma nemá žádnou vlastní šablonu. Šablony M-line mají všechny firmy, posílat je není potřeba.");
                return;
            }
            Guid? vybrana = Selected is not null && !IsSelectedSystem && Guid.TryParse(Selected.Id, out var id) ? id : null;
            RequestSendDialog?.Invoke(new SendTemplatesDialogViewModel(_service,
                vlastni.Select(t => (t.TemplateId, t.Name, _catalog.AgendaTitle(t.AgendaCode))), vybrana));
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    public AsyncRelayCommand AcceptShipmentCommand { get; }
    public AsyncRelayCommand RejectShipmentCommand { get; }

    private ReceivedShipments? _shipments;
    private ReceivedShipments Shipments => _shipments ??= new ReceivedShipments(_service, PopupDisplayer);

    private int? _receivedCount;

    /// <summary>Počet nevyřízených zásilek u uzlu Přijaté šablony. Zjišťuje se v ISYCloudu, proto až po zobrazení
    /// okna a bez čekání - nedostupný ISYCloud okno nezdrží, uzel jen zůstane bez počtu.</summary>
    public async Task LoadReceivedCountAsync()
    {
        if (!ShowsSharing) return;
        // Na klonu spojení: uživatel mezitím kliká a seznam se načítá přes tutéž session.
        try { _receivedCount = (await (await Task.Run(_service.Clone)).GetReceivedSummaryAsync()).Shipments; }
        catch (Exception) { _receivedCount = null; }
        TemplateCatalogNodeVM.Find(Nodes, TemplateCatalogNodeVM.ReceivedKey)?.SetCount(_receivedCount);
        if (IsReceivedNode) OnPropertyChanged(nameof(Hint));
    }

    /// <summary>Chyby serveru u jednotlivých zásilek hlásí <see cref="ReceivedShipments"/> souhrnně po vyřízení všech.</summary>
    private async Task AcceptShipmentsAsync()
    {
        if (await Shipments.AcceptAsync(ShipmentsToHandle)) await AfterShipmentHandledAsync();
    }

    private async Task RejectShipmentsAsync()
    {
        if (await Shipments.RejectAsync(ShipmentsToHandle)) await AfterShipmentHandledAsync();
    }

    /// <summary>Vyřízená zásilka zmizí ze seznamu; převzaté šablony přibydou v počtech agend.</summary>
    private async Task AfterShipmentHandledAsync()
    {
        await LoadCatalogAsync();
        RequestReload?.Invoke();
        await LoadReceivedCountAsync();
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
        // Stejný strom se jen obnoví na místě - nový by ve WPF zahodil výběr (okno skočilo na Všechny šablony) i rozbalení.
        var nove = TemplateCatalogNodeVM.Build(_catalog, _receivedCount);
        if (!TemplateCatalogNodeVM.UpdateInPlace(Nodes, nove))
        {
            TemplateCatalogNodeVM.KeepExpansion(Nodes, nove);
            Nodes = nove;
            OnPropertyChanged(nameof(Nodes));
        }
        OnPropertyChanged(nameof(ShowsPrintDefault));

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
        // Akce až po výběru uzlu: Přijaté šablony mají jiné (okno otevřené z pruhu v hlavním okně začíná rovnou tam).
        BuildActions();
        OnPropertyChanged(nameof(ListSelectionMode));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
        OpenCommand           = Command(Open,             () => Selected is not null && !IsSelectedReceived);
        NewTemplateCommand    = Command(NewTemplate,      () => SelectedNode?.NewTemplateAgendaCode is not null);
        DeleteTemplateCommand = CommandAsync(DeleteAsync, () => Selected is not null && !IsSelectedSystem && !IsSelectedReceived);
        EditTemplateCommand   = CommandAsync(EditAsync,   () => Selected is not null && !IsSelectedSystem && !IsSelectedReceived);
        SetDefaultCommand     = CommandAsync(SetDefaultAsync,
            () => Selected is not null && CanSetDefault(Selected["vychozi"], CanPrint(Selected["agendaCode"])));
        SendTemplatesCommand  = CommandAsync(SendTemplatesAsync);
        AcceptShipmentCommand = CommandAsync(AcceptShipmentsAsync, () => ShipmentsToHandle.Count > 0);
        RejectShipmentCommand = CommandAsync(RejectShipmentsAsync, () => ShipmentsToHandle.Count > 0);

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
