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
    /// <summary>Klíč pamatování filtru hledání (viz <c>EDInputForm.SearchMemoryKey</c>). Nastavuje
    /// ho jen otevření vlastností prvku z plátna - jinde se filtr nepamatuje.</summary>
    public string SearchMemoryKey { get => GeneralForm.SearchMemoryKey; set => GeneralForm.SearchMemoryKey = value; }

    private static readonly string GeneralSchemaClass = Models.ElementCatalog.FormClass(Models.ElementTypes.DynamicTable);

    private readonly DynamicTableEditorDialogViewModel _vm;
    private readonly DynamicTableElement _element;
    private readonly IReadOnlyList<DataFieldCollection> _collections;
    private Dictionary<string, string> _preFilled = [];

    // Vybraný zdroj pro seed sloupců. Kdyby se formulář načetl znovu, Prefill by výběr přepsal hodnotou
    // z elementu, proto se po Prefillu dosadí zpět.
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
        Title = Models.ElementNames.TitulekVlastnosti(element);

        // Tab „Obecné": EDInputForm (general properties přes reflexi). Sloupce → tab „Sloupce".
        GeneralForm.CacheClass = GeneralSchemaClass;
        GeneralForm.SchemaLoaded += OnGeneralFormInitialized;
        ElementFormBinder.HookFontWeightRefresh(GeneralForm);
        GeneralForm.ButtonClicked += OnGeneralButtonClicked;

        // Zdroj dat je EDInputForm List pole (SelectedValue není TwoWay by default → Field.Value se
        // propíše až při Save). Napojíme se přímo na SelectionChanged toho ComboBoxu, ať máme hodnotu
        // živě při výběru a hned naseedujeme sloupce (routed event bublá z GeneralFormu).
        GeneralForm.AddHandler(Selector.SelectionChangedEvent,
            new SelectionChangedEventHandler(OnFormSelectionChanged));

        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    private void OnGeneralButtonClicked(string key)
    {
        if (key != Models.TlacitkaFormulare.ZvyrazneniRadku) return;
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
        PopulateSortFields();
        _preFilled = ElementFormBinder.Prefill(GeneralForm, _element);
        ElementFormBinder.PopulateFontWeightFields(GeneralForm);   // řezy dle rodiny (před Validate)

        // Po Prefillu (hodnota z elementu) dosaď vybraný zdroj. _preFilled zůstává na hodnotě z elementu,
        // takže ExtractCommands správně detekuje změnu proti uloženému stavu.
        if (_selectedDataSource is not null && GeneralForm.GetField("DataSource") is { } dataSource)
            dataSource.Value = _selectedDataSource;

        GeneralForm.Validate();
    }

    /// <summary>Naplní List „Zdroj dat" kolekcemi aktuální agendy (Caché schema je nezná).</summary>
    private void PopulateDataSourceField()
    {
        var field = (GeneralForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == "DataSource");
        if (field is null) return;
        field.ListItems = _collections.Select(c => new ListItem(c.CollectionKey, c.Name)).ToList();
    }

    /// <summary>Naplní nabídku řazení sloupci prvku (Caché je nezná - jsou per element).
    /// Nabízí jen sloupce s datovým polem; počítané nemají co porovnat bez vyhodnocení výrazu.</summary>
    private void PopulateSortFields()
    {
        var items = _vm.ColumnVMs
            .Where(c => !string.IsNullOrWhiteSpace(c.Field))
            .Select(c => new ListItem(c.Field!, string.IsNullOrWhiteSpace(c.Header) ? c.Field! : c.Header))
            .ToList();

        foreach (var key in new[] { "SortField", "ThenByField" })
            if ((GeneralForm.Items ?? []).OfType<Field>().FirstOrDefault(f => f.Key == key) is { } field)
                field.ListItems = items;
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

    // Záložka Obecné zapíše styl prvku až při OK - sloupce mají dědit z toho, co na ní právě je.
    private void ColumnsTab_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) _vm.ObnovZdedene(ElementFormBinder.Nahled(GeneralForm, _element, _preFilled));
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // EDInputForm validace „Obecné" - při chybě zůstat v dialogu. Zvolený zdroj v poli už je (SelectedValue
        // je s Value svázaný obousměrně), znovu ho nedosazovat: prázdný výběr dialog nepamatuje a vrátil by smazaný zdroj.
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
