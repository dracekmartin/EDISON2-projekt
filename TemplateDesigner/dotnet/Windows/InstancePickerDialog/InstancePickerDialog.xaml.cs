using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

public partial class InstancePickerDialog : Window
{
    private readonly InstancePickerDialogViewModel _vm;

    // Searchable combobox skupin: filtrujeme přes ICollectionView (ne Items.Clear → SelectedItem přežije).
    private ICollectionView? _skupinyView;
    private string _skupinaFilter = "";
    private bool _skupinaUserTyping;      // rozliší uživatelské psaní od programové změny textu (výběr)
    private bool _suppressSkupinaReload;   // předvýběr nesmí přenačíst list (dělá to Loaded)

    public InstancePickerDialog(InstancePickerDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();

        // Eventy
        TheList.CheckedRowsChanged += (_, ids) => _vm.OnCheckedRowsChanged(ids.Count, TheList.Rows.Count);

        // Callbacky z VM
        _vm.RequestSelectedInstances = BuildSelectedInstances;

        // Close
        _vm.RequestClose += result => { DialogResult = result; Close(); };

        if (_vm.HasSkupiny) SetupSkupinaFilter();

        // Init list po načtení okna. Agenda + zvolená skupina jdou do EDList filtru pod
        // ^||ML("IN","filter",...) — dispatch je přečte a předá do GetAvailableInstances.
        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            ReloadInstances();

            // Po načtení dat — předoznačit initially selected
            foreach (var id in _vm.InitiallySelectedValues)
                TheList.SetChecked(id, true);

            _vm.OnCheckedRowsChanged(_vm.InitiallySelectedValues.Count, TheList.Rows.Count);
        };
    }

    /// <summary>Naplní filtr skupin (searchable combobox), předvybere aktuální/první a napojí přenačtení.</summary>
    private void SetupSkupinaFilter()
    {
        SkupinaRow.Visibility = Visibility.Visible;

        _skupinyView = CollectionViewSource.GetDefaultView(_vm.Skupiny);
        _skupinyView.Filter = o => _skupinaFilter == ""
            || (o is AgendaSkupina s && s.Display.Contains(_skupinaFilter, StringComparison.OrdinalIgnoreCase));
        CmbSkupina.ItemsSource = _skupinyView;

        // Předvýběr: uložená/aktuální skupina, jinak první v seznamu (= server default).
        var initial = _vm.Skupiny.FirstOrDefault(s => s.Value == _vm.SelectedSkupinaId) ?? _vm.Skupiny.FirstOrDefault();
        _suppressSkupinaReload = true;
        CmbSkupina.SelectedItem = initial;
        _vm.SelectedSkupinaId = initial?.Value;
        _vm.SelectedSkupinaLabel = initial?.Display;
        _suppressSkupinaReload = false;

        // Contains-filtr jen při uživatelském psaní (programová změna textu při výběru nerefiltruje).
        CmbSkupina.PreviewKeyUp += (_, _) => _skupinaUserTyping = true;
        CmbSkupina.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) =>
        {
            if (!_skupinaUserTyping) return;
            _skupinaFilter = CmbSkupina.Text ?? "";
            _skupinyView?.Refresh();
            CmbSkupina.IsDropDownOpen = true;
        }));
        CmbSkupina.SelectionChanged += (_, _) =>
        {
            _skupinaUserTyping = false;
            if (CmbSkupina.SelectedItem is not AgendaSkupina s) return;
            _vm.SelectedSkupinaId = s.Value;
            _vm.SelectedSkupinaLabel = s.Display;
            // Reset filtru → příště plný seznam.
            if (_skupinaFilter != "") { _skupinaFilter = ""; _skupinyView?.Refresh(); }
            if (!_suppressSkupinaReload) ReloadInstances();
        };
    }

    /// <summary>Přenačte seznam instancí (EDList) pro aktuální agendu + zvolenou skupinu.</summary>
    private void ReloadInstances()
    {
        var filter = new Dictionary<string, string> { ["agendaCode"] = _vm.AgendaCode };
        if (!string.IsNullOrEmpty(_vm.SelectedSkupinaId))
            filter["SKUPINA_LINEK_ID"] = _vm.SelectedSkupinaId!;
        TheList.LoadList(filter);
    }

    /// <summary>Mapuje zaškrtnuté EDListRow → AgendaInstance pro výstup VM.</summary>
    private IReadOnlyList<AgendaInstance> BuildSelectedInstances()
    {
        var result = new List<AgendaInstance>();
        foreach (var id in TheList.SelectedRowIds)
        {
            var row = TheList.Rows.FirstOrDefault(r => r.Id == id);
            if (row is null) continue;
            var display = row["display"] ?? id;
            var subtitle = row["subtitle"];
            result.Add(new AgendaInstance(id, display, subtitle));
        }
        return result;
    }
}
