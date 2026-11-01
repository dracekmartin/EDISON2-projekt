using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using EDControls;
using EDControls.EDViewModel;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Config;
using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;
using TemplateDesigner.Services;
using ALWA.SYS;

namespace TemplateDesigner;

// ─────────────────────────────────────────────────────────────────────────────
// SectionVM
// ─────────────────────────────────────────────────────────────────────────────

public class SectionVM : INotifyPropertyChanged
{
    private readonly TemplateDesignerViewModel _vm;
    public Section Section { get; }

    /// <summary>Global callback for showing section properties dialog (wired by code-behind).</summary>
    public static Action<Section>? ShowPropertiesGlobal   { get; set; }
    public static Action<Section>? SaveTemplateGlobal     { get; set; }

    public SectionVM(Section section, TemplateDesignerViewModel vm)
    {
        Section = section;
        _vm     = vm;
    }

    public string  Name      => Section.Name;
    /// <summary>„Strana 2" (nebo „Na každé stránce") nad první sekcí oddílu; jinak null.</summary>
    public string? ZahlaviStranky { get; init; }
    public bool    MaZahlaviStranky => ZahlaviStranky is not null;
    /// <summary>Stránka tohoto nadpisu je aktivní - sem jde „Přidat sekci".</summary>
    public bool    ZahlaviAktivni { get; init; }
    /// <summary>Pod poslední sekcí stránky: hlavička a patička, které se na ni tisknou navíc.</summary>
    public string? SpolecneNaStrance { get; init; }
    public bool    MaSpolecneNaStrance => SpolecneNaStrance is not null;
    public bool    IsDynamic => Section.DesignerHeight is null;
    public bool    IsActive  => _vm.State.ActiveSection == Section;
    public bool    HasSubtitle => Subtitle is not null;

    public string? Subtitle
    {
        get
        {
            string? sub = null;
            if (Section.PrintBehavior != SectionPrintBehavior.Once)
                sub = Section.PrintBehavior switch
                {
                    SectionPrintBehavior.OnEveryPage            => "každá stránka",
                    SectionPrintBehavior.OnFirstPageOnly        => "jen 1. stránka",
                    SectionPrintBehavior.OnLastPageOnly         => "jen posl. stránka",
                    SectionPrintBehavior.OnEveryPageExceptFirst => "kromě 1. stránky",
                    _ => null
                };
            if (Section.PrintBehavior == SectionPrintBehavior.PerDataRow && !string.IsNullOrEmpty(Section.DataSource))
                sub = (sub is null ? "" : sub + " · ") + Section.DataSource;
            return sub;
        }
    }

    public bool CanMoveUp   => _vm.State.CurrentTemplate.Sections.IndexOf(Section) > 0;
    public bool CanMoveDown => _vm.State.CurrentTemplate.Sections.IndexOf(Section)
                               < _vm.State.CurrentTemplate.Sections.Count - 1;

    private RelayCommand? _moveUp, _moveDown, _delete, _saveTemplate, _activate, _showProps;

    public RelayCommand MoveUpCommand       => _moveUp       ??= new(() => _vm.MoveSection(Section, -1),   () => CanMoveUp);
    public RelayCommand MoveDownCommand     => _moveDown     ??= new(() => _vm.MoveSection(Section, +1),   () => CanMoveDown);
    public RelayCommand DeleteCommand       => _delete       ??= new(() => _vm.DeleteSection(Section));
    public RelayCommand SaveTemplateCommand => _saveTemplate ??= new(() => SaveTemplateGlobal?.Invoke(Section));
    public RelayCommand ActivateCommand     => _activate     ??= new(() => _vm.ActivateSection(Section));
    public RelayCommand ShowPropertiesCommand => _showProps  ??= new(() => ShowPropertiesGlobal?.Invoke(Section));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ─────────────────────────────────────────────────────────────────────────────
// Schema ViewModels
// ─────────────────────────────────────────────────────────────────────────────

public class SchemaGroupVM
{
    public string                     Header { get; }
    public IReadOnlyList<SchemaFieldVM> Fields { get; }

    public SchemaGroupVM(string header, IEnumerable<SchemaFieldVM> fields)
    {
        Header = header;
        Fields = [.. fields];
    }
}

public class SchemaFieldVM
{
    public string FieldName   { get; }
    public string Label       { get; }
    public string Placeholder => $"{{{FieldName}}}";
    public string LabelDisplay => string.IsNullOrEmpty(Label) ? "" : "  " + Label;

    public SchemaFieldVM(string fieldName, string label)
    {
        FieldName = fieldName;
        Label     = label;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ParameterControlVM
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Volba enum parametru sestavy: <paramref name="Value"/> se ukládá, <paramref name="Label"/> se zobrazuje.</summary>
public sealed record ParameterOption(string Value, string Label);

public class ParameterControlVM : INotifyPropertyChanged
{
    private readonly TemplateParameter           _param;
    private readonly Dictionary<string, object?> _values;
    private readonly Action?                     _onChanged;
    private readonly Action?                     _onCommit;
    private readonly Func<Template?>?            _template;
    private bool                                 _isVisible = true;

    public ParameterControlVM(TemplateParameter param, Dictionary<string,object?> values,
        Action? onChanged, Action? onCommit = null, Func<Template?>? template = null)
    {
        _param     = param;
        _values    = values;
        _onChanged = onChanged;
        _onCommit  = onCommit;
        _template  = template;
    }

    /// <summary>
    /// Naplní kolekci VM pro daný seznam parametrů + sdílený <paramref name="values"/> slovník.
    /// Auto-zapojuje re-vyhodnocení viditelnosti všech VM po každé změně (volá se PŘED <paramref name="onChanged"/>)
    /// a provede initial visibility check po naplnění. Sjednocuje wiring mezi preview lištou (MainWindow) a PrintDialog.
    /// </summary>
    /// <param name="target">Cílová kolekce (typicky ObservableCollection napojená na ItemsControl).</param>
    /// <param name="parameters">Definice parametrů agendy/šablony.</param>
    /// <param name="values">Slovník hodnot - sdílený mezi VM (každá změna ho aktualizuje).</param>
    /// <param name="onChanged">Volitelný callback po změně hodnoty (např. invalidace cache + překreslení).</param>
    /// <param name="commitFactory">Volitelný factory - pro každý parametr může vrátit commit akci (Načíst tlačítko).</param>
    /// <param name="template">Aktuální šablona pro parametry závislé na sloupci prvku (čte se při každém vyhodnocení).</param>
    public static void Init(
        ICollection<ParameterControlVM>      target,
        IEnumerable<TemplateParameter>       parameters,
        Dictionary<string, object?>          values,
        Action?                              onChanged     = null,
        Func<TemplateParameter, Action?>?    commitFactory = null,
        Func<Template?>?                     template      = null)
    {
        target.Clear();
        foreach (var p in parameters)
        {
            // Auto-refresh visibility všech VM před externím callbackem - VisibleWhen rule ve VisualWhen
            // se musí přepočítat dřív, než UI dostane signál k překreslení.
            Action vmOnChanged = () =>
            {
                foreach (var vm in target) vm.UpdateVisibility();
                onChanged?.Invoke();
            };
            target.Add(new ParameterControlVM(p, values, vmOnChanged, commitFactory?.Invoke(p), template));
        }

        // Initial visibility check - proběhne až po naplnění kolekce, aby VisibleWhen vidělo všechny VM.
        foreach (var vm in target) vm.UpdateVisibility();
    }

    public string                    Label         => _param.Label;
    public string                    ParameterType => _param.Type;
    /// <summary>Volby enum parametru: hodnota je uložený text volby, popisek to, co uživatel vidí.</summary>
    public IReadOnlyList<ParameterOption> Options =>
        _options ??= [.. (_param.Options ?? []).Select(o => new ParameterOption(o, _param.OptionLabel(o)))];
    private IReadOnlyList<ParameterOption>? _options;
    /// <summary>Skupina pro CollectionViewSource grouping v liště parametrů (prázdné = bez skupiny).</summary>
    public string                    Group         => _param.Group ?? "";

    /// <summary>Hodnota parametru - setter volá onChanged (live refresh) při každé změně.</summary>
    public object? Value
    {
        get => _values.TryGetValue(_param.Name, out var v) ? v : _param.GetDefaultTyped();
        set { _values[_param.Name] = value; OnPropertyChanged(); _onChanged?.Invoke(); }
    }

    /// <summary>
    /// Notifikuje UI binding po externí změně <c>_values[Name]</c> (např. po loaded PrintParams).
    /// Bez volání setteru se neaktivuje OnPropertyChanged - proto explicit notifikace.
    /// </summary>
    public void NotifyValueChanged() => OnPropertyChanged(nameof(Value));

    /// <summary>Viditelnost parametru - řízena dependency rule VisibleWhen z TemplateParameter.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
    }

    /// <summary>Vyhodnotí VisibleWhen rule proti aktuálním hodnotám. Volá se po každé změně libovolného parametru.</summary>
    public void UpdateVisibility() => IsVisible = _param.IsVisible(_values, _template?.Invoke());

    /// <summary>Má se volat při potvrzení hodnoty (Enter/LostFocus) - spustí těžší operace (reload dat).</summary>
    public bool HasCommit => _onCommit is not null;

    private RelayCommand? _commitCmd;
    public RelayCommand CommitCommand => _commitCmd ??= new(() => _onCommit?.Invoke());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Selector přesunut do Controls/ParameterTemplateSelector.cs (sdílený mezi MainWindow a PrintDialog).
