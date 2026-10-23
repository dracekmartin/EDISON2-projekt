using System.Windows;

namespace TemplateDesigner;

public partial class LaunchPdfWindow : Window
{
    public LaunchPdfWindow(LaunchPdfWindowViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += _ => Close();
        Loaded += async (_, _) => await vm.RunAsync();
    }
}
