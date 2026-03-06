using System.Windows;

namespace EDControls.ConfirmCloseDialog;

public partial class ConfirmCloseWindow : Window
{
    public ConfirmCloseWindow() => InitializeComponent();

    private void OkBtn_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void StornoBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// Shows the confirmation dialog. Returns true if the user confirmed (Ok),
    /// false if they chose Storno or dismissed the dialog.
    /// </summary>
    internal static bool Show(Window? owner)
    {
        var dlg = new ConfirmCloseWindow { Owner = owner };
        return dlg.ShowDialog() == true;
    }
}
