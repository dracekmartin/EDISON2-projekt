using System.IO;
using System.Text;
using System.Windows;
using EDControls.ErrorReporting;

namespace EDControls.EDViewModel;

internal partial class EDErrorPopup : Window
{
    private readonly string? _refId;

    private EDErrorPopup(Exception ex, string? refId)
    {
        InitializeComponent();
        // CenterOwner mine maximalizovaný shell (restore bounds) - centrujeme ručně po layoutu.
        Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);
        MessageText.Text = HeadlineFor(ex);
        ErrorText.Text = ex.ToString();

        _refId = refId;
        if (refId is not null)
        {
            RefIdText.Text = refId;
            RefIdPanel.Visibility = Visibility.Visible;
            FollowUpPanel.Visibility = Visibility.Visible;
            SendFollowUpBtn.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Hláška nahoře: zpráva nejvnitřnější výjimky (obalové TargetInvocation/Aggregate nic neříkají),
    /// bez zprávy aspoň typ.</summary>
    internal static string HeadlineFor(Exception ex)
    {
        var root = ex.GetBaseException();
        return string.IsNullOrWhiteSpace(root.Message) ? root.GetType().Name : root.Message;
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void SendFollowUpBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_refId is null) return;

        var text = FollowUpText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        ErrorReports.ReportFollowUp(_refId, text, Owner);

        // Odesílá se na pozadí a výsledek se nezjišťuje - potvrzení je o tom, že jsme text
        // převzali. Druhé kliknutí by poslalo duplicitu, proto se ovládání schová.
        FollowUpText.IsEnabled = false;
        SendFollowUpBtn.Visibility = Visibility.Collapsed;
        FollowUpSentText.Visibility = Visibility.Visible;
    }

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
            var header = _refId is null ? "" : $"Referenční číslo: {_refId}{Environment.NewLine}{Environment.NewLine}";
            File.WriteAllText(dlg.FileName, header + ErrorText.Text, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            // jsme uvnitř error popupu - nevoláme PopupDisplayer (re-entrance), jen MessageBox
            MessageBox.Show(this, $"Soubor se nepodařilo uložit:\n{ex.Message}",
                "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static void Show(Window? owner, Exception ex, string? refId = null)
    {
        var popup = new EDErrorPopup(ex, refId) { Owner = owner };
        popup.ShowDialog();
    }
}
