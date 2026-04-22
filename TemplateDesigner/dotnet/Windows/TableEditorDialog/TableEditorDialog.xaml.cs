using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Controls;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;
using TemplateDesigner.Serialization;

namespace TemplateDesigner;

public partial class TableEditorDialog : Window
{
    private readonly TableEditorDialogViewModel _vm;

    /// <summary>Výsledné sloupce po OK — pass-through z VM.</summary>
    public List<TableColumn> ResultColumns => _vm.ResultColumns;

    /// <summary>Výsledné řádky po OK — pass-through z VM.</summary>
    public List<TableRow>? ResultRows => _vm.ResultRows;

    /// <summary>Výsledné souhrnné řádky po OK — pass-through z VM.</summary>
    public List<TableRow>? ResultSummaryRows => _vm.ResultSummaryRows;

    public TableEditorDialog(TableElement element, IReadOnlyList<DataField>? collectionFields = null,
        ITableTemplateProvider? templateProvider = null)
    {
        _vm         = new TableEditorDialogViewModel(element, collectionFields, templateProvider);
        DataContext = _vm;
        InitializeComponent();

        _vm.RequestScrollColumnIntoView = vm => ColumnsGrid.ScrollIntoView(vm);
        _vm.RequestCommitEdits          = () =>
        {
            ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            CellsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            SummaryCellsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };

        _vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // Klávesová navigace v DataGridu — Enter=BeginEdit, Tab=CommitEdit
    // Musí být v code-behind: přistupuje k DataGrid.BeginEdit() / CommitEdit()
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid dg) return;
        if (e.Key == Key.Enter && dg.CurrentCell.Item != null)
        {
            dg.BeginEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
        }
    }
}
