namespace EDControls.EDInputForm
{
    public enum ItemVisibility
    {
        Visible,
        Collapsed,
        /// <summary>Rezervováno: přenáší se a chová se jako skryté, ale žádné UI ho nenastavuje (uživatel formulář nekonfiguruje).</summary>
        UserHidden,
        ProgrammerHidden
    }

    public static class ItemVisibilityWire
    {
        private const string VisibleWire = "0";
        private const string CollapsedWire = "1";
        private const string UserHiddenWire = "2";
        private const string ProgrammerHiddenWire = "3";

        public static string ToWire(this ItemVisibility v) => v switch
        {
            ItemVisibility.Collapsed        => CollapsedWire,
            ItemVisibility.UserHidden       => UserHiddenWire,
            ItemVisibility.ProgrammerHidden => ProgrammerHiddenWire,
            _                                => VisibleWire
        };

        public static ItemVisibility FromWire(string wire) => wire switch
        {
            CollapsedWire        => ItemVisibility.Collapsed,
            UserHiddenWire       => ItemVisibility.UserHidden,
            ProgrammerHiddenWire => ItemVisibility.ProgrammerHidden,
            _                    => ItemVisibility.Visible
        };
    }
}
