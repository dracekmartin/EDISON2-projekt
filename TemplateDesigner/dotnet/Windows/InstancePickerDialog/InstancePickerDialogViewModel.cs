using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

/// <summary>
/// Multi-výběr dostupných instancí pro aktuální agendu (např. linky pro MHD).
/// Server-side filter — pole „Hledat" odešle dotaz do Caché na Enter / tlačítko.
/// </summary>
public class InstancePickerDialogViewModel : EDViewModel
{
    private readonly CacheAgendaProvider _provider;
    private readonly string _agendaCode;
    private readonly HashSet<string> _initiallySelectedValues;

    public ObservableCollection<InstanceRowVM> Instances { get; } = [];

    /// <summary>Po OK obsahuje finální seznam vybraných instancí (zachovává pořadí výběru).</summary>
    public IReadOnlyList<AgendaInstance> SelectedInstances { get; private set; } = [];

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

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

    public AsyncRelayCommand SearchCommand     { get; }
    public RelayCommand      SelectAllCommand  { get; }
    public RelayCommand      ClearSelectionCommand { get; }

    public InstancePickerDialogViewModel(
        CacheAgendaProvider provider,
        string agendaCode,
        IReadOnlyList<AgendaInstance> initiallySelected)
    {
        _provider   = provider;
        _agendaCode = agendaCode;
        _initiallySelectedValues = initiallySelected.Select(i => i.Value).ToHashSet(StringComparer.Ordinal);

        SearchCommand     = CommandAsync(LoadAsync);
        SelectAllCommand  = Command(() => SetSelectionForVisible(true));
        ClearSelectionCommand = Command(() => SetSelectionForVisible(false));
    }

    public async Task LoadAsync()
    {
        try
        {
            StatusText = "Načítám…";
            var search = _searchText;
            var results = await Task.Run(() => _provider.GetAvailableInstances(_agendaCode, search, 50));

            Instances.Clear();
            foreach (var inst in results)
            {
                var row = new InstanceRowVM(inst, _initiallySelectedValues.Contains(inst.Value));
                row.PropertyChanged += OnRowPropertyChanged;
                Instances.Add(row);
            }
            RecomputeSelectedCount();
            StatusText = Instances.Count == 50
                ? "Zobrazeno prvních 50 — zúžit přes Hledat"
                : Instances.Count == 0
                    ? "Žádné výsledky"
                    : $"Zobrazeno: {Instances.Count}";
        }
        catch (Exception ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstanceRowVM.IsSelected))
            RecomputeSelectedCount();
    }

    private void RecomputeSelectedCount() => SelectedCount = Instances.Count(r => r.IsSelected);

    private void SetSelectionForVisible(bool selected)
    {
        foreach (var row in Instances) row.IsSelected = selected;
    }

    protected override void OnOk()
    {
        SelectedInstances = Instances
            .Where(r => r.IsSelected)
            .Select(r => r.Instance)
            .ToList();
        CloseDialog(true);
    }
}

public class InstanceRowVM : EDViewModel
{
    public AgendaInstance Instance { get; }
    public string Display => Instance.Display;
    public string? Subtitle => Instance.Subtitle;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public InstanceRowVM(AgendaInstance instance, bool isSelected)
    {
        Instance = instance;
        _isSelected = isSelected;
    }
}
