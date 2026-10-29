using EDControls.EDViewModel;

namespace TemplateDesigner;

/// <summary>
/// Rychlý start přes prázdné plátno po spuštění návrháře (zkušební verze, nápad Jan 29. 10.).
/// Nový uživatel místo prázdné stránky vidí, kudy dál: šablony z databáze, hotová šablona M-line
/// pro agendu, nebo prázdná šablona jako dřív.
/// </summary>
/// <remarks>
/// Panel jen nabízí, nic sám neotevírá. Zmizí, jakmile uživatel šablonu vymění nebo začne kreslit
/// (<see cref="TemplateDesignerViewModel"/> volá <see cref="Hide"/>), takže stálého uživatele nezdrží.
/// </remarks>
public class StartPanelViewModel : EDViewModel
{
    private readonly Func<Task> _openFromDatabase;
    private readonly Func<Task> _startFromSystemTemplate;
    private readonly Func<Task> _changeAgenda;

    public StartPanelViewModel(Func<Task> openFromDatabase, Func<Task> startFromSystemTemplate, Func<Task> changeAgenda)
    {
        _openFromDatabase = openFromDatabase;
        _startFromSystemTemplate = startFromSystemTemplate;
        _changeAgenda = changeAgenda;
    }

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    private string _agendaName = "";
    /// <summary>Agenda nové šablony („Linkový JŘ · AD") - nováček má vidět, pro co kreslí.</summary>
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

    private string? _systemTemplateName;
    /// <summary>Název hotové šablony M-line pro agendu; null = agenda žádnou nemá (Obecná).</summary>
    public string? SystemTemplateName
    {
        get => _systemTemplateName;
        private set
        {
            if (Set(ref _systemTemplateName, value)) OnPropertyChanged(nameof(HasSystemTemplate));
        }
    }

    public bool HasSystemTemplate => SystemTemplateName is not null;

    private AsyncRelayCommand? _openCommand;
    public AsyncRelayCommand OpenFromDatabaseCommand =>
        _openCommand ??= CommandAsync(_openFromDatabase, () => CanOpenFromDatabase);

    private AsyncRelayCommand? _systemCommand;
    public AsyncRelayCommand StartFromSystemTemplateCommand =>
        _systemCommand ??= CommandAsync(_startFromSystemTemplate, () => HasSystemTemplate);

    private AsyncRelayCommand? _agendaCommand;
    public AsyncRelayCommand ChangeAgendaCommand => _agendaCommand ??= CommandAsync(_changeAgenda);

    private RelayCommand? _blankCommand;
    /// <summary>Prázdná šablona už na plátně je - stačí panel schovat.</summary>
    public RelayCommand StartBlankCommand => _blankCommand ??= Command(Hide);

    internal void Show(string agendaName, bool canOpenFromDatabase, string? systemTemplateName)
    {
        AgendaName = agendaName;
        CanOpenFromDatabase = canOpenFromDatabase;
        SystemTemplateName = systemTemplateName;
        IsVisible = true;
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    public void Hide() => IsVisible = false;
}
