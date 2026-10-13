using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.SetupWindow
{
    /// <summary>
    /// Setup wizard připojení (parita polí s legacy frmSetup). Uložit jde jen otestované připojení: změna serveru
    /// nebo REST portu uložení zamkne, dokud test neproběhne znovu, protože seznam databází patří k jinému serveru.
    /// </summary>
    internal sealed class SetupWizardViewModel : EDViewModel
    {
        private readonly Func<string, int, bool> _probe;
        private readonly Func<string, int, IReadOnlyList<string>> _databaseLoader;
        private readonly Action<ServerSettings> _save;
        private string _name;
        private string _server;
        private string _superPort;
        private string _restPort;
        private bool _iris;
        private string? _selectedDatabase;
        private string _idfa;
        private bool _isConnectionTested;
        private bool _canSave;
        private string _statusText = "";
        private bool _statusIsError;

        /// <param name="initial">Předvyplnění z <c>EDISONini.xml</c> (<see cref="SettingsLoader.Load"/>).</param>
        /// <param name="probe">Test připojení na server a REST port.</param>
        /// <param name="databaseLoader">Seznam databází serveru; volá se jen po úspěšném testu.</param>
        /// <param name="save">Zápis nastavení; výjimka se ukáže ve stavu okna.</param>
        public SetupWizardViewModel(EDServices services, ServerSettings initial, Func<string, int, bool> probe,
            Func<string, int, IReadOnlyList<string>> databaseLoader, Action<ServerSettings> save)
            : base(services)
        {
            _probe = probe;
            _databaseLoader = databaseLoader;
            _save = save;
            _name = initial.Name;
            _server = initial.Host;
            _superPort = initial.SuperPort > 0 ? initial.SuperPort.ToString() : "";
            _restPort = initial.Port.ToString();
            _iris = initial.Iris;
            if (initial.Database.Length > 0) Databases.Add(initial.Database);
            _selectedDatabase = Databases.Count > 0 ? Databases[0] : null;
            _idfa = initial.IdFa;
            TestCommand = Command(TestConnection);
            SaveCommand = Command(SaveSettings);
        }

        // Před loginem není session, do které by šla geometrie okna uložit.
        protected override string? PersistedFormId => null;

        public string Name { get => _name; set => Set(ref _name, value); }

        public string Server
        {
            get => _server;
            set { if (Set(ref _server, value)) LockConnection(); }
        }

        /// <summary>Superserver port; shell ho nepoužívá, test připojení jeho změna nezamyká.</summary>
        public string SuperPort { get => _superPort; set => Set(ref _superPort, value); }

        public string RestPort
        {
            get => _restPort;
            set { if (Set(ref _restPort, value)) LockConnection(); }
        }

        public bool Iris { get => _iris; set => Set(ref _iris, value); }

        public ObservableCollection<string> Databases { get; } = [];

        public string? SelectedDatabase { get => _selectedDatabase; set => Set(ref _selectedDatabase, value); }

        public string Idfa { get => _idfa; set => Set(ref _idfa, value); }

        /// <summary>Databáze a IDFa jdou vybrat až nad otestovaným připojením.</summary>
        public bool IsConnectionTested { get => _isConnectionTested; private set => Set(ref _isConnectionTested, value); }

        public bool CanSave { get => _canSave; private set => Set(ref _canSave, value); }

        public string StatusText
        {
            get => _statusText;
            private set { if (Set(ref _statusText, value)) OnPropertyChanged(nameof(HasStatus)); }
        }

        public bool HasStatus => StatusText.Length > 0;

        public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }

        public ICommand TestCommand { get; }

        public ICommand SaveCommand { get; }

        private void LockConnection()
        {
            CanSave = false;
            IsConnectionTested = false;
            StatusText = "";
        }

        private void TestConnection()
        {
            if (!TryReadConnection(lockSave: true, out var server, out _, out var restPort)) return;

            bool ok = _probe(server, restPort);
            if (ok)
            {
                try
                {
                    var selected = SelectedDatabase;
                    var databases = _databaseLoader(server, restPort);
                    Databases.Clear();
                    foreach (var database in databases)
                        Databases.Add(database);
                    int index = SetupWizardLogic.PickDatabaseIndex(databases, selected);
                    SelectedDatabase = index >= 0 ? databases[index] : null;
                }
                catch (Exception ex)
                {
                    IsConnectionTested = false;
                    Warn($"Seznam databází nelze načíst ({ex.Message}).");
                    return;
                }
            }

            CanSave = ok;
            IsConnectionTested = ok;
            ShowStatus(ok ? "Připojení v pořádku." : "Nelze se připojit, zkontrolujte parametry.", isError: !ok);
        }

        private void SaveSettings()
        {
            if (!CanSave) return;
            // Validace nezamyká OK - připojení je otestované, uživatel jen doplní pole.
            if (!TryReadConnection(lockSave: false, out var server, out var superPort, out var restPort)) return;
            var database = SelectedDatabase ?? "";
            if (SetupWizardLogic.ValidateSave(Name, database, Idfa) is { } saveError)
            {
                ShowStatus(saveError, isError: true);
                return;
            }

            try
            {
                _save(new ServerSettings
                {
                    Name      = Name.Trim(),
                    Host      = server,
                    SuperPort = superPort,
                    Port      = restPort,
                    Iris      = Iris,
                    Database  = database,
                    IdFa      = Idfa.Trim(),
                });
            }
            catch (Exception ex)
            {
                ShowStatus($"Nastavení nelze uložit ({ex.Message}).", isError: true);
                return;
            }
            CloseDialog(true);
        }

        private bool TryReadConnection(bool lockSave, out string server, out int superPort, out int restPort)
        {
            server = Server.Trim();
            var error = SetupWizardLogic.ValidateConnection(server, SuperPort.Trim(), RestPort.Trim(), out superPort, out restPort);
            if (error is null) return true;
            if (lockSave) Warn(error); else ShowStatus(error, isError: true);
            return false;
        }

        // Vadné připojení → zákaz uložení, dokud se znovu neotestuje.
        private void Warn(string message)
        {
            CanSave = false;
            ShowStatus(message, isError: true);
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusIsError = isError;
            StatusText = message;
        }
    }
}
