using System.Windows;

namespace EDControls.EDViewModel;

public partial class EDPopup : Window
{
    /// <summary>Result kódovaný v DialogResult bool? → třístav pro AnoNeStorno.</summary>
    private bool? _result;

    private EDPopup(string message, EDPopupButtons buttons, string? title, bool showLogo = false)
    {
        InitializeComponent();
        // CenterOwner mine maximalizovaný shell (restore bounds) - centrujeme ručně po layoutu.
        Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);
        MessageText.Text = message;
        if (showLogo) LogoImage.Visibility = Visibility.Visible;

        switch (buttons)
        {
            case EDPopupButtons.Ok:
                PositiveBtn.Content = Str("EDStrings.Ok", "OK");
                NegativeBtn.Visibility = Visibility.Collapsed;
                Title = title ?? Str("EDStrings.Information", "Informace");
                break;
            case EDPopupButtons.AnoNe:
                PositiveBtn.Content = Str("EDStrings.Yes", "Ano");
                NegativeBtn.Content = Str("EDStrings.No", "Ne");
                NegativeBtn.IsCancel = true;        // Esc = Ne
                Title = title ?? Str("EDStrings.Confirmation", "Potvrzení");
                break;
            case EDPopupButtons.AnoNeStorno:
                PositiveBtn.Content = Str("EDStrings.Yes", "Ano");
                NegativeBtn.Content = Str("EDStrings.No", "Ne");
                CancelBtn.Content   = Str("EDStrings.Cancel", "Storno");
                CancelBtn.Visibility = Visibility.Visible;
                Title = title ?? Str("EDStrings.Confirmation", "Potvrzení");
                // Esc / X v title baru = Storno (default DialogResult=null pokud nezavřeno tlačítkem)
                break;
        }
    }

    // Sdílený string z EDStrings; fallback když slovník není mergnutý (EDControls jako knihovna).
    private static string Str(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    private void PositiveBtn_Click(object sender, RoutedEventArgs e) { _result = true;  DialogResult = true; }
    private void NegativeBtn_Click(object sender, RoutedEventArgs e) { _result = false; DialogResult = false; }
    private void CancelBtn_Click  (object sender, RoutedEventArgs e) { _result = null;  Close(); }

    /// <summary>2-button popup (Ok, AnoNe) - vrátí true/false. Title volitelný (default "Informace"/"Potvrzení"), showLogo = logo EDISON vlevo.</summary>
    internal static bool Show(Window? owner, string message, EDPopupButtons buttons, string? title = null, bool showLogo = false)
    {
        if (!PopupGuard.CanOpen()) return false;
        var popup = new EDPopup(message, buttons, title, showLogo);
        PopupGuard.SetOwner(popup, owner);
        return popup.ShowDialog() == true;
    }

    /// <summary>3-button popup (AnoNeStorno) - vrátí true (Ano) / false (Ne) / null (Storno nebo Esc). Title volitelný,
    /// <paramref name="first"/> a <paramref name="second"/> nahradí popisky Ano a Ne akcemi („Přepsat").</summary>
    internal static bool? ShowYesNoCancel(Window? owner, string message, string? title = null, string? first = null, string? second = null)
    {
        if (!PopupGuard.CanOpen()) return null;
        var popup = new EDPopup(message, EDPopupButtons.AnoNeStorno, title);
        if (first is not null) popup.PositiveBtn.Content = first;
        if (second is not null) popup.NegativeBtn.Content = second;
        PopupGuard.SetOwner(popup, owner);
        popup.ShowDialog();
        return popup._result;
    }
}
