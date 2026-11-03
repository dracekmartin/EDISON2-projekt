using System.Windows;
using System.Windows.Automation;
using EDControls;
using EDControls.EDSupportPanel;
using EDControls.EDTheme;

namespace EDISONMAIN2.Windows.LoginWindow
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

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
            // Esc jako dřív tlačítko Storno: start tím končí aplikaci, dialog vrací zrušení.
            // Během progress fáze zavření vetuje Closing.
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Escape) return;
                e.Handled = true;
                Close();
            };

            PasswordBox.Password = vm.InitialPassword;
            switch (vm.InitialFocus)
            {
                case LoginFocus.Username:
                    UsernameBox.Focus();
                    break;
                case LoginFocus.Password:
                    PasswordBox.Focus();
                    break;
                case LoginFocus.LoginButton:
                    OkBtn.Focus();
                    break;
            }

            if (vm.IsCompact)
            {
                UseDialogLayout();
                return;
            }

            UpdateThemeToggle();
            ThemeService.Applied += UpdateThemeToggle;
            // Statický event - bez odhlášení by zavřené okno zůstalo v paměti.
            Closed += (_, _) => ThemeService.Applied -= UpdateThemeToggle;
        }

        // Malý dialog uprostřed shellu: brandová plocha přes celou obrazovku by zakryla rozdělanou práci.
        private void UseDialogLayout()
        {
            BrandPanel.Visibility = Visibility.Collapsed;
            // Shell má přepínač motivu v menu Nastavení.
            ThemeToggle.Visibility = Visibility.Collapsed;
            WindowState = WindowState.Normal;
            ClearValue(WidthProperty);
            ClearValue(HeightProperty);
            MinWidth = 0;
            MinHeight = 0;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);
        }

        private void UpdateThemeToggle()
        {
            bool dark = ThemeService.Current == AppTheme.Dark;
            // Segoe MDL2: E706 slunce, E708 měsíc - ikona ukazuje režim, na který se přepne.
            ThemeGlyph.Text = dark ? "" : "";
            var label = dark ? "Světlý režim" : "Tmavý režim";
            ThemeToggle.ToolTip = label;
            AutomationProperties.SetName(ThemeToggle, label);
        }

        // Preference je per Windows uživatel a sdílená mezi procesy, session k ní není potřeba.
        private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
            ThemeService.SetDarkMode(ThemeService.Current != AppTheme.Dark);

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
    }
}
