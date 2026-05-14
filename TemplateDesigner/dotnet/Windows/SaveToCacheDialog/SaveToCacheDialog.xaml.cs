using System.Windows;

namespace TemplateDesigner;

public partial class SaveToCacheDialog : Window
{
    public SaveToCacheDialog(SaveToCacheDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }
}
