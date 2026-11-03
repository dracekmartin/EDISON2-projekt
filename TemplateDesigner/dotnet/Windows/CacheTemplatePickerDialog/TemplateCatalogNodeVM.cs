using System.ComponentModel;
using System.Runtime.CompilerServices;
using TemplateDesigner.Services;

namespace TemplateDesigner;

/// <summary>
/// Uzel stromu v okně šablon: Všechny šablony, oblast práce, agenda, varianta (`B190`). Uzel jen zužuje seznam
/// vpravo na své kódy agend; agenda s jedinou variantou je jeden uzel, úroveň varianty by nic nerozlišovala.
/// </summary>
public sealed class TemplateCatalogNodeVM : INotifyPropertyChanged
{
    /// <summary>Klíč uzlu pro zapamatování posledního výběru: "" = Všechny šablony, jinak „oblast:", „agenda:"
    /// nebo „varianta:" a kód. Uzel agendy s jedinou variantou má klíč agendy - přežije přidání další varianty.</summary>
    public string Key { get; }

    public string Label { get; }

    /// <summary>Úroveň uzlu; strom podle ní volí vzhled (oblast jako nadpis, agenda tučně).</summary>
    public TemplateCatalogNodeKind Kind { get; init; }

    /// <summary>Výchozí šablona pro tisk uzlu jedné agendy (firmy, jinak M-line); null u ostatních uzlů a bez šablony.</summary>
    public string? DefaultName { get; init; }

    /// <summary>Počet šablon pod uzlem; u Všech šablon <c>null</c> (patří k nim i šablony agend mimo strom).</summary>
    public int? Count { get; }

    /// <summary>Šablona, kterou tisk vezme („Pro tisk: …"), jen u uzlu jedné agendy šablon, která se tiskne z modulu.</summary>
    public string? Detail { get; }

    /// <summary>Kódy agend šablon pod uzlem; prázdné = všechny šablony bez filtru.</summary>
    public IReadOnlyList<string> AgendaCodes { get; }

    public IReadOnlyList<TemplateCatalogNodeVM> Children { get; }

    public TemplateCatalogNodeVM(string key, string label, int? count, string? detail,
        IReadOnlyList<string> agendaCodes, IReadOnlyList<TemplateCatalogNodeVM>? children = null)
    {
        Key = key;
        Label = label;
        Count = count;
        Detail = detail;
        AgendaCodes = agendaCodes;
        Children = children ?? [];
    }

    public bool HasDetail => Detail is not null;

    /// <summary>Hodnota filtru seznamu šablon (<c>agendaCodes</c> v <c>EDList.Sablony</c>).</summary>
    public string FilterValue => string.Join(",", AgendaCodes);

    /// <summary>Agenda, ve které tlačítko Nová šablona založí prázdnou šablonu; null, když uzel neurčuje jednu
    /// agendu (Všechny šablony, oblast nebo agenda s víc variantami).</summary>
    public string? NewTemplateAgendaCode => AgendaCodes.Count == 1 ? AgendaCodes[0] : null;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public const string AllKey = "";

    /// <summary>Uzly stromu: první Všechny šablony, pak oblasti práce v pořadí serveru. Oblasti jsou rozbalené.</summary>
    public static IReadOnlyList<TemplateCatalogNodeVM> Build(TemplateCatalog catalog)
    {
        var nodes = new List<TemplateCatalogNodeVM> { new(AllKey, "Všechny šablony", null, null, []) };
        foreach (var area in catalog.Areas)
        {
            var agendas = area.Agendas.Select(AgendaNode).ToList();
            nodes.Add(new TemplateCatalogNodeVM($"oblast:{area.Code}", area.Label, agendas.Sum(a => a.Count ?? 0), null,
                [.. agendas.SelectMany(a => a.AgendaCodes)], agendas) { Kind = TemplateCatalogNodeKind.Area, IsExpanded = true });
        }
        return nodes;
    }

    private static TemplateCatalogNodeVM AgendaNode(CatalogAgenda agenda)
    {
        if (agenda.Variants.Count == 1)
        {
            var only = agenda.Variants[0];
            return new TemplateCatalogNodeVM($"agenda:{agenda.Code}", agenda.Label, only.Count, DetailFor(only), [only.Code])
                { Kind = TemplateCatalogNodeKind.Agenda, DefaultName = only.Default?.Name };
        }
        var variants = agenda.Variants
            .Select(v => new TemplateCatalogNodeVM($"varianta:{v.Code}", v.Label, v.Count, DetailFor(v), [v.Code])
                { Kind = TemplateCatalogNodeKind.Variant, DefaultName = v.Default?.Name })
            .ToList();
        return new TemplateCatalogNodeVM($"agenda:{agenda.Code}", agenda.Label, variants.Sum(v => v.Count ?? 0), null,
            [.. variants.SelectMany(v => v.AgendaCodes)], variants) { Kind = TemplateCatalogNodeKind.Agenda };
    }

    private static string? DetailFor(CatalogVariant variant) =>
        !variant.CanPrint ? null
        : variant.Default is { } d ? $"Pro tisk: {d.Name}"
        : "Bez šablony pro tisk";

    /// <summary>Uzel podle klíče do hloubky; null když ve stromu není.</summary>
    public static TemplateCatalogNodeVM? Find(IEnumerable<TemplateCatalogNodeVM> nodes, string? key)
    {
        foreach (var node in nodes)
        {
            if (node.Key == key) return node;
            if (Find(node.Children, key) is { } found) return found;
        }
        return null;
    }

    /// <summary>Nejhlubší uzel, který ukazuje jen šablony dané agendy (varianta, nebo agenda s jedinou variantou);
    /// null, když agenda ve stromu není (nezveřejněná).</summary>
    public static TemplateCatalogNodeVM? FindAgenda(IEnumerable<TemplateCatalogNodeVM> nodes, string? agendaCode)
    {
        if (string.IsNullOrEmpty(agendaCode)) return null;
        foreach (var node in nodes)
        {
            if (FindAgenda(node.Children, agendaCode) is { } deeper) return deeper;
            if (string.Equals(node.NewTemplateAgendaCode, agendaCode, StringComparison.OrdinalIgnoreCase)) return node;
        }
        return null;
    }

    /// <summary>Cesta od kořene k uzlu (včetně), prázdná když ve stromu není.</summary>
    public static IReadOnlyList<TemplateCatalogNodeVM> PathTo(IEnumerable<TemplateCatalogNodeVM> nodes, TemplateCatalogNodeVM target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target)) return [node];
            if (PathTo(node.Children, target) is { Count: > 0 } path) return [node, .. path];
        }
        return [];
    }
}

/// <summary>Úroveň uzlu ve stromu šablon.</summary>
public enum TemplateCatalogNodeKind { All, Area, Agenda, Variant }
