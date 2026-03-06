using System.Windows;

namespace EDControls.EDViewModel;

public partial class EDConfirmCloseWindow : Window
{
    public EDConfirmCloseWindow() => InitializeComponent();

    private void OkBtn_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void StornoBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// Shows the confirmation dialog. Returns true if the user confirmed (Ano),
    /// false if they chose Ne or dismissed the dialog.
    /// </summary>
    internal static bool Show(Window? owner)
    {
        var dlg = new EDConfirmCloseWindow { Owner = owner };
        return dlg.ShowDialog() == true;
    }
}
