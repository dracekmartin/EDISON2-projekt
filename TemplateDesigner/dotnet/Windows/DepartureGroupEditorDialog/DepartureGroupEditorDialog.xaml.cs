using System.Collections.Generic;
using System.Windows;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Controls;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

public partial class DepartureGroupEditorDialog : Window
{
    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.DepartureGridElementEdit";

    private readonly DepartureGroupEditorDialogViewModel _vm;
    private readonly DepartureGridElement _element;
    private Dictionary<string, string> _preFilled = [];

    public List<DepartureGroupColumnStyle> ResultGroupColumns => _vm.ResultGroupColumns;
    public ColumnVisualStyle? ResultHourColumnStyle => _vm.ResultHourColumnStyle;

    /// <summary>Command z tabu „Obecné" (EDInputForm) — null pokud se nic nezměnilo.
    /// Caller (MainWindow) ho vykoná spolu se Result* (per-column styly + hour).</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

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

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralFormInitialized()
    {
        // Prefill přes reflexi. HeaderLine2Style (nullable) zůstává null → jeho pole se předvyplní
        // prázdná (= dědí z HeaderStyle); při OK ElementPropertyAccessor.Set lazy alokuje.
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        GeneralForm.Validate();
    }

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
