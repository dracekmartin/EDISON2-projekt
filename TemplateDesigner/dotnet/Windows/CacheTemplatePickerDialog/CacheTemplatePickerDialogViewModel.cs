using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro Caché picker dialog — list uložených šablon s akcemi
/// Otevřít / Smazat / Přejmenovat.
/// Před zobrazením volá <see cref="LoadAsync"/>.
/// </summary>
public class CacheTemplatePickerDialogViewModel : EDViewModel
{
    private readonly CacheTemplateService _service;
    private readonly IReadOnlyList<AgendaInfo> _agendas;

    public ObservableCollection<CacheTemplateRowVM> Templates { get; } = [];

    private CacheTemplateRowVM? _selected;
    public CacheTemplateRowVM? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public List<AgendaFilterVM> AgendaFilters { get; }

    private AgendaFilterVM _selectedFilter;
    public AgendaFilterVM SelectedFilter
    {
        get => _selectedFilter;
        set { if (Set(ref _selectedFilter, value)) RefreshView(); }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) RefreshView(); }
    }

    public ICollectionView TemplatesView { get; }

    /// <summary>Vybraná šablona po stisku Otevřít. Null = dialog zrušen.</summary>
    public Guid? SelectedTemplateId { get; private set; }

    public AsyncRelayCommand OpenCommand   { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public CacheTemplatePickerDialogViewModel(CacheTemplateService service, IReadOnlyList<AgendaInfo> agendas)
    {
        _service = service;
        _agendas = agendas;

        AgendaFilters = new List<AgendaFilterVM> { new(null, "Všechny agendy") };
        foreach (var a in agendas)
            AgendaFilters.Add(new AgendaFilterVM(a.Code, $"{a.Code} — {a.Name}"));
        _selectedFilter = AgendaFilters[0];

        TemplatesView = CollectionViewSource.GetDefaultView(Templates);
        TemplatesView.Filter = FilterRow;
        TemplatesView.SortDescriptions.Add(new SortDescription(nameof(CacheTemplateRowVM.ModifiedAt), ListSortDirection.Descending));

        OpenCommand    = CommandAsync(OpenAsync,   () => Selected is not null);
        DeleteCommand  = CommandAsync(DeleteAsync, () => Selected is not null);
        RenameCommand  = CommandAsync(RenameAsync, () => Selected is not null);
        RefreshCommand = CommandAsync(LoadAsync);
    }

    private bool FilterRow(object obj)
    {
        if (obj is not CacheTemplateRowVM row) return false;

        // Agenda filter (null = všechny agendy)
        if (_selectedFilter.Code is { } code &&
            !row.AgendaCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            return false;

        // Search filter
        if (!string.IsNullOrEmpty(_searchText) &&
            row.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }

    private void RefreshView() => TemplatesView.Refresh();

    /// <summary>Načte všechny šablony z Caché (server-side bez filteru).
    /// Filtrace per agenda + search je client-side přes <see cref="FilterRow"/> → instant.</summary>
    public async Task LoadAsync()
    {
        try
        {
            var metas = await _service.ListAsync(null);
            Templates.Clear();
            foreach (var m in metas)
                Templates.Add(new CacheTemplateRowVM(m));
            RefreshView();
        }
        catch (CacheTemplateException ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    private Task OpenAsync()
    {
        if (Selected is null) return Task.CompletedTask;
        SelectedTemplateId = Selected.TemplateId;
        CloseDialog(true);
        return Task.CompletedTask;
    }

    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        if (!PopupDisplayer.Confirm($"Opravdu smazat šablonu „{Selected.Name}\"?")) return;
        try
        {
            await _service.DeleteAsync(Selected.TemplateId);
            Templates.Remove(Selected);
        }
        catch (CacheTemplateException ex)
        {
            PopupDisplayer.Error(ex);
        }
    }

    private async Task RenameAsync()
    {
        if (Selected is null) return;
        var dlg = new SaveSectionTemplateDialogViewModel(Selected.Name);
        var win = new SaveSectionTemplateDialog(dlg);
        if (win.ShowDialog() != true) return;

        var newName = dlg.Name;
        if (newName == Selected.Name) return;

        try
        {
            await _service.RenameAsync(Selected.TemplateId, newName);
            Selected.Name = newName;
            RefreshView();
        }
        catch (CacheTemplateException ex)
        {
            PopupDisplayer.Error(ex);
        }
    }
}

/// <summary>Filter item pro agenda dropdown.</summary>
public record AgendaFilterVM(string? Code, string Label);

/// <summary>Jeden řádek v listu šablon.</summary>
public class CacheTemplateRowVM : EDViewModel
{
    public Guid TemplateId { get; }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string AgendaCode { get; }
    public string Author     { get; }
    public DateTime? ModifiedAt { get; }
    public string ModifiedBy { get; }

    public string ModifiedAtText => ModifiedAt?.ToString("d.M.yyyy HH:mm") ?? "—";

    public CacheTemplateRowVM(CacheTemplateMetadata m)
    {
        TemplateId = m.TemplateId;
        _name      = m.Name;
        AgendaCode = m.AgendaCode;
        Author     = m.Author     ?? "";
        ModifiedAt = m.ModifiedAt;
        ModifiedBy = m.ModifiedBy ?? "";
    }
}
