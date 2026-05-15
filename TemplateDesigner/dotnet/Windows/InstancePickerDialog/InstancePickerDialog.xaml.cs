using System.Windows;

namespace TemplateDesigner;

public partial class InstancePickerDialog : Window
{
    public InstancePickerDialog(InstancePickerDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };

        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
