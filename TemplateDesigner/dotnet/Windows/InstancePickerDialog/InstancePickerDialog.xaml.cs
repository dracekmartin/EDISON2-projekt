using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EDControls;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

public partial class InstancePickerDialog : Window
{
    private readonly InstancePickerDialogViewModel _vm;

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
        // ^||ML("IN","filter",...) - dispatch je přečte a předá do GetAvailableInstances.
        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            ReloadInstances();

            // Po načtení dat - předoznačit initially selected
            foreach (var id in _vm.InitiallySelectedValues)
                TheList.SetChecked(id, true);

            _vm.OnCheckedRowsChanged(_vm.InitiallySelectedValues.Count, TheList.Rows.Count);
        };
    }

    /// <summary>Naplní filtr skupin, předvybere aktuální/první a napojí přenačtení instancí na výběr.</summary>
    private void SetupSkupinaFilter()
    {
        SkupinaRow.Visibility = Visibility.Visible;

        // Zobrazení „číslo - název" a hledání dle obojího umí sdílená komponenta sama (její výchozí
        // separátor je totéž co AgendaSkupina.Display), takže se sem posílá holý Key + Label.
        CmbSkupina.Suggestions = _vm.Skupiny.Select(s => new ListItem(s.Value, s.Label)).ToList();

        // Předvýběr: uložená/aktuální skupina, jinak první v seznamu (= server default). Musí být
        // hotový DŘÍV, než se napojí DropDownClosed - jinak by předvýběr přenačetl list, který
        // stejně vzápětí načte Loaded.
        var initial = _vm.Skupiny.FirstOrDefault(s => s.Value == _vm.SelectedSkupinaId) ?? _vm.Skupiny.FirstOrDefault();
        _vm.SelectedSkupinaId = initial?.Value;
        _vm.SelectedSkupinaLabel = initial?.Display;
        CmbSkupina.SelectedKey = initial?.Value;

        // Striktní režim fixuje výběr až ve svém DropDownClosed; náš handler je registrovaný později,
        // takže SelectedKey už nese novou hodnotu.
        CmbSkupina.DropDownClosed += (_, _) =>
        {
            var s = _vm.Skupiny.FirstOrDefault(x => x.Value == CmbSkupina.SelectedKey);
            if (s is null || s.Value == _vm.SelectedSkupinaId) return;
            _vm.SelectedSkupinaId = s.Value;
            _vm.SelectedSkupinaLabel = s.Display;
            ReloadInstances();
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
            // Chip ukazuje „číslo - název"; sloupce v pickeru jsou oddělené (cislo/nazev).
            var cislo = row["cislo"];
            var nazev = row["nazev"];
            var display = !string.IsNullOrEmpty(nazev)
                ? (string.IsNullOrEmpty(cislo) ? nazev : $"{cislo} - {nazev}")
                : (string.IsNullOrEmpty(cislo) ? id : cislo);
            var subtitle = row["subtitle"];
            result.Add(new AgendaInstance(id, display, subtitle));
        }
        return result;
    }
}
