namespace EDControls.EDInputForm
{
    public enum EItemVisibility
    {
        Visible,
        Collapsed,
        UserHidden,
        ProgrammerHidden
    }

    public static class EItemVisibilityWire
    {
        private const string VisibleWire = "0";
        private const string CollapsedWire = "1";
        private const string UserHiddenWire = "2";
        private const string ProgrammerHiddenWire = "3";

        public static string ToWire(this EItemVisibility v) => v switch
        {
            EItemVisibility.Collapsed        => CollapsedWire,
            EItemVisibility.UserHidden       => UserHiddenWire,
            EItemVisibility.ProgrammerHidden => ProgrammerHiddenWire,
            _                                => VisibleWire
        };

        public static EItemVisibility FromWire(string wire) => wire switch
        {
            CollapsedWire        => EItemVisibility.Collapsed,
            UserHiddenWire       => EItemVisibility.UserHidden,
            ProgrammerHiddenWire => EItemVisibility.ProgrammerHidden,
            _                    => EItemVisibility.Visible
        };
    }
}
