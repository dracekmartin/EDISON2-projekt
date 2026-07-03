using System.Windows;
using EDControls;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly IMLRest _mlRest;
        private readonly string _username;

        public ChangePasswordWindow(IMLRest mlRest, string username)
        {
            _mlRest = mlRest;
            _username = username;
            InitializeComponent();
            HeaderText.Text = $"Změna hesla pro uživatele „{username}“.";
            OldPasswordBox.Focus();
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            // Shodu nového hesla kontroluje klient — server dostává jen jedno nové heslo.
            if (NewPasswordBox.Password.Length == 0) { ShowError("Zadejte nové heslo."); return; }
            if (NewPasswordBox.Password != ConfirmPasswordBox.Password) { ShowError("Nová hesla se neshodují."); return; }

            var result = UserAuthenticator.ChangePassword(_mlRest, _username, OldPasswordBox.Password, NewPasswordBox.Password);
            if (result.Success) { DialogResult = true; return; }
            ShowError(result.Message);
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
