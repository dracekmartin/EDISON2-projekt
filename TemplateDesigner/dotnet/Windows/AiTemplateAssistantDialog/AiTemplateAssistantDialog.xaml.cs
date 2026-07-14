using System.Windows;

namespace TemplateDesigner;

public partial class AiTemplateAssistantDialog : Window
{
    public AiTemplateAssistantDialog(AiTemplateAssistantDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        ApiKeyBox.Password = vm.ApiKey;   // prefill z appsettings (Password není bindovatelný)
        vm.RequestClose += result => { DialogResult = result; Close(); };
    }

    // PasswordBox.Password není DependencyProperty → ruční sync do VM
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AiTemplateAssistantDialogViewModel vm)
            vm.ApiKey = ApiKeyBox.Password;
    }
}
