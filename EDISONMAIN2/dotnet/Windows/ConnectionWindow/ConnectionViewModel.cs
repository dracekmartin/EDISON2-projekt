using System.Collections.ObjectModel;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.ConnectionWindow
{
    /// <summary>Řádek seznamu: výchozí připojení z <c>EDISONini.xml</c> (jen pro čtení) nebo uložené z LocalPrefs.</summary>
    public sealed record ConnectionChoice(string Name, string Server, int RestPort, bool IsDefault)
    {
        public string Label => IsDefault ? $"{Name} (výchozí)" : Name;
        public string Address => $"{Server}:{RestPort}";
    }

    /// <summary>
    /// Výběr připojení pro novou instanci shellu (legacy <c>frmConnection</c>). Výchozí připojení řídí
    /// centrálně <c>EDISONini.xml</c> a odebrat nejde; další si uživatel ukládá lokálně a změny seznamu
    /// se persistují hned, ne až při OK.
    /// </summary>
    public class ConnectionViewModel : EDViewModel
    {
        private readonly ILocalPrefs _prefs;
        private ConnectionChoice? _selected;
        private string _newName = "", _newServer = "", _newRestPort = "";
        private string? _validationMessage;

        public ConnectionViewModel(ILocalPrefs prefs, ConnectionChoice defaultConnection)
            : this(EDServices.Current, prefs, defaultConnection) { }

        /// <summary>DI/test overload - vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
        public ConnectionViewModel(EDServices services, ILocalPrefs prefs, ConnectionChoice defaultConnection)
            : base(services)
        {
            _prefs = prefs;
            Choices = [defaultConnection];
            foreach (var saved in prefs.Connections ?? [])
                Choices.Add(new ConnectionChoice(saved.Name, saved.Server, saved.RestPort, IsDefault: false));
            _selected = Choices[0];
        }

        public ObservableCollection<ConnectionChoice> Choices { get; }

        public ConnectionChoice? Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value)) OnPropertyChanged(nameof(CanRemove));
            }
        }

        public bool CanRemove => Selected is { IsDefault: false };

        public string NewName { get => _newName; set => Set(ref _newName, value); }
        public string NewServer { get => _newServer; set => Set(ref _newServer, value); }
        public string NewRestPort { get => _newRestPort; set => Set(ref _newRestPort, value); }

        public string? ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                if (Set(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage));
            }
        }

        public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

        /// <summary>Validace nového připojení; null = OK, jinak hláška pro uživatele.</summary>
        internal static string? ValidateNew(string name, string server, string restPortText, out int restPort)
        {
            restPort = 0;
            if (name.Trim().Length == 0) return "Zadejte název připojení.";
            if (server.Trim().Length == 0) return "Zadejte server.";
            if (!int.TryParse(restPortText.Trim(), out restPort) || restPort <= 0) return "Zadejte platný REST port.";
            return null;
        }

        protected override void OnAdd()
        {
            ValidationMessage = ValidateNew(NewName, NewServer, NewRestPort, out int restPort);
            if (ValidationMessage is not null) return;

            var added = new ConnectionChoice(NewName.Trim(), NewServer.Trim(), restPort, IsDefault: false);
            Choices.Add(added);
            Persist();
            Selected = added;
            NewName = NewServer = NewRestPort = "";
        }

        protected override void OnDelete()
        {
            if (Selected is not { IsDefault: false } removed) return;
            Choices.Remove(removed);
            Persist();
            Selected = Choices[0];
        }

        private void Persist()
        {
            _prefs.Connections = Choices.Where(c => !c.IsDefault)
                .Select(c => new SavedConnection { Name = c.Name, Server = c.Server, RestPort = c.RestPort })
                .ToList();
            _prefs.Save();
        }

        // Bez výběru se dialog nesmí zavřít jako potvrzený - volající by spouštěl null.
        protected override void OnOk()
        {
            if (Selected is null) return;
            base.OnOk();
        }

        // Okno se centruje na ownera, geometrie se nepersistuje (vzor MiniAppPickerWindow).
        protected override string? PersistedFormId => null;
    }
}
