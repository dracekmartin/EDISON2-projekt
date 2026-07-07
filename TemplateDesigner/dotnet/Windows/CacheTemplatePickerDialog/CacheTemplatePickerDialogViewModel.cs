using EDControls.EDList;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// ViewModel pro Caché picker dialog. Data drží EDList (Caché-driven přes
/// <c>ALVA.TemplateDesigner.EDList.Sablony</c>), filter řídí <c>EDFilter</c>
/// (deklarativně v XAML přes <c>FilterCombo</c>); VM jen vystavuje seznam agend
/// pro combo, CRUD akce a OK output.
/// </summary>
public class CacheTemplatePickerDialogViewModel : EDViewModel
{
    private readonly CacheTemplateService _service;

    /// <summary>Seznam agend pro <c>FilterCombo</c>. První položka <c>null</c> kód = „Všechny agendy".</summary>
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

    public RelayCommand      OpenCommand   { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }

    /// <summary>Code-behind napojí — VM žádá o reload EDListu (po Delete / Rename). EDFilter sebere current filter.</summary>
    public Action? RequestReload { get; set; }

    /// <summary>Code-behind napojí — VM žádá o nový název (rename dialog). Vrátí název nebo null (zrušeno). VM nezná WPF.</summary>
    public Func<string, string?>? RequestRenameInput { get; set; }

    public CacheTemplatePickerDialogViewModel(CacheTemplateService service, IReadOnlyList<AgendaInfo> agendas)
    {
        _service = service;

        AgendaFilters = new List<AgendaFilterVM> { new(null, "Všechny agendy") };
        foreach (var a in agendas)
            AgendaFilters.Add(new AgendaFilterVM(a.Code, $"{a.Code} — {a.Name}"));

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
