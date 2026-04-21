using System.Windows;

namespace TemplateDesigner;

public partial class NormalizePositionsDialog : Window
{
    public NormalizePositionsDialog(NormalizePositionsDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
