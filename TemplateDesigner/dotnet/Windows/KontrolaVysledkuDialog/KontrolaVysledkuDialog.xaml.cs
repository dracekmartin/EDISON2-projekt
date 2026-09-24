using System.Windows;

namespace TemplateDesigner;

public partial class KontrolaVysledkuDialog : Window
{
    public KontrolaVysledkuDialog(KontrolaVysledkuDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += _ => Close();
    }
}
