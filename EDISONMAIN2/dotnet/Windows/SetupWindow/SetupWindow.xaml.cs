using System;
using System.Collections.Generic;
using System.Windows;
// UseWindowsForms zapnuté → System.Drawing.Brushes koliduje s WPF.
using Brushes = System.Windows.Media.Brushes;

namespace EDISONMAIN2.Windows.SetupWindow
{
    /// <summary>
    /// First-launch setup: parita polí s legacy frmSetup - připojení (název, server, oba
    /// porty, IRIS), po testu připojení databáze (seznam ze serveru) a IDFa firmy; zápis
    /// <c>EDISONini.xml</c>. Běží před loginem, žádná session ještě neexistuje - proto
    /// code-behind a spojení před loginem z napsaných polí (<see cref="PreLoginConnection"/>).
    /// Greenfield server-init (firma/licence/admin) je mimo scope - zůstává na legacy frmSetup.
    /// </summary>
    public partial class SetupWindow : Window
    {
        // Connection probe + seznam databází - defaulty reálné, testy si podstrčí vlastní.
        private readonly Func<string, int, bool> _probe;
        private readonly Func<string, int, IReadOnlyList<string>> _databaseLoader;

        public SetupWindow(Func<string, int, bool>? probe = null,
                           Func<string, int, IReadOnlyList<string>>? databaseLoader = null)
        {
            _probe = probe ?? PreLoginConnection.Probe;
            _databaseLoader = databaseLoader ?? PreLoginConnection.LoadDatabases;
            InitializeComponent();

            var server = (SettingsLoader.Current ?? new SettingsLoader()).Server;
            NameBox.Text = server.Name;
            ServerBox.Text = server.Host;
            SuperPortBox.Text = server.SuperPort > 0 ? server.SuperPort.ToString() : "";
            RestPortBox.Text = server.Port.ToString();
            IrisBox.IsChecked = server.Iris;
            if (server.Database.Length > 0) DatabaseCombo.Items.Add(server.Database);
            DatabaseCombo.SelectedIndex = DatabaseCombo.Items.Count > 0 ? 0 : -1;
            IdfaBox.Text = server.IdFa;
        }

        // Změna serveru/REST portu po úspěšném testu → pole závislá na spojení zamknout,
        // dokud se znovu neotestuje (seznam databází patří k jinému serveru).
        private void ConnectionInput_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            SaveBtn.IsEnabled = false;
            DatabaseCombo.IsEnabled = false;
            IdfaBox.IsEnabled = false;
            StatusText.Visibility = Visibility.Collapsed;
        }

        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadConnection(out var server, out _, out var restPort, lockSave: true)) return;

            bool ok = _probe(server, restPort);
            if (ok)
            {
                try
                {
                    var selected = DatabaseCombo.SelectedItem as string;
                    var databases = _databaseLoader(server, restPort);
                    DatabaseCombo.Items.Clear();
                    foreach (var database in databases)
                        DatabaseCombo.Items.Add(database);
                    DatabaseCombo.SelectedIndex = SetupWizardLogic.PickDatabaseIndex(databases, selected);
                }
                catch (Exception ex)
                {
                    DatabaseCombo.IsEnabled = false;
                    IdfaBox.IsEnabled = false;
                    Warn($"Seznam databází nelze načíst ({ex.Message}).");
                    return;
                }
            }

            SaveBtn.IsEnabled = ok;
            DatabaseCombo.IsEnabled = ok;
            IdfaBox.IsEnabled = ok;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = ok ? "Připojení v pořádku." : "Nelze se připojit, zkontrolujte parametry.";
            StatusText.Foreground = ok ? ThemeBrush("EDTheme.Semantic.Success", Brushes.Green)
                                       : ThemeBrush("EDTheme.Semantic.Danger", Brushes.Red);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validace nezamyká OK - připojení je otestované, uživatel jen doplní pole.
            if (!TryReadConnection(out var server, out var superPort, out var restPort, lockSave: false)) return;
            string database = DatabaseCombo.SelectedItem as string ?? "";
            if (SetupWizardLogic.ValidateSave(NameBox.Text, database, IdfaBox.Text) is { } saveError)
            {
                ShowError(saveError);
                return;
            }

            var settings = new SettingsLoader
            {
                Server = new ServerSettings
                {
                    Name      = NameBox.Text.Trim(),
                    Host      = server,
                    SuperPort = superPort,
                    Port      = restPort,
                    Iris      = IrisBox.IsChecked == true,
                    Database  = database,
                    IdFa      = IdfaBox.Text.Trim(),
                }
            };
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

        private bool TryReadConnection(out string server, out int superPort, out int restPort, bool lockSave)
        {
            server = ServerBox.Text.Trim();
            var error = SetupWizardLogic.ValidateConnection(
                server, SuperPortBox.Text.Trim(), RestPortBox.Text.Trim(), out superPort, out restPort);

            if (error is null) return true;
            if (lockSave) Warn(error); else ShowError(error);
            return false;
        }

        // Vadné připojení → zákaz uložení, dokud se znovu neotestuje.
        private void Warn(string message)
        {
            SaveBtn.IsEnabled = false;
            ShowError(message);
        }

        private void ShowError(string message)
        {
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
