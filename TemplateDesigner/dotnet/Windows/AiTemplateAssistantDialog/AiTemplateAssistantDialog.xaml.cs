using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace TemplateDesigner;

public partial class AiTemplateAssistantDialog : Window
{
    public AiTemplateAssistantDialog(AiTemplateAssistantDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // Hyperlink otevírá URL v prohlížeči — WPF-specific event, musí být v code-behind
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
