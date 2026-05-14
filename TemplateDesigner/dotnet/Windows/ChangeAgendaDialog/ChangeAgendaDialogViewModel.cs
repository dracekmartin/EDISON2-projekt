using EDControls.EDViewModel;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

/// <summary>
/// Dialog pro přepnutí kontextu agendy. Přepnutí vytváří novou šablonu se
/// zvolenou agendou — agenda určuje DB schéma + typy elementů + reálná data,
/// proto nelze zachovat rozdělaný obsah napříč agendami.
/// </summary>
public class ChangeAgendaDialogViewModel : EDViewModel
{
    public List<AgendaRowVM> Agendas { get; }

    private AgendaRowVM? _selected;
    public AgendaRowVM? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    /// <summary>Po OK obsahuje kód vybrané agendy. Null = dialog zrušen nebo nic nevybráno.</summary>
    public string? ChosenCode { get; private set; }

    public ChangeAgendaDialogViewModel(IReadOnlyList<AgendaInfo> available, string? currentCode)
    {
        Agendas = available
            .Select(a => new AgendaRowVM(
                a.Code,
                a.Name,
                IsCurrent: a.Code.Equals(currentCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        _selected = Agendas.FirstOrDefault(a => a.IsCurrent) ?? Agendas.FirstOrDefault();
    }

    protected override void OnOk()
    {
        if (_selected is null)
        {
            PopupDisplayer.Info("Vyber agendu.");
            return;
        }
        ChosenCode = _selected.Code;
        CloseDialog(true);
    }
}

public record AgendaRowVM(string Code, string Name, bool IsCurrent);
