using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ALWA.SYS;
using EDControls;
using EDControls.EDSupportPanel;
using EDISONMAIN2;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly MLRest _mlRest;
        private readonly string _initialPassword;
        private readonly bool _keepOpenOnSuccess;

        // Startup progress fáze - okno zůstává otevřené pod dobíhajícím bootstrapem, nesmí ho zavřít uživatel.
        private bool _inProgress;

        // WebView2 widgety novinek/wiki/školení - pre-login (bez session).
        private readonly List<IDisposable> _widgets = new();

        public LoginResult? Result { get; private set; }

        /// <summary>Úspěšný login v režimu <c>keepOpenOnSuccess</c> - caller ukončí čekací frame a pokračuje v bootstrapu.</summary>
        public event EventHandler? LoginSucceeded;

        /// <summary>Uživatel chce zapamatovat heslo (checkbox). Čte caller po úspěšném loginu.</summary>
        public bool RememberPassword => RememberBox.IsChecked == true;

        /// <summary>Zadané heslo - caller ho po úspěchu zašifruje k uložení.</summary>
        public string EnteredPassword => PasswordBox.Password;

        /// <summary>Předvyplněné (zapamatované) heslo bylo serverem odmítnuto - caller ho má zapomenout,
        /// ať se příště automaticky nevyplní znovu a login zbytečně nepadá.</summary>
        public bool RememberedPasswordRejected { get; private set; }

        public LoginWindow(MLRest mlRest, string? initialUsername = null, string? initialPassword = null,
                           bool showRemember = true, bool keepOpenOnSuccess = false)
        {
            _mlRest = mlRest;
            _initialPassword = initialPassword ?? "";
            _keepOpenOnSuccess = keepOpenOnSuccess;
            InitializeComponent();

            // Alt+F4 / X během progress fáze nesmí okno shodit; Application.Shutdown cancel ignoruje.
            Closing += (_, e) => e.Cancel = _inProgress;

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
            }
            switch (LoginWindowLogic.PickInitialFocus(initialUsername, initialPassword))
            {
                case LoginFocus.Username:
                    UsernameBox.Focus();
                    UsernameBox.SelectAll();
                    break;
                case LoginFocus.Password:
                    PasswordBox.Focus();
                    break;
            }

            // Novinky/Wiki jsou auth-free; Školení pre-login bez personalizace (prázdná identita).
            // Widgety jsou čistě dekorativní - jejich (ne)načtení nesmí blokovat login.
            AddWidget(NovinkyHost, WidgetUrls.Novinky, "Novinky z portálu podpory");
            AddWidget(WikiHost, WidgetUrls.Wiki, "Uživatelská nápověda a wiki");
            AddWidget(SkoleniHost, WidgetUrls.Trainings("", ""), "Nabídka školení");
            // FontZoomWebView drží statické eventy (Font/Theme) → bez dispose leak; Closed jde na OK i Cancel.
            Closed += (_, _) => { foreach (var w in _widgets) w.Dispose(); };
        }

        private void AddWidget(Border host, string url, string placeholder)
        {
            if (SupportWidgetHost.Fill(host, url, placeholder) is { } widget)
                _widgets.Add(widget);
        }

        private void MlineWeb_Click(object sender, RoutedEventArgs e) => OpenExternal(SupportActions.WebUrl);

        private void MlineSupport_Click(object sender, RoutedEventArgs e) => OpenExternal(SupportActions.PortalUrl);

        // Selhání prohlížeče nesmí shodit přihlašovací obrazovku - hlásí se do stejného pruhu jako login.
        private void OpenExternal(string url)
        {
            try
            {
                SupportActions.OpenUrl(url);
            }
            catch (Exception)
            {
                ErrorText.Text = "Nepodařilo se otevřít prohlížeč.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ErrorText.Visibility = Visibility.Collapsed;
                Result = UserAuthenticator.Login(new MLRestAdapter(_mlRest), UsernameBox.Text, PasswordBox.Password);
                if (_keepOpenOnSuccess)
                {
                    EnterProgressState();
                    LoginSucceeded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    DialogResult = true;
                }
            }
            catch (LoginException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;

                // Selhalo předvyplněné zapamatované heslo (změněné jinde) → vyčisti pole,
                // ať uživatel vidí, že neplatí, a rovnou napíše nové. Caller ho zapomene.
                if (LoginWindowLogic.ShouldForgetRememberedPassword(_initialPassword, PasswordBox.Password))
                {
                    RememberedPasswordRejected = true;
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
        }

        // IsCancel zavírá jen modální okno (ShowDialog) - startup režim běží přes Show, zavřít musíme sami.
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_keepOpenOnSuccess && !_inProgress)
                Close();
        }

        private void EnterProgressState()
        {
            _inProgress = true;
            UsernameBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            RememberBox.IsEnabled = false;
            OkBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            // Plně kvalifikované - UseWindowsForms dělá z Cursors ambiguitu s WinForms.
            Cursor = System.Windows.Input.Cursors.Wait;
            ShowProgress("Přihlašování…");
        }

        /// <summary>Aktualizuje progress text startupu a hned ho vykreslí - další bootstrap krok UI thread zablokuje.</summary>
        public void ShowProgress(string message)
        {
            ProgressText.Text = message;
            ProgressText.Visibility = Visibility.Visible;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }

        /// <summary>Zavření bootstrapperem po vykreslení shellu - jediná cesta ven z progress fáze.</summary>
        public void CloseAfterHandoff()
        {
            _inProgress = false;
            Close();
        }
    }
}
