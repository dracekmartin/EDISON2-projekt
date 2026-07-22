using System;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
// UseWindowsForms zapnuté → System.Drawing.Brushes koliduje s WPF.
using Brushes = System.Windows.Media.Brushes;

namespace EDISONMAIN2.Windows.SetupWindow
{
    /// <summary>
    /// First-launch setup (jen připojení): zadání serveru + REST portu, test
    /// připojení a zápis <c>appsettings.json</c>. Běží před loginem, žádná session ještě
    /// neexistuje — proto code-behind a vlastní pre-login <see cref="MLRest"/> z napsaných polí
    /// (mirror <see cref="LoginWindow.LoginWindow"/>). Greenfield server-init (firma/licence/admin)
    /// je mimo scope — zůstává na legacy frmSetup.
    /// </summary>
    public partial class SetupWindow : Window
    {
        // Connection probe — default reálný GetCompanyList; testy by si mohly podstrčit vlastní.
        private readonly Func<string, int, bool> _probe;

        public SetupWindow(Func<string, int, bool>? probe = null)
        {
            _probe = probe ?? DefaultProbe;
            InitializeComponent();

            var config = SettingsLoader.Current ?? new SettingsLoader();
            ServerBox.Text = config.Server.Host;
            PortBox.Text = config.Server.Port.ToString();
        }

        /// <summary>Připojení projde, když server bez chyby vrátí seznam firem.</summary>
        private static bool DefaultProbe(string server, int port)
        {
            try
            {
                var rest = new MLRest(server, port, "", "");
                string errState = "";
                Database.GetCompanyList(ref errState, rest, false);
                return string.IsNullOrEmpty(errState);
            }
            catch
            {
                return false;
            }
        }

        // Změna serveru/portu po úspěšném testu → znovu otestovat před uložením.
        private void Input_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            SaveBtn.IsEnabled = false;
            StatusText.Visibility = Visibility.Collapsed;
        }

        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadInput(out var server, out var port)) return;

            bool ok = _probe(server, port);
            SaveBtn.IsEnabled = ok;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = ok ? "Připojení v pořádku." : "Nelze se připojit, zkontrolujte parametry.";
            StatusText.Foreground = ok ? ThemeBrush("EDTheme.Semantic.Success", Brushes.Green)
                                       : ThemeBrush("EDTheme.Semantic.Danger", Brushes.Red);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadInput(out var server, out var port)) return;

            var settings = new SettingsLoader { Server = new ServerSettings { Host = server, Port = port } };
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                StatusText.Visibility = Visibility.Visible;
                StatusText.Foreground = ThemeBrush("EDTheme.Semantic.Danger", Brushes.Red);
                StatusText.Text = $"Nastavení nelze uložit ({ex.Message}).";
                return;
            }
            DialogResult = true;
        }

        private bool TryReadInput(out string server, out int port)
        {
            server = ServerBox.Text.Trim();
            port = 0;
            if (server.Length == 0)
            {
                Warn("Zadejte server.");
                return false;
            }
            if (!int.TryParse(PortBox.Text.Trim(), out port) || port <= 0)
            {
                Warn("Zadejte platný REST port.");
                return false;
            }
            return true;
        }

        private void Warn(string message)
        {
            // Změna vstupu po úspěšném testu → zákaz uložení, dokud se znovu neotestuje.
            SaveBtn.IsEnabled = false;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Foreground = ThemeBrush("EDTheme.Semantic.Danger", Brushes.Red);
            StatusText.Text = message;
        }

        // Theme brush z EDTheme (DynamicResource ekvivalent v code-behind); fallback když
        // slovník ještě není mergnutý (setup běží před loginem).
        private System.Windows.Media.Brush ThemeBrush(string key, System.Windows.Media.Brush fallback)
            => TryFindResource(key) as System.Windows.Media.Brush ?? fallback;
    }
}
