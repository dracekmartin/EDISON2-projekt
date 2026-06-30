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

        public LoginWindow(MLRest mlRest, string? initialUsername = null)
        {
            _mlRest = mlRest;
            InitializeComponent();

            if (!string.IsNullOrEmpty(initialUsername))
            {
                UsernameBox.Text = initialUsername;
                PasswordBox.Focus();   // jméno už je vyplněné → rovnou na heslo
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
