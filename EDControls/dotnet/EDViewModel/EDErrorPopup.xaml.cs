using System.Windows;

namespace EDControls.EDViewModel;

internal partial class EDErrorPopup : Window
{
    private EDErrorPopup(Exception ex)
    {
        InitializeComponent();
        ErrorText.Text = ex.ToString();
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e) => Close();

    internal static void Show(Window? owner, Exception ex)
    {
        var popup = new EDErrorPopup(ex) { Owner = owner };
        popup.ShowDialog();
    }
}
