using System.Windows;

namespace TemplateDesigner;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
