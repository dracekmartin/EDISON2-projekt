using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfVisibility = System.Windows.Visibility;

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
        public virtual WpfVisibility EffectiveVisibility =>
            _visibility == EItemVisibility.Visible
                ? WpfVisibility.Visible
                : WpfVisibility.Collapsed;

        /// <summary>Called after Visibility changes. Override to notify derived properties.</summary>
        protected virtual void OnVisibilityChanged() { }

        private string? _helpText;
        /// <summary>
        /// Optional help text set by Caché. Displayed as a tooltip or in a dedicated column
        /// depending on the form-level HelpTextMode setting.
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

        /// <summary>Visible when HelpText is non-empty; Collapsed otherwise.</summary>
        public WpfVisibility HelpTextVisibility =>
            string.IsNullOrEmpty(_helpText) ? WpfVisibility.Collapsed : WpfVisibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
