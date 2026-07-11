using EDControls.EDViewModel;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

/// <summary>
/// Dialog pro přepnutí konfigurace agenda × varianta. Přepnutí vytváří novou šablonu se
/// zvolenou konfigurací — ta určuje DB schéma + typy elementů + reálná data,
/// proto nelze zachovat rozdělaný obsah napříč agendami.
///
/// Dvouosý model: řádky se seskupují podle agendy (Zastávkový JŘ, Linkový JŘ, Faktury…),
/// uvnitř skupiny jsou datové varianty (AD / MHD); agenda bez variant má jeden řádek.
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

    /// <summary>Po OK obsahuje kombinovaný kód vybrané konfigurace. Null = dialog zrušen nebo nic nevybráno.</summary>
    public string? ChosenCode { get; private set; }

    public ChangeAgendaDialogViewModel(IReadOnlyList<AgendaRegistryEntry> available, string? currentCode)
    {
        Agendas = available
            .Select(a => new AgendaRowVM(
                a.Code,
                a.Variant is { Length: > 0 } ? (a.VariantLabel is { Length: > 0 } ? a.VariantLabel : a.Variant)
                                             : a.EffectiveAgendaLabel,
                a.EffectiveAgendaLabel,
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

/// <summary>Jeden řádek výběru: Name = varianta (nebo agenda, když varianty nemá), Group = agenda.</summary>
public record AgendaRowVM(string Code, string Name, string Group, bool IsCurrent);
