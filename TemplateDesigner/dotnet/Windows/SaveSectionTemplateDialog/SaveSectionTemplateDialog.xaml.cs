using System.Windows;

namespace TemplateDesigner;

public partial class SaveSectionTemplateDialog : Window
{
    public SaveSectionTemplateDialog(SaveSectionTemplateDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };

        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }
}
