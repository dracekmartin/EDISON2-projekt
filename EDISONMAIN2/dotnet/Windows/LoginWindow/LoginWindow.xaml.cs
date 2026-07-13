using System.Windows;
using ALWA.SYS;
using EDControls;
using EDISONMAIN2;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly MLRest _mlRest;

        public LoginResult? Result { get; private set; }

        /// <summary>Uživatel chce zapamatovat heslo (checkbox). Čte caller po úspěšném loginu.</summary>
        public bool RememberPassword => RememberBox.IsChecked == true;

        /// <summary>Zadané heslo — caller ho po úspěchu zašifruje k uložení.</summary>
        public string EnteredPassword => PasswordBox.Password;

        public LoginWindow(MLRest mlRest, string? initialUsername = null, string? initialPassword = null, bool showRemember = true)
        {
            _mlRest = mlRest;
            InitializeComponent();

            // Reauth (přihlásit jiného uživatele) session nepersistuje → checkbox skrytý (byl by mrtvý).
            if (!showRemember)
                RememberBox.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(initialUsername))
                UsernameBox.Text = initialUsername;

            if (!string.IsNullOrEmpty(initialPassword))
            {
                // Předvyplněné heslo → checkbox zaškrtnutý (uložení se má obnovit při dalším loginu).
                PasswordBox.Password = initialPassword;
                RememberBox.IsChecked = true;
                // Focus (a select) na jméno, ať přihlášení může snadno převzít jiný uživatel.
                UsernameBox.Focus();
                UsernameBox.SelectAll();
            }
            else if (!string.IsNullOrEmpty(initialUsername))
            {
                PasswordBox.Focus();   // jen jméno je vyplněné → rovnou na heslo
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Visibility = Visibility.Collapsed;
                Result = UserAuthenticator.Login(new MLRestAdapter(_mlRest), UsernameBox.Text, PasswordBox.Password);
                DialogResult = true;
            }
            catch (LoginException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
