using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDControls.EDInputForm
{
    public abstract class Item : INotifyPropertyChanged
    {
        /// <summary>
        /// 1-based sequential number assigned by Caché across all items (groups and fields).
        /// </summary>
        public int Number { get; set; }

        private EItemVisibility _visibility = EItemVisibility.Visible;
        /// <summary>
        /// Controls whether this item is shown, collapsed, or hidden (by user or programmer).
        /// </summary>
        public EItemVisibility Visibility
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

        /// <summary>
        /// Computed WPF Visibility for XAML binding. Override in subclasses for additional logic.
        /// </summary>
        public virtual System.Windows.Visibility EffectiveVisibility =>
            _visibility == EItemVisibility.Visible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        /// <summary>Called after Visibility changes. Override to notify derived properties.</summary>
        protected virtual void OnVisibilityChanged() { }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
