using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Controls;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner;

public partial class DepartureGroupEditorDialog : Window
{
    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit";

    private readonly DepartureGroupEditorDialogViewModel _vm;
    private readonly DepartureGridElement _element;
    private Dictionary<string, string> _preFilled = [];
    private readonly Dictionary<string, IDesignerCommand> _styleCommands = [];

    public List<DepartureGroupColumnStyle> ResultGroupColumns => _vm.ResultGroupColumns;
    public ColumnVisualStyle? ResultHourColumnStyle => _vm.ResultHourColumnStyle;

    /// <summary>Command z tabu „Obecné" (EDInputForm) — null pokud se nic nezměnilo.
    /// Caller (MainWindow) ho vykoná spolu se Result* (per-column styly + hour).</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    /// <summary>Commandy ze stylových pod-formulářů (tlačítka „Styl…"), keyované prefixem zóny
    /// (opakované otevření téže zóny nahradí předchozí). Caller je přidá do composite.</summary>
    public IReadOnlyCollection<IDesignerCommand> StyleCommands => _styleCommands.Values;

    public DepartureGroupEditorDialog(
        DepartureGridElement element,
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle)
    {
        _element = element;
        _vm = new DepartureGroupEditorDialogViewModel(element, availableGroups, currentStyles, hourColumnStyle);
        DataContext = _vm;
        InitializeComponent();

        // Tab „Obecné": EDInputForm (DepartureGridElementEdit přes reflexi). Per-skupina/sloupec
        // override řeší CellStyleEditor v tabu „Sloupce".
        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.Initialized += OnGeneralFormInitialized;
        GeneralForm.ButtonClicked += OnGeneralButtonClicked;

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralFormInitialized()
    {
        ElementFormBinder.PopulateFontFields(GeneralForm);
        // Prefill přes reflexi. HeaderLine2Style (nullable) zůstává null → jeho pole se předvyplní
        // prázdná (= dědí z HeaderStyle); při OK ElementPropertyAccessor.Set lazy alokuje.
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        ElementFormBinder.PopulateFontWeightFields(GeneralForm);   // řezy dle rodiny (před Validate)
        GeneralForm.Validate();
    }

    // Tlačítko „Styl…" v EDInputformu → otevři pod-formulář stylu pro daný prefix (key = "style:<prefix>").
    // Command se akumuluje a vykoná se v parent OK (batch, jeden undo krok).
    private void OnGeneralButtonClicked(string key)
    {
        const string marker = "style:";
        if (!key.StartsWith(marker, System.StringComparison.Ordinal)) return;
        var prefix = key[marker.Length..];
        var title = (GeneralForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == key)?.Label?.TrimEnd('…', ' ') ?? "Styl";

        var vm = new StyleEditDialogViewModel(_element, prefix, ResolveInherited(prefix));
        var dlg = new StyleEditDialog(vm) { Owner = this, Title = title };
        if (dlg.ShowDialog() == true && vm.ResultCommand is not null)
            _styleCommands[prefix] = vm.ResultCommand;   // replace (poslední edit zóny vyhraje)
    }

    /// <summary>Vyřešený rodičovský styl zóny (= zděděné hodnoty pro šedé placeholdery v CellStyleEditoru).</summary>
    private ResolvedTextStyle ResolveInherited(string prefix) => prefix switch
    {
        "HeaderStyle"      => _element.ResolveElementHeaderParent(),
        "HeaderLine2Style" => _element.ResolveElementHeaderParent(),   // 2. řádek dědí ze záhlaví
        "DataStyle"        => _element.ResolveElementDataParent(),
        "MinuteStyle"      => _element.ResolveGroupMinuteParent(),
        _                  => CellDefaults.Default
    };

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // EDInputForm validace „Obecné" — při chybě zůstat v dialogu.
        if (!GeneralForm.Save()) return;

        var cmds = ElementFormBinder.ExtractCommands(GeneralForm, _element, _preFilled);
        GeneralCommand = cmds.Count switch
        {
            0 => null,
            1 => cmds[0],
            _ => new CompositeCommand("Nastavení odjezdů", cmds)
        };

        // VM dokončí (ResultGroupColumns, ResultHourColumnStyle, hour writes, zavření).
        _vm.OkCommand.Execute(null);
    }
}
