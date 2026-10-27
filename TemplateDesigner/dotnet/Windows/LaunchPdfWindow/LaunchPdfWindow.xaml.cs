using System.Windows;

namespace TemplateDesigner;

public partial class LaunchPdfWindow : Window
{
    public LaunchPdfWindow(LaunchPdfWindowViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += _ => Close();
        // Okno nemá vlastníka: CenterScreen by ho dal pod kurzor, tohle na monitor, odkud TD spustili (pole [21]).
        Loaded += (_, _) => EDControls.OwnerWindowCentering.CenterOnOwner(this);
        Loaded += async (_, _) => await vm.RunAsync();
    }
}
