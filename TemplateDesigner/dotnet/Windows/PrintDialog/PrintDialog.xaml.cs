using System.Windows;

namespace TemplateDesigner;

public partial class PrintDialog : Window
{
    public PrintDialog(PrintDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
