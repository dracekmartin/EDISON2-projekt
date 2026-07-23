using System.IO;
using System.Text;
using System.Windows;

namespace EDControls.EDViewModel;

internal partial class EDErrorPopup : Window
{
    private EDErrorPopup(Exception ex)
    {
        InitializeComponent();
        // CenterOwner mine maximalizovaný shell (restore bounds) — centrujeme ručně po layoutu.
        Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);
        ErrorText.Text = ex.ToString();
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Uložit chybu do souboru",
            Filter = "Textové soubory (*.txt)|*.txt|Všechny soubory (*.*)|*.*",
            DefaultExt = "txt",
            FileName = $"EDISON2-chyba-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, ErrorText.Text, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // jsme uvnitř error popupu — nevoláme PopupDisplayer (re-entrance), jen MessageBox
            MessageBox.Show(this, $"Soubor se nepodařilo uložit:\n{ex.Message}",
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static void Show(Window? owner, Exception ex)
    {
        var popup = new EDErrorPopup(ex) { Owner = owner };
        popup.ShowDialog();
    }
}
