using System.Windows;
using ALWA.SYS;
using EDISONMAIN2;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly MLRest _mlRest;

        public LoginResult? Result { get; private set; }

        public LoginWindow(MLRest mlRest)
        {
            _mlRest = mlRest;
            InitializeComponent();
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Visibility = Visibility.Collapsed;
                Result = UserAuthenticator.Login(_mlRest, UsernameBox.Text, PasswordBox.Password);
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
