using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDControls.EDInputForm
{
    public class Field : Item, INotifyPropertyChanged
    {
        /// <summary>Stable identifier used as the key when communicating with Caché.</summary>
        public string Key { get; set; } = "";

        public string Label { get; set; } = "";

        /// <summary>When true, changing Value automatically triggers validation of the whole form.</summary>
        public bool ValidateOnChange { get; set; } = false;

        /// <summary>
        /// Visible in DEBUG builds when ValidateOnChange is true; Collapsed otherwise.
        /// Used by InputForm to render a debug-mode indicator dot.
        /// </summary>
        public System.Windows.Visibility ValidateIndicatorVisibility =>
#if DEBUG
            ValidateOnChange ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
#else
            System.Windows.Visibility.Collapsed;
#endif

        /// <summary>When true, displays a required-field asterisk next to the label.</summary>
        public bool Required { get; set; } = false;

        public System.Windows.Visibility RequiredIndicatorVisibility =>
            Required ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public EFieldType FieldType { get; set; } = EFieldType.Text;

        private string? _value;
        public string? Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        private string? _validationError;
        public string? ValidationError
        {
            get => _validationError;
            set
            {
                _validationError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(_validationError);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
