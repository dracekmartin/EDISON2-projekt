using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner;

public partial class InstancePickerDialog : Window
{
    private readonly InstancePickerDialogViewModel _vm;

    public InstancePickerDialog(InstancePickerDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();

        // Konfigurace EDListu
        TheList.CacheClass = "ALVA.TemplateDesigner.API.InstancesList";
        TheList.HostId = "TemplateDesigner.InstancePicker";
        TheList.Title = "Výběr instancí";
        TheList.SelectionMode = EDControls.EDList.EListSelectionMode.Check;

        // Eventy
        TheList.CheckedRowsChanged += (_, ids) => _vm.OnCheckedRowsChanged(ids.Count, TheList.Rows.Count);

        // Callbacky z VM
        _vm.RequestSelectedInstances = BuildSelectedInstances;

        // Close
        _vm.RequestClose += result => { DialogResult = result; Close(); };

        // Init list po načtení okna
        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            TheList.LoadList(new Dictionary<string, string> { ["agendaCode"] = _vm.AgendaCode });

            // Po načtení dat — předoznačit initially selected
            foreach (var id in _vm.InitiallySelectedValues)
                TheList.SetChecked(id, true);

            _vm.OnCheckedRowsChanged(_vm.InitiallySelectedValues.Count, TheList.Rows.Count);
        };
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
