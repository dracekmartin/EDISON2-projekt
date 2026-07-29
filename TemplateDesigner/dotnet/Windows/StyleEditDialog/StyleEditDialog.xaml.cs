using System.Windows;
using TemplateDesigner.Controls;

namespace TemplateDesigner;

/// <summary>
/// Pod-formulář pro editaci jednoho TextStyle (zóny) přes sdílený <c>CellStyleEditor</c>
/// (tri-state - zděděné hodnoty jako šedý placeholder). VM se vytvoří vně; po OK je v
/// <see cref="StyleEditDialogViewModel.ResultCommand"/> undo-able command.
/// </summary>
public partial class StyleEditDialog : Window
{
    private readonly StyleEditDialogViewModel _vm;

    internal StyleEditDialog(StyleEditDialogViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        vm.RequestColorPicker = hex => ColorPickerHelper.PickColor(this, hex);
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
