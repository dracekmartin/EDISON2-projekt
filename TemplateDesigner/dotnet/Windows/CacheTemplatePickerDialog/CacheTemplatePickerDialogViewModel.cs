using EDControls.EDList;
using EDControls.EDViewModel;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro Caché picker dialog. Data drží EDList (Caché-driven přes
/// <c>ALVA.TemplateDesigner.EDList.Sablony</c>), filter řídí <c>EDFilter</c>
/// (deklarativně v XAML přes <c>EDFilterCombo</c>); VM jen vystavuje seznam agend
/// pro combo, CRUD akce a OK output.
/// </summary>
public class CacheTemplatePickerDialogViewModel : EDViewModel
{
    private readonly CacheTemplateService _service;

    /// <summary>Seznam agend pro <c>EDFilterCombo</c>. První položka <c>null</c> kód = „Všechny agendy".</summary>
    public List<AgendaFilterVM> AgendaFilters { get; }

    private EDListRow? _selected;
    /// <summary>Code-behind plní z <c>EDList.SelectionChanged</c>.</summary>
    public EDListRow? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Vybraná šablona po stisku Otevřít. Null = dialog zrušen.</summary>
    public Guid? SelectedTemplateId { get; private set; }

    /// <summary>Po Otevřít: byla vybraná šablona systémová (M-line)? Řídí načtení z MLINE větve
    /// + otevření jako „kopie" (Ctrl+S → Uložit jako do firmy).</summary>
    public bool SelectedIsSystem { get; private set; }

    /// <summary>Aktuálně vybraný řádek je systémová (M-line) šablona → read-only (nejde mazat/přejmenovat).</summary>
    private bool IsSelectedSystem => string.Equals(Selected?["zdroj"], "M-line", StringComparison.Ordinal);

    public RelayCommand      OpenCommand   { get; }
    public AsyncRelayCommand DeleteTemplateCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }

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

        RequestReload?.Invoke();
        PopupDisplayer.Info(ulozeno > 0
            ? $"Staženo šablon: {ulozeno}. Najdete je v přehledu mezi systémovými (M-line)."
            : "Šablony se nepodařilo stáhnout - zkuste to prosím později.");
    }

    /// <summary>Code-behind napojí - VM žádá o reload EDListu (po Delete / Rename). EDFilter sebere current filter.</summary>
    public Action? RequestReload { get; set; }

    /// <summary>Code-behind napojí - VM žádá o nový název (rename dialog). Vrátí název nebo null (zrušeno). VM nezná WPF.</summary>
    public Func<string, string?>? RequestRenameInput { get; set; }

    public CacheTemplatePickerDialogViewModel(CacheTemplateService service, IReadOnlyList<AgendaRegistryEntry> agendas)
    {
        _service = service;
        _channel = new SystemTemplateChannelSync(service);

        // Dvouosé popisky seřazené po agendách (Zastávkový JŘ · AD / · MHD vedle sebe).
        // Jen zveřejněné agendy (Released) - pilotní se ve filtru nenabízí; jejich šablony
        // zůstávají dosažitelné přes „Všechny agendy".
        AgendaFilters = new List<AgendaFilterVM> { new(null, "Všechny agendy") };
        foreach (var a in agendas.Where(x => x.Released).OrderBy(x => x.EffectiveAgendaLabel).ThenBy(x => x.Variant))
            AgendaFilters.Add(new AgendaFilterVM(a.Code, a.DisplayName));

        // Systémové (M-line) jsou read-only - otevřít lze, mazat/přejmenovat ne.
        OpenCommand           = Command(Open,             () => Selected is not null);
        DeleteTemplateCommand = CommandAsync(DeleteAsync, () => Selected is not null && !IsSelectedSystem);
        RenameCommand         = CommandAsync(RenameAsync, () => Selected is not null && !IsSelectedSystem);
    }

    private void Open()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        SelectedTemplateId = id;
        SelectedIsSystem = IsSelectedSystem;
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
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }

    private async Task RenameAsync()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        var oldName = Selected["name"] ?? "";

        var newName = RequestRenameInput?.Invoke(oldName);
        if (newName is null || newName == oldName) return;
        try
        {
            await _service.RenameAsync(id, newName);
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }
}

/// <summary>Filter item pro agenda dropdown.</summary>
public record AgendaFilterVM(string? Code, string Label);
