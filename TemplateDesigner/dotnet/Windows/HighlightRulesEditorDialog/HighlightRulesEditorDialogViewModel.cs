using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner;

/// <summary>
/// Řádek editoru pravidel zvýraznění - podmínka + barva pozadí. WPF-free (R4); barvu na swatch
/// překládá converter v XAML.
/// </summary>
public class HighlightRuleVM : INotifyPropertyChanged
{
    private string _condition = "";
    private string? _backgroundColor;

    public string Condition
    {
        get => _condition;
        set { _condition = value; OnChanged(); }
    }

    public string? BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; OnChanged(); }
    }

    public static HighlightRuleVM FromModel(HighlightRule r) => new()
    {
        _condition = r.Condition ?? "",
        _backgroundColor = r.BackgroundColor,
    };

    public HighlightRule ToModel() => new()
    {
        Condition = string.IsNullOrWhiteSpace(Condition) ? null : Condition.Trim(),
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColor) ? null : BackgroundColor,
        // TargetColumn zatím needitujeme (per-sloupec render až #2) - u nových pravidel null
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// Editor seznamu pravidel podmíněného zvýraznění řádku (varianta 3 - custom dialog jako editor
/// sloupců). Sdílený DynamicTable/StopList/LineTable. Jen podmínka + barva pozadí; pořadí = priorita
/// (první splněné vyhrává). Barva se edituje na vybraném pravidle (jako color pickery v editoru sloupců).
/// </summary>
public class HighlightRulesEditorDialogViewModel : EDViewModel
{
    public ObservableCollection<HighlightRuleVM> Rules { get; } = [];

    private HighlightRuleVM? _selectedRule;
    public HighlightRuleVM? SelectedRule
    {
        get => _selectedRule;
        set => Set(ref _selectedRule, value);
    }

    public RelayCommand AddRuleCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand PickColorCommand { get; }
    public RelayCommand ClearColorCommand { get; }

    /// <summary>View wire: hex → vybraná barva (null = storno). Nastavuje code-behind.</summary>
    public Func<string, string?>? RequestColorPicker { get; set; }

    /// <summary>Výsledná pravidla po OK (prázdná pravidla bez podmínky vyřazena).</summary>
    public List<HighlightRule> ResultRules { get; private set; } = [];

    public HighlightRulesEditorDialogViewModel(IReadOnlyList<HighlightRule>? rules)
    {
        if (rules is not null)
            foreach (var r in rules)
                Rules.Add(HighlightRuleVM.FromModel(r));

        if (Rules.Count > 0) SelectedRule = Rules[0];

        AddRuleCommand = Command(() =>
        {
            var vm = new HighlightRuleVM { BackgroundColor = "#FFFFF2CC" };   // světle žlutá jako výchozí
            Rules.Add(vm);
            SelectedRule = vm;
        });

        RemoveCommand = Command(() =>
        {
            if (SelectedRule is null) return;
            int idx = Rules.IndexOf(SelectedRule);
            Rules.Remove(SelectedRule);
            SelectedRule = Rules.Count > 0 ? Rules[Math.Min(idx, Rules.Count - 1)] : null;
        });

        MoveUpCommand   = Command(() => Move(-1));
        MoveDownCommand = Command(() => Move(+1));

        PickColorCommand = Command(() =>
        {
            if (SelectedRule is null || RequestColorPicker is null) return;
            var result = RequestColorPicker(SelectedRule.BackgroundColor ?? "");
            if (result is not null) SelectedRule.BackgroundColor = result;
        });

        ClearColorCommand = Command(() =>
        {
            if (SelectedRule is not null) SelectedRule.BackgroundColor = null;
        });
    }

    private void Move(int delta)
    {
        if (SelectedRule is null) return;
        int idx = Rules.IndexOf(SelectedRule);
        int target = idx + delta;
        if (idx >= 0 && target >= 0 && target < Rules.Count)
            Rules.Move(idx, target);
    }

    protected override void OnOk()
    {
        // Pravidlo bez podmínky nemá smysl - vyřaď (prázdné řádky z editoru)
        ResultRules = Rules.Select(r => r.ToModel())
                           .Where(r => !string.IsNullOrWhiteSpace(r.Condition))
                           .ToList();
        CloseDialog(true);
    }
}
