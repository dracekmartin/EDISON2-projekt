using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EDControls.EDInputForm
{
    public class Group : Item, INotifyPropertyChanged
    {
        public string GroupName { get; set; } = "";

        private bool _isVisible = true;
        /// <summary>
        /// Server-controlled. When false, the entire group (header and all its fields) is hidden.
        /// Set from the Caché wire format on Init/Validate.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveVisibility)); }
        }

        private bool _isExpanded = true;
        /// <summary>
        /// User-controlled. When false, the group header stays visible but its fields are collapsed.
        /// Preserved across Init/Validate round-trips.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        /// <summary>Visibility of the group header. Collapsed only when IsVisible is false.</summary>
        public Visibility EffectiveVisibility =>
            IsVisible ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
