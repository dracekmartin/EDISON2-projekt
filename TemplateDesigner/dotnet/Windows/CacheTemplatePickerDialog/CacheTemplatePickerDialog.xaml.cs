using System.Windows;

namespace TemplateDesigner;

public partial class CacheTemplatePickerDialog : Window
{
    private readonly CacheTemplatePickerDialogViewModel _vm;

    public CacheTemplatePickerDialog(CacheTemplatePickerDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();

        // Wire-up EDList eventů → VM
        TheList.SelectionChanged += (_, _) => _vm.Selected = TheList.SelectedRow;
        TheList.RowDoubleClicked += (_, _) => { if (_vm.OpenCommand.CanExecute(null)) _vm.OpenCommand.Execute(null); };

        // VM žádá reload (po Delete / Rename) — sebere current filter a znovu načte
        _vm.RequestReload = () => TheFilter.Refresh();

        // VM žádá nový název pro rename — otevře dialog (WPF drží View)
        _vm.RequestRenameInput = oldName =>
        {
            var dvm = new SaveSectionTemplateDialogViewModel(oldName);
            var win = new SaveSectionTemplateDialog(dvm) { Owner = this };
            return win.ShowDialog() == true ? dvm.Name : null;
        };

        // Close
        _vm.RequestClose += result => { DialogResult = result; Close(); };

        // Init sloupců po načtení okna; EDFilter sám zavolá první LoadList přes initial Refresh
        Loaded += (_, _) => TheList.InitializeList();
    }
}
