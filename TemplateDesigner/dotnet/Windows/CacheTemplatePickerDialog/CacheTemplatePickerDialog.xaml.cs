using System;
using System.Windows;
using System.Windows.Media;
using EDControls.EDComponentToolbar;

namespace TemplateDesigner;

public partial class CacheTemplatePickerDialog : Window
{
    private readonly CacheTemplatePickerDialogViewModel _vm;

    public CacheTemplatePickerDialog(CacheTemplatePickerDialogViewModel vm)
    {
        DataContext = _vm = vm;
        InitializeComponent();
        // Zkratky akcí nad seznamem (Ctrl+N/E/D) a hlášky s tímto oknem jako vlastníkem.
        _vm.AttachToWindow(this);

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

        // Upravit = týž formulář šablony jako Uložit jako (název, agenda jen ke čtení), WPF drží View.
        _vm.RequestEditInput = (oldName, agendaLabel, agendaCode) =>
        {
            var dvm = new SaveToCacheDialogViewModel(oldName, agendaCode, agendaLabel) { WindowTitle = "Upravit šablonu" };
            var win = new SaveToCacheDialog(dvm) { Owner = this };
            return win.ShowDialog() == true ? dvm.Name : null;
        };

        // Zavření (OK, Storno, Esc, dvojklik) obsluhuje EDFooter - druhý odběratel RequestClose by po zavřeném
        // okně nastavoval DialogResult a spadl.

        // Pruh nad stromem drží výšku pruhu seznamu - ten je vyšší o pole hledání a výška se mění s velikostí písma.
        TheList.Loaded += (_, _) =>
        {
            if (FindDescendant<EDComponentToolbar>(TheList) is not { } listHeader) return;
            TreeHeader.MinHeight = listHeader.ActualHeight;
            listHeader.SizeChanged += (_, e) => TreeHeader.MinHeight = e.NewSize.Height;
        };

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

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
