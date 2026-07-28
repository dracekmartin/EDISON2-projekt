using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EDControls;
using EDControls.EDInputForm;
using TemplateDesigner.Commands;
using TemplateDesigner.Commands.Implementations;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

public partial class DynamicTableEditorDialog : Window
{
    private const string GeneralSchemaClass = "ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit";

    private readonly DynamicTableEditorDialogViewModel _vm;
    private readonly DynamicTableElement _element;
    private readonly IReadOnlyList<DataFieldCollection> _collections;
    private Dictionary<string, string> _preFilled = [];
    private Field? _dataSourceField;

    // Dialog-scoped transient stav vybraného zdroje. EDInputForm se re-Inituje při každém návratu na
    // tab (auto-Init na Loaded) → Prefill by ho přepsal hodnotou z elementu (u nové tabulky prázdnou).
    // Držíme ho tady a po každém Prefillu znovu aplikujeme, ať výběr přežije přepínání tabů.
    private string? _selectedDataSource;

    /// <summary>Výsledné sloupce po OK - pass-through z VM.</summary>
    public List<TableColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Command z tabu „Obecné" (EDInputForm) - null pokud se nic nezměnilo.
    /// Caller vykoná spolu se ResultColumns.</summary>
    public IDesignerCommand? GeneralCommand { get; private set; }

    /// <summary>Pravidla zvýraznění po editaci (button „Zvýraznění řádků…"). Aplikuje caller až na OK
    /// dialogu (deferred - Storno columns dialogu změnu zahodí). <see cref="HighlightEdited"/> = bylo upraveno.</summary>
    public List<HighlightRule>? ResultHighlightRules { get; private set; }
    public bool HighlightEdited { get; private set; }

    public DynamicTableEditorDialog(DynamicTableElement element, IReadOnlyList<DataFieldCollection>? collections)
    {
        _element     = element;
        _collections = collections ?? [];
        _selectedDataSource = string.IsNullOrWhiteSpace(element.DataSource) ? null : element.DataSource;

        _vm = new DynamicTableEditorDialogViewModel(element, _collections);
        // Seed při otevření: má-li tabulka zdroj, ale žádné sloupce (např. zdroj nastavený v panelu),
        // naseeduj je z kolekce. No-op, když už sloupce má (SeedColumnsFromCollection je respektuje).
        SeedForKey(_selectedDataSource);
        DataContext = _vm;
        InitializeComponent();

        // Tab „Obecné": EDInputForm (general properties přes reflexi). Sloupce → tab „Sloupce".
        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.Initialized += OnGeneralFormInitialized;
        GeneralForm.ButtonClicked += OnGeneralButtonClicked;

        // Zdroj dat je EDInputForm List pole (SelectedValue není TwoWay by default → Field.Value se
        // propíše až při Save). Napojíme se přímo na SelectionChanged toho ComboBoxu, ať máme hodnotu
        // živě při výběru a hned naseedujeme sloupce (routed event bublá z GeneralFormu).
        GeneralForm.AddHandler(Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(OnFormSelectionChanged));

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralButtonClicked(string key)
    {
        if (key != "__highlightRules") return;
        var current = ResultHighlightRules ?? _element.HighlightRules;   // reopen ukáže rozeditovaná pravidla
        var dlg = new HighlightRulesEditorDialog(current) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ResultHighlightRules = dlg.ResultRules;
            HighlightEdited = true;
        }
    }

    // Běží při KAŽDÉM (re-)Initu formuláře - tj. i po návratu na tab Obecné (Loaded auto-Init).
    private void OnGeneralFormInitialized()
    {
        ElementFormBinder.PopulateFontFields(GeneralForm);
        PopulateDataSourceField();
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        ElementFormBinder.PopulateFontWeightFields(GeneralForm);   // řezy dle rodiny (před Validate)

        _dataSourceField = (GeneralForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == "DataSource");

        // Po Prefillu (který nastavil hodnotu z elementu) znovu aplikuj vybraný zdroj z transient stavu,
        // ať re-Init při přepnutí tabu výběr nevymaže. _preFilled zůstává na hodnotě z elementu, takže
        // ExtractCommands správně detekuje změnu proti uloženému stavu.
        if (_dataSourceField is not null && _selectedDataSource is not null)
            _dataSourceField.Value = _selectedDataSource;

        GeneralForm.Validate();
    }

    /// <summary>Naplní List „Zdroj dat" kolekcemi aktuální agendy (Caché schema je nezná).</summary>
    private void PopulateDataSourceField()
    {
        var field = (GeneralForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == "DataSource");
        if (field is null) return;
        field.ListItems = _collections.Select(c => new ListItem(c.CollectionKey, c.Name)).ToList();
    }

    /// <summary>Přímý odchyt výběru v ComboBoxu pole „Zdroj dat" - čte SelectedValue živě, uloží do
    /// transient stavu i modelu a hned naseeduje sloupce.</summary>
    private void OnFormSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource is not ComboBox cb || cb.DataContext is not Field f || f.Key != "DataSource")
            return;
        if (cb.SelectedValue is not string key || string.IsNullOrWhiteSpace(key)) return;

        _selectedDataSource = key;
        f.Value = key;   // ať to sedí i pro Save()/ExtractCommands
        SeedForKey(key);
    }

    private void SeedForKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var coll = _collections.FirstOrDefault(c => c.CollectionKey == key);
        if (coll is { Fields.Count: > 0 })
            _vm.SeedColumnsFromCollection(coll.Fields, key);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Zajisti aktuální zdroj v poli (ComboBox ho mohl při přepnutí tabu vynulovat) - ať Save/
        // ExtractCommands uloží zvolený zdroj, i když uživatel nebyl naposledy na tabu Obecné.
        if (_dataSourceField is not null && _selectedDataSource is not null)
            _dataSourceField.Value = _selectedDataSource;

        // EDInputForm validace „Obecné" - při chybě zůstat v dialogu.
        if (!GeneralForm.Save()) return;

        // Pojistka: kdyby uživatel vybral zdroj, ale nepřepnul na tab Sloupce, naseeduj teď.
        SeedForKey(_selectedDataSource);

        var cmds = ElementFormBinder.ExtractCommands(GeneralForm, _element, _preFilled);
        GeneralCommand = cmds.Count switch
        {
            0 => null,
            1 => cmds[0],
            _ => new CompositeCommand("Nastavení tabulky", cmds)
        };

        // VM dokončí (ResultColumns, zavření).
        _vm.OkCommand.Execute(null);
    }
}
