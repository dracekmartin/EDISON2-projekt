using System.Windows;

namespace TemplateDesigner;

public partial class ChangeAgendaDialog : Window
{
    public ChangeAgendaDialog(ChangeAgendaDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }
}
