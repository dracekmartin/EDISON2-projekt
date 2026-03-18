using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

public partial class StopColumnsEditorDialog : Window
{
    private readonly StopColumnsEditorDialogViewModel _vm;

    /// <summary>Výsledné sloupce po OK — pass-through z VM.</summary>
    public List<StopColumn> ResultColumns => _vm.ResultColumns;

    public StopColumnsEditorDialog(List<StopColumn> columns)
    {
        _vm         = new StopColumnsEditorDialogViewModel(columns);
        DataContext = _vm;
        InitializeComponent();

        _vm.RequestCommitEdits = () =>
            ColumnsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        _vm.RequestClose += result => { DialogResult = result; Close(); };
    }

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
