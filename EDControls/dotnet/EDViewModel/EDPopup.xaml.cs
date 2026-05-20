using System.Windows;

namespace EDControls.EDViewModel;

public partial class EDPopup : Window
{
    /// <summary>Result kódovaný v DialogResult bool? → třístav pro AnoNeStorno.</summary>
    private bool? _result;

    private EDPopup(string message, EDPopupButtons buttons, string? title)
    {
        InitializeComponent();
        MessageText.Text = message;

        switch (buttons)
        {
            case EDPopupButtons.Ok:
                PositiveBtn.Content = "Ok";
                NegativeBtn.Visibility = Visibility.Collapsed;
                Title = title ?? "Informace";
                break;
            case EDPopupButtons.AnoNe:
                PositiveBtn.Content = "Ano";
                NegativeBtn.Content = "Ne";
                NegativeBtn.IsCancel = true;        // Esc = Ne
                Title = title ?? "Potvrzení";
                break;
            case EDPopupButtons.AnoNeStorno:
                PositiveBtn.Content = "Ano";
                NegativeBtn.Content = "Ne";
                CancelBtn.Content   = "Storno";
                CancelBtn.Visibility = Visibility.Visible;
                Title = title ?? "Potvrzení";
                // Esc / X v title baru = Storno (default DialogResult=null pokud nezavřeno tlačítkem)
                break;
        }
    }

    private void PositiveBtn_Click(object sender, RoutedEventArgs e) { _result = true;  DialogResult = true; }
    private void NegativeBtn_Click(object sender, RoutedEventArgs e) { _result = false; DialogResult = false; }
    private void CancelBtn_Click  (object sender, RoutedEventArgs e) { _result = null;  Close(); }

    /// <summary>2-button popup (Ok, AnoNe) — vrátí true/false. Title volitelný (default "Informace"/"Potvrzení").</summary>
    internal static bool Show(Window? owner, string message, EDPopupButtons buttons, string? title = null)
    {
        var popup = new EDPopup(message, buttons, title) { Owner = owner };
        return popup.ShowDialog() == true;
    }

    /// <summary>3-button popup (AnoNeStorno) — vrátí true (Ano) / false (Ne) / null (Storno nebo Esc). Title volitelný.</summary>
    internal static bool? ShowYesNoCancel(Window? owner, string message, string? title = null)
    {
        var popup = new EDPopup(message, EDPopupButtons.AnoNeStorno, title) { Owner = owner };
        popup.ShowDialog();
        return popup._result;
    }
}
