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
    /// <summary>Klíč pamatování filtru hledání (viz <c>EDInputForm.SearchMemoryKey</c>). Nastavuje
    /// ho jen otevření vlastností prvku z plátna - jinde se filtr nepamatuje.</summary>
    public string SearchMemoryKey { get => GeneralForm.SearchMemoryKey; set => GeneralForm.SearchMemoryKey = value; }

    private static readonly string GeneralSchemaClass = Models.ElementCatalog.FormClass(Models.ElementTypes.DepartureGrid);

    private readonly DepartureGroupEditorDialogViewModel _vm;
    private readonly DepartureGridElement _element;
    private Dictionary<string, string> _preFilled = [];

    public List<DepartureGroupColumnStyle> ResultGroupColumns => _vm.ResultGroupColumns;
    public ColumnVisualStyle? ResultHourColumnStyle => _vm.ResultHourColumnStyle;
    public string ResultHourColumnHeader => _vm.ResultHourColumnHeader;
    public float ResultHourColumnWidth => _vm.ResultHourColumnWidth;

    /// <summary>Definice vlastních sloupců (tab „Definice sloupců"). Null = automaticky dle linky.</summary>
    public List<DepartureColumnDefinition>? ResultCustomColumns => _vm.ResultCustomColumns;

    /// <summary>Command z tabu „Obecné" (EDInputForm) - null pokud se nic nezměnilo.
    /// Caller (MainWindow) ho vykoná spolu se Result* (per-column styly + hour).</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    public DepartureGroupEditorDialog(
        DepartureGridElement element,
        List<SkupinaDnuData> availableGroups,
        List<DepartureGroupColumnStyle> currentStyles,
        ColumnVisualStyle? hourColumnStyle,
        List<DepartureColumnDefinition>? customColumns = null,
        string? presetsJson = null,
        bool ukazatVlastniSloupce = true,
        IReadOnlyList<DepartureColumnPresets.Preset>? presetOffer = null)
    {
        _element = element;
        _vm = new DepartureGroupEditorDialogViewModel(element, availableGroups, currentStyles, hourColumnStyle,
            customColumns, presetsJson, ukazatVlastniSloupce, presetOffer);
        DataContext = _vm;
        InitializeComponent();
        Title = ElementNames.TitulekVlastnosti(element);

        // Tab „Obecné": EDInputForm (DepartureGridElementEdit přes reflexi). Per-skupina/sloupec
        // override řeší CellStyleEditor v tabu „Sloupce".
        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.SchemaLoaded += OnGeneralFormInitialized;
        ElementFormBinder.HookFontWeightRefresh(GeneralForm);

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

    // Záložka Obecné zapíše styl prvku až při OK - sloupce mají dědit z toho, co na ní právě je.
    private void ColumnsTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) _vm.ObnovZdedene(ElementFormBinder.Nahled(GeneralForm, _element, _preFilled));
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // EDInputForm validace „Obecné" - při chybě zůstat v dialogu.
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
