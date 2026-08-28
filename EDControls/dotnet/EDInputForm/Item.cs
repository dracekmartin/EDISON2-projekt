using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfVisibility = System.Windows.Visibility;

namespace EDControls.EDInputForm
{
    public abstract class Item : INotifyPropertyChanged
    {
        /// <summary>1-based sekvenční číslo přidělené Caché napříč všemi položkami (groups + fields).</summary>
        public int Number { get; set; }

        private ItemVisibility _visibility = ItemVisibility.Visible;
        /// <summary>Řídí zobrazení položky: visible / collapsed / hidden (uživatel nebo programátor).</summary>
        public ItemVisibility Visibility
        {
            get => _visibility;
            set
            {
                _visibility = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveVisibility));
                OnVisibilityChanged();
            }
        }

        /// <summary>Vypočtená WPF <see cref="WpfVisibility"/> pro XAML binding. Override v subclass pro dodatečnou logiku.</summary>
        public virtual WpfVisibility EffectiveVisibility =>
            !_isFilteredBySearch && _visibility == ItemVisibility.Visible
                ? WpfVisibility.Visible
                : WpfVisibility.Collapsed;

        /// <summary>Volá se po změně <see cref="Visibility"/>. Override pro notifikaci odvozených properties.</summary>
        protected virtual void OnVisibilityChanged() { }

        private bool _isFilteredBySearch;
        /// <summary>
        /// True když položka neprošla aktuálním search filtrem (skryje ji v UI).
        /// Nastavuje EDInputForm při změně toolbar SearchText. Nezávislé na <see cref="Visibility"/>.
        /// </summary>
        internal bool IsFilteredBySearch
        {
            get => _isFilteredBySearch;
            set
            {
                if (_isFilteredBySearch == value) return;
                _isFilteredBySearch = value;
                OnPropertyChanged(nameof(EffectiveVisibility));
            }
        }

        private string? _helpText;
        /// <summary>
        /// Volitelný help text nastavený serverem. Zobrazí se jako tooltip nebo v samostatném sloupci
        /// podle form-level <c>HelpTextMode</c> nastavení.
        /// </summary>
        public string? HelpText
        {
            get => _helpText;
            set
            {
                _helpText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HelpTextVisibility));
            }
        }

        /// <summary>Visible pokud <see cref="HelpText"/> není prázdný, jinak Collapsed.</summary>
        public WpfVisibility HelpTextVisibility =>
            string.IsNullOrEmpty(_helpText) ? WpfVisibility.Collapsed : WpfVisibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
