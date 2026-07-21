using System.Windows;
using System.Windows.Controls;
using ALWA.SYS;
using EDControls;
using EDISONMAIN2;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly MLRest _mlRest;
        private readonly string _initialPassword;

        // WebView2 widgety novinek/wiki/školení — pre-login (bez session).
        private readonly List<FontZoomWebView> _widgets = new();

        public LoginResult? Result { get; private set; }

        /// <summary>Uživatel chce zapamatovat heslo (checkbox). Čte caller po úspěšném loginu.</summary>
        public bool RememberPassword => RememberBox.IsChecked == true;

        /// <summary>Zadané heslo — caller ho po úspěchu zašifruje k uložení.</summary>
        public string EnteredPassword => PasswordBox.Password;

        /// <summary>Předvyplněné (zapamatované) heslo bylo serverem odmítnuto — caller ho má zapomenout,
        /// ať se příště automaticky nevyplní znovu a login zbytečně nepadá.</summary>
        public bool RememberedPasswordRejected { get; private set; }

        public LoginWindow(MLRest mlRest, string? initialUsername = null, string? initialPassword = null, bool showRemember = true)
        {
            _mlRest = mlRest;
            _initialPassword = initialPassword ?? "";
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

            // Novinky/Wiki jsou auth-free; Školení pre-login bez personalizace (prázdná identita).
            // Widgety jsou čistě dekorativní — jejich (ne)načtení nesmí blokovat login.
            AddWidget(NovinkyHost, WidgetUrls.Novinky);
            AddWidget(WikiHost, WidgetUrls.Wiki);
            AddWidget(SkoleniHost, WidgetUrls.Trainings("", ""));
            // FontZoomWebView drží statické eventy (Font/Theme) → bez dispose leak; Closed jde na OK i Cancel.
            Closed += (_, _) => { foreach (var w in _widgets) w.Dispose(); };
        }

        private void AddWidget(Border host, string url)
        {
            var wv = new FontZoomWebView();
            wv.Navigate(url);
            host.Child = wv;
            _widgets.Add(wv);
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

                // Selhalo předvyplněné zapamatované heslo (změněné jinde) → vyčisti pole,
                // ať uživatel vidí, že neplatí, a rovnou napíše nové. Caller ho zapomene.
                if (_initialPassword.Length > 0 && PasswordBox.Password == _initialPassword)
                {
                    RememberedPasswordRejected = true;
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
        }
    }
}
