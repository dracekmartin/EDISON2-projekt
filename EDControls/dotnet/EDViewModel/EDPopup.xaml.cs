using System.Windows;

namespace EDControls.EDViewModel;

public partial class EDPopup : Window
{
    private EDPopup(string message, EDPopupButtons buttons)
    {
        InitializeComponent();
        MessageText.Text = message;

        switch (buttons)
        {
            case EDPopupButtons.AnoNe:
                PositiveBtn.Content = "Ano";
                NegativeBtn.Content = "Ne";
                break;
            case EDPopupButtons.Ok:
                PositiveBtn.Content = "Ok";
                NegativeBtn.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void PositiveBtn_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void NegativeBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    internal static bool Show(Window? owner, string message, EDPopupButtons buttons)
    {
        var popup = new EDPopup(message, buttons) { Owner = owner };
        return popup.ShowDialog() == true;
    }
}
