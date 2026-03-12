using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

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
        public Visibility ValidateIndicatorVisibility =>
#if DEBUG
            ValidateOnChange ? Visibility.Visible : Visibility.Collapsed;
#else
            Visibility.Collapsed;
#endif

        /// <summary>When true, displays a required-field asterisk next to the label.</summary>
        public bool Required { get; set; } = false;

        public Visibility RequiredIndicatorVisibility =>
            Required ? Visibility.Visible : Visibility.Collapsed;

        public EFieldType FieldType { get; set; } = EFieldType.Text;

        private string? _value;
        public string? Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
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

        /// <summary>
        /// The group this field belongs to. Set by EDInputForm after parsing.
        /// When null the field is treated as always visible.
        /// </summary>
        internal Group? ParentGroup
        {
            get => _parentGroup;
            set
            {
                if (_parentGroup != null) _parentGroup.PropertyChanged -= OnGroupPropertyChanged;
                _parentGroup = value;
                if (_parentGroup != null) _parentGroup.PropertyChanged += OnGroupPropertyChanged;
                OnPropertyChanged(nameof(EffectiveVisibility));
            }
        }
        private Group? _parentGroup;

        private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Group.IsVisible) or nameof(Group.IsExpanded))
                OnPropertyChanged(nameof(EffectiveVisibility));
        }

        /// <summary>
        /// Collapsed when the parent group is hidden (IsVisible=false) or collapsed by the user (IsExpanded=false).
        /// </summary>
        public Visibility EffectiveVisibility =>
            (_parentGroup?.IsVisible ?? true) && (_parentGroup?.IsExpanded ?? true)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
