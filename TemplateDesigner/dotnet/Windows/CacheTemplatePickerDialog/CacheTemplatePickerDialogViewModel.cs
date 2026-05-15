using EDControls.EDList;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro Caché picker dialog. Data drží EDList (Caché-driven přes
/// <c>ALVA.TemplateDesigner.API.TemplatesList</c>); VM jen řídí agenda filter,
/// CRUD akce a OK output.
/// </summary>
public class CacheTemplatePickerDialogViewModel : EDViewModel
{
    private readonly CacheTemplateService _service;

    public List<AgendaFilterVM> AgendaFilters { get; }

    private AgendaFilterVM _selectedFilter;
    public AgendaFilterVM SelectedFilter
    {
        get => _selectedFilter;
        set => Set(ref _selectedFilter, value);  // change → code-behind volá reload
    }

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

    public RelayCommand      OpenCommand   { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }

    /// <summary>Code-behind napojí — VM žádá o reload EDListu (např. po Delete / Rename / agenda změně).</summary>
    public Action? RequestReload { get; set; }

    public CacheTemplatePickerDialogViewModel(CacheTemplateService service, IReadOnlyList<AgendaInfo> agendas)
    {
        _service = service;

        AgendaFilters = new List<AgendaFilterVM> { new(null, "Všechny agendy") };
        foreach (var a in agendas)
            AgendaFilters.Add(new AgendaFilterVM(a.Code, $"{a.Code} — {a.Name}"));
        _selectedFilter = AgendaFilters[0];

        OpenCommand    = Command(Open,                () => Selected is not null);
        DeleteCommand  = CommandAsync(DeleteAsync,    () => Selected is not null);
        RenameCommand  = CommandAsync(RenameAsync,    () => Selected is not null);
    }

    private void Open()
    {
        if (Selected is null) return;
        if (!Guid.TryParse(Selected.Id, out var id)) return;
        SelectedTemplateId = id;
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

        var dlg = new SaveSectionTemplateDialogViewModel(oldName);
        var win = new SaveSectionTemplateDialog(dlg);
        if (win.ShowDialog() != true) return;

        if (dlg.Name == oldName) return;
        try
        {
            await _service.RenameAsync(id, dlg.Name);
            RequestReload?.Invoke();
        }
        catch (CacheTemplateException ex) { PopupDisplayer.Error(ex); }
    }
}

/// <summary>Filter item pro agenda dropdown.</summary>
public record AgendaFilterVM(string? Code, string Label);
