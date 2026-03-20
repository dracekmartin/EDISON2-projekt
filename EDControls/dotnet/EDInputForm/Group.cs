using WpfVisibility = System.Windows.Visibility;

namespace EDControls.EDInputForm
{
    public class Group : Item
    {
        public string Key { get; set; } = "";
        public string GroupName { get; set; } = "";

        /// <summary>
        /// Computed from Visibility. Visible = expanded, Collapsed = collapsed.
        /// Setting this toggles Visibility between Visible and Collapsed.
        /// </summary>
        public bool IsExpanded
        {
            get => Visibility == EItemVisibility.Visible;
            set => Visibility = value ? EItemVisibility.Visible : EItemVisibility.Collapsed;
        }

        protected override void OnVisibilityChanged()
            => OnPropertyChanged(nameof(IsExpanded));

        /// <summary>
        /// Group header is visible when Visible or Collapsed.
        /// Hidden only for UserHidden / ProgrammerHidden.
        /// </summary>
        public override WpfVisibility EffectiveVisibility =>
            Visibility is EItemVisibility.Visible or EItemVisibility.Collapsed
                ? WpfVisibility.Visible
                : WpfVisibility.Collapsed;
    }
}
