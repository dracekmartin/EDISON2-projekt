using System.Windows;
using EDControls.EDSupportPanel;
using EDISONMAIN2.SupportStrip;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        // Přechodně vypnuto: přihlášení zabere celé okno místo pravého sloupce s widgetem a WebView2 nevznikne.
        private const bool SupportWidgetEnabled = false;

        // Widget podpory (WebView2) - pre-login, bez session.
        private IDisposable? _supportWidget;

        internal LoginWindow(LoginViewModel vm)
        {
            DataContext = _vm = vm;
            InitializeComponent();

            // Alt+F4 / X během progress fáze nesmí okno shodit; Application.Shutdown cancel ignoruje.
            Closing += (_, e) => e.Cancel = _vm.IsInProgress;
            // DialogResult jen pro modální reauth; startup okno běží přes Show a zavírá se Close.
            _vm.RequestClose += result =>
            {
                if (result is { } confirmed) DialogResult = confirmed;
                else Close();
            };
            _vm.PropertyChanged += (_, e) =>
            {
                // Plně kvalifikované - UseWindowsForms dělá z Cursors ambiguitu s WinForms.
                if (e.PropertyName == nameof(LoginViewModel.IsInProgress))
                    Cursor = _vm.IsInProgress ? System.Windows.Input.Cursors.Wait : null;
            };

            PasswordBox.Password = vm.InitialPassword;
            switch (vm.InitialFocus)
            {
                case LoginFocus.Username:
                    UsernameBox.Focus();
                    UsernameBox.SelectAll();
                    break;
                case LoginFocus.Password:
                    PasswordBox.Focus();
                    break;
            }

            if (!SupportWidgetEnabled)
            {
                SupportPanel.Visibility = Visibility.Collapsed;
                System.Windows.Controls.Grid.SetColumnSpan(LoginCard, 2);
                LoginCard.BorderThickness = new Thickness(0);
                return;
            }

            // Týž widget podpory jako fixní pruh shellu; pre-login nese jen IdFa - uživatel
            // ještě není ověřený. Dekorace: jeho (ne)načtení nesmí blokovat login.
            _supportWidget = SupportWidgetHost.Fill(SupportHost,
                WidgetUrls.Edison2(vm.Idfa, "", ""),
                "Informační panel podpory - novinky a upozornění IT pro uživatele");
            // FontZoomWebView drží statické eventy (Font/Theme) → bez dispose leak; Closed jde na OK i Cancel.
            Closed += (_, _) => _supportWidget?.Dispose();
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
                _vm.ShowError("Nepodařilo se otevřít prohlížeč.");
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Login(PasswordBox.Password) != LoginAttempt.RememberedPasswordRejected) return;
            // Zapamatované heslo neplatí (změněné jinde) - vyčistit pole, ať uživatel rovnou napíše nové.
            PasswordBox.Clear();
            PasswordBox.Focus();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) => _vm.Cancel();
    }
}
