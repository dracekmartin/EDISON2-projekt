using System.Globalization;
using EDControls.EDViewModel;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>Šablona v úvodní obrazovce: název a vedle něj agenda a kdy byla upravena.
/// <c>IsMline</c> = výchozí šablona M-line pro agendu, otevře se jako kopie.</summary>
public sealed record StartPanelItem(Guid TemplateId, string Name, string Detail, bool IsMline = false);

/// <summary>
/// Úvodní obrazovka přes prázdné plátno po spuštění bez linky (zkušební verze, Jan 29.-30. 10.).
/// Zkušený uživatel pokračuje poslední šablonou nebo otevře šablonu firmy, nový najde v seznamu
/// výchozí šablonu M-line (Jan 30. 10.: každý má mít jednu jasnou cestu). Jen nabízí, nic sama neotevře.
/// </summary>
/// <remarks>
/// Zmizí, jakmile uživatel šablonu vymění nebo začne kreslit (<see cref="TemplateDesignerViewModel"/>
/// volá <see cref="Hide"/>), takže nikoho nezdrží a „Příště nezobrazovat" nepotřebuje.
/// </remarks>
public class StartPanelViewModel : EDViewModel
{
    /// <summary>Kolik posledních šablon a kolik šablon firmy obrazovka ukáže; zbytek je v okně Otevřít.</summary>
    internal const int MaxRecent = 3, MaxFirm = 5;

    private readonly Func<Guid, Task> _openTemplate;
    private readonly Func<Task> _openFromDatabase;
    private readonly Func<Task> _startFromSystemTemplate;
    private readonly Func<Task> _changeAgenda;
    private readonly Func<Task> _startBlank;

    public StartPanelViewModel(Func<Guid, Task> openTemplate, Func<Task> openFromDatabase,
        Func<Task> startFromSystemTemplate, Func<Task> changeAgenda, Func<Task> startBlank)
    {
        _openTemplate = openTemplate;
        _openFromDatabase = openFromDatabase;
        _startFromSystemTemplate = startFromSystemTemplate;
        _changeAgenda = changeAgenda;
        _startBlank = startBlank;
    }

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    private string _agendaName = "";
    /// <summary>Agenda nové šablony („Linkový JŘ · AD") - uživatel má vidět, pro co kreslí.</summary>
    public string AgendaName
    {
        get => _agendaName;
        private set => Set(ref _agendaName, value);
    }

    private bool _canOpenFromDatabase;
    /// <summary>Bez spojení na server databáze šablon není.</summary>
    public bool CanOpenFromDatabase
    {
        get => _canOpenFromDatabase;
        private set => Set(ref _canOpenFromDatabase, value);
    }

    private IReadOnlyList<StartPanelItem> _recent = [];
    /// <summary>Šablony, se kterými uživatel naposledy pracoval, v kterékoli agendě - hlavní cesta
    /// zkušeného uživatele.</summary>
    public IReadOnlyList<StartPanelItem> Recent
    {
        get => _recent;
        private set { if (Set(ref _recent, value)) OnPropertyChanged(nameof(HasRecent)); }
    }
    public bool HasRecent => Recent.Count > 0;

    private IReadOnlyList<StartPanelItem> _agendaTemplates = [];
    /// <summary>Šablony pro agendu nové šablony: šablony firmy (kromě těch v <see cref="Recent"/>)
    /// a na konci výchozí šablona M-line. Výchozí šablonu uživatel otevře a upraví stejně jako
    /// šablonu firmy, uloží se mu jako jeho - proto není „nová šablona", ale jedna ze šablon agendy.</summary>
    public IReadOnlyList<StartPanelItem> AgendaTemplates
    {
        get => _agendaTemplates;
        private set { if (Set(ref _agendaTemplates, value)) OnPropertyChanged(nameof(HasNoAgendaTemplates)); }
    }

    /// <summary>Obecná agenda bez šablon firmy nemá co nabídnout - obrazovka to řekne jedním řádkem.</summary>
    public bool HasNoAgendaTemplates => AgendaTemplates.Count == 0;

    private AsyncRelayCommand<StartPanelItem>? _openTemplateCommand;
    public AsyncRelayCommand<StartPanelItem> OpenTemplateCommand =>
        _openTemplateCommand ??= CommandAsync<StartPanelItem>(item =>
            item is null ? Task.CompletedTask
            : item.IsMline ? _startFromSystemTemplate()
            : _openTemplate(item.TemplateId));

    private AsyncRelayCommand? _openCommand;
    public AsyncRelayCommand OpenFromDatabaseCommand =>
        _openCommand ??= CommandAsync(_openFromDatabase, () => CanOpenFromDatabase);

    private AsyncRelayCommand? _agendaCommand;
    public AsyncRelayCommand ChangeAgendaCommand => _agendaCommand ??= CommandAsync(_changeAgenda);

    private AsyncRelayCommand? _blankCommand;
    /// <summary>Po spuštění prázdná šablona na plátně už je a obrazovka se jen schová; otevřená
    /// z menu nad rozdělanou šablonou založí novou (jako Ctrl+N).</summary>
    public AsyncRelayCommand StartBlankCommand => _blankCommand ??= CommandAsync(_startBlank);

    private RelayCommand? _closeCommand;
    public RelayCommand CloseCommand => _closeCommand ??= Command(Hide);

    /// <param name="systemTemplateName">Výchozí šablona M-line pro agendu; null = agenda žádnou nemá (Obecná).</param>
    internal void Show(string agendaName, bool canOpenFromDatabase, string? systemTemplateName,
        IReadOnlyList<StartPanelItem> recent, IReadOnlyList<StartPanelItem> firmTemplates)
    {
        AgendaName = agendaName;
        CanOpenFromDatabase = canOpenFromDatabase;
        Recent = recent;
        // Výchozí šablonu odliší jen ikona M-line; „Doporučeno" ani rady se nepřidávají, kterou šablonu
        // vzít, si uživatel rozhodne sám (Jan 30. 10.).
        AgendaTemplates = systemTemplateName is null
            ? firmTemplates
            : [.. firmTemplates, new StartPanelItem(Guid.Empty, systemTemplateName, "výchozí šablona M-line", IsMline: true)];
        IsVisible = true;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    public void Hide() => IsVisible = false;

    /// <summary>
    /// Sestaví seznamy obrazovky ze seznamu šablon firmy: poslední šablony v pořadí, v jakém s nimi
    /// uživatel pracoval (jen ty, které v databázi pořád jsou), a pod nimi šablony agendy od nejnověji
    /// upravené.
    /// </summary>
    /// <param name="agendaName">Kód agendy → název pro uživatele („Zastávkový JŘ · AD").</param>
    internal static (IReadOnlyList<StartPanelItem> Recent, IReadOnlyList<StartPanelItem> Firm) BuildLists(
        IReadOnlyList<CacheTemplateMetadata> templates, IReadOnlyList<Guid> recentIds, string? agendaCode,
        Func<string, string> agendaName, DateTime today)
    {
        var podleId = templates.GroupBy(t => t.TemplateId).ToDictionary(g => g.Key, g => g.First());
        var recent = recentIds
            .Where(podleId.ContainsKey)
            .Take(MaxRecent)
            .Select(id => podleId[id])
            .Select(t => Item(t, $"{agendaName(t.AgendaCode)} · {Upraveno(t, today)}"))
            .ToList();

        var agenda = string.IsNullOrEmpty(agendaCode) ? CacheTemplateService.GeneralAgendaCode : agendaCode;
        var vRecent = recent.Select(r => r.TemplateId).ToHashSet();
        var firm = templates
            .Where(t => string.Equals(t.AgendaCode, agenda, StringComparison.OrdinalIgnoreCase) && !vRecent.Contains(t.TemplateId))
            .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt ?? DateTime.MinValue)
            .Take(MaxFirm)
            .Select(t => Item(t, Upraveno(t, today)))
            .ToList();
        return (recent, firm);
    }

    private static StartPanelItem Item(CacheTemplateMetadata t, string detail) =>
        new(t.TemplateId, string.IsNullOrWhiteSpace(t.Name) ? "(bez názvu)" : t.Name, detail);

    private static string Upraveno(CacheTemplateMetadata t, DateTime today)
    {
        var kdy = t.ModifiedAt ?? t.CreatedAt;
        if (kdy is null) return "";
        var den = kdy.Value.ToLocalTime().Date;
        var text = den == today.Date ? "dnes"
            : den == today.Date.AddDays(-1) ? "včera"
            : den.ToString(den.Year == today.Year ? "d. M." : "d. M. yyyy", CultureInfo.GetCultureInfo("cs-CZ"));
        // Kdo šablonu upravil, se neukazuje: v metadatech je číslo uživatele, ne jméno (Jan 30. 10.).
        return $"upraveno {text}";
    }
}
