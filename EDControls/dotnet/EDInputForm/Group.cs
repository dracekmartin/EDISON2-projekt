using WpfVisibility = System.Windows.Visibility;

namespace EDControls.EDInputForm
{
    public class Group : Item
    {
        public string Key { get; set; } = "";
        public string GroupName { get; set; } = "";

        /// <summary>
        /// Odvozeno z <see cref="Item.Visibility"/>: Visible = expanded, Collapsed = collapsed.
        /// Set přepne <see cref="Item.Visibility"/> mezi Visible a Collapsed.
        /// </summary>
        public bool IsExpanded
        {
            get => Visibility == EItemVisibility.Visible;
            set => Visibility = value ? EItemVisibility.Visible : EItemVisibility.Collapsed;
        }

        protected override void OnVisibilityChanged()
            => OnPropertyChanged(nameof(IsExpanded));

        /// <summary>
        /// Header skupiny je viditelný pro Visible i Collapsed (zachová možnost rozbalení).
        /// Skrytý pro UserHidden / ProgrammerHidden nebo když skupina neprošla search filtrem.
        /// </summary>
        public override WpfVisibility EffectiveVisibility =>
            !IsFilteredBySearch && Visibility is EItemVisibility.Visible or EItemVisibility.Collapsed
                ? WpfVisibility.Visible
                : WpfVisibility.Collapsed;
    }
}
