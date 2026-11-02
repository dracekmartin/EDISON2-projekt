using System;
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
        Tree.SelectedItemChanged += (_, e) => _vm.SelectedNode = e.NewValue as TemplateCatalogNodeVM;

        // VM žádá načtení seznamu (jiný uzel stromu, Delete, Rename) s filtrem vybraného uzlu.
        // RequestReload je Action, takže async cesta musí chybu spolknout tady (fire-and-forget).
        _vm.RequestReload = async () =>
        {
            try { await TheList.LoadListAsync(_vm.CurrentFilter); }
            catch (Exception ex) { _vm.PopupDisplayer.Error(ex); }
        };

        // VM žádá nový název pro rename - otevře dialog (WPF drží View)
        _vm.RequestRenameInput = oldName =>
        {
            var dvm = new SaveSectionTemplateDialogViewModel(oldName);
            var win = new SaveSectionTemplateDialog(dvm) { Owner = this };
            return win.ShowDialog() == true ? dvm.Name : null;
        };

        // Close
        _vm.RequestClose += result => { DialogResult = result; Close(); };

        // Sloupce, strom s posledním uzlem, pak seznam toho uzlu.
        // Async (R1): na pomalé lince by synchronní varianta zmrazila celou aplikaci, ne jen dialog.
        Loaded += async (_, _) =>
        {
            try
            {
                await TheList.InitializeListAsync();
                await _vm.LoadCatalogAsync();
                await TheList.LoadListAsync(_vm.CurrentFilter);
            }
            catch (Exception ex) { _vm.PopupDisplayer.Error(ex); }

            // Dotaz na kanál až po zobrazení seznamu a bez čekání: nabídka nových šablon se
            // doplní, až přijde odpověď. Přehled se kvůli helpdesku nesmí zdržet ani spadnout.
            await _vm.CheckChannelAsync();
        };
    }
}
