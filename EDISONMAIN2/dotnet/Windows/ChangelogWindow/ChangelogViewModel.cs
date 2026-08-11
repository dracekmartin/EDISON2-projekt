using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    /// <summary>Stav okna novinek - přepínání změny/historie a snooze checkbox. Žádné IO.</summary>
    public class ChangelogViewModel : EDViewModel
    {
        private readonly ChangelogContent _content;

        public ChangelogViewModel(string moduleTitle, ChangelogContent content)
        {
            _content = content;
            ModuleTitle = $"Novinky - {moduleTitle}";
            _showChangesOnly = HasChanges;
        }

        public string ModuleTitle { get; }

        // Okno se vždy otvírá uprostřed shellu (CenterOwner) - pozici/velikost si záměrně nepamatuje.
        protected override string? PersistedFormId => null;

        public bool HasChanges => _content.ChangesText is { Length: > 0 };

        private bool _showChangesOnly;
        public bool ShowChangesOnly
        {
            get => _showChangesOnly;
            set
            {
                if (!Set(ref _showChangesOnly, value)) return;
                OnPropertyChanged(nameof(ShowFullHistory));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        // Druhý RadioButton potřebuje vlastní bindovatelnou property - negace v XAML nejde.
        public bool ShowFullHistory
        {
            get => !ShowChangesOnly;
            set => ShowChangesOnly = !value;
        }

        public string DisplayText => ShowChangesOnly && HasChanges ? _content.ChangesText! : _content.FullText;

        private bool _snoozeChecked;
        /// <summary>„Nezobrazovat do příští změny" - čte ho presenter po zavření okna.</summary>
        public bool SnoozeChecked
        {
            get => _snoozeChecked;
            set => Set(ref _snoozeChecked, value);
        }
    }
}
