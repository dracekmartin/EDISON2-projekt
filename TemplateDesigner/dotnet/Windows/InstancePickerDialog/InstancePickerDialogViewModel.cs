using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

/// <summary>
/// Multi-výběr dostupných instancí pro aktuální agendu (např. linky pro MHD).
/// Data + UI řeší <c>EDList</c> přes Caché endpoint <c>ALVA.TemplateDesigner.EDList.Instance</c>.
/// VM jen drží kontext (agendaCode), počítá výběr a sestaví výstup po OK.
/// </summary>
public class InstancePickerDialogViewModel : EDViewModel
{
    private readonly string _agendaCode;
    private readonly HashSet<string> _initiallySelectedValues;

    /// <summary>Po OK obsahuje finální seznam vybraných instancí.</summary>
    public IReadOnlyList<AgendaInstance> SelectedInstances { get; private set; } = [];

    /// <summary>Skupiny linek pro filtr (searchable combobox). Prázdné = agenda nemá skupiny → filtr skrytý.</summary>
    public IReadOnlyList<AgendaSkupina> Skupiny { get; }

    public bool HasSkupiny => Skupiny.Count > 0;

    /// <summary>Aktuálně zvolená skupina (SKUPINA_LINEK_ID). Nastavuje code-behind při výběru v comboboxu;
    /// po OK ji čte hostitelský VM a propaguje do GetData. Null = default (server první nezablokovaná).</summary>
    public string? SelectedSkupinaId { get; set; }

    /// <summary>Popisek zvolené skupiny (pro status v hlavním okně). Nastavuje code-behind.</summary>
    public string? SelectedSkupinaLabel { get; set; }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        private set => Set(ref _selectedCount, value);
    }

    /// <summary>Code-behind napojí — VM žádá výstupní mapu vybraných řádků na AgendaInstance.</summary>
    public Func<IReadOnlyList<AgendaInstance>>? RequestSelectedInstances { get; set; }

    public InstancePickerDialogViewModel(
        string agendaCode,
        IReadOnlyList<AgendaInstance> initiallySelected,
        IReadOnlyList<AgendaSkupina>? skupiny = null,
        string? initialSkupinaId = null)
    {
        _agendaCode = agendaCode;
        _initiallySelectedValues = initiallySelected.Select(i => i.Value).ToHashSet(StringComparer.Ordinal);
        Skupiny = skupiny ?? [];
        SelectedSkupinaId = initialSkupinaId;
    }

    public string AgendaCode => _agendaCode;
    public IReadOnlySet<string> InitiallySelectedValues => _initiallySelectedValues;

    /// <summary>Code-behind volá po každé změně checkboxů v EDListu.</summary>
    public void OnCheckedRowsChanged(int checkedCount, int totalCount)
    {
        SelectedCount = checkedCount;
        StatusText = totalCount == 0
            ? "Žádné instance"
            : $"Zobrazeno: {totalCount}";
    }

    protected override void OnOk()
    {
        SelectedInstances = RequestSelectedInstances?.Invoke() ?? [];
        CloseDialog(true);
    }
}
