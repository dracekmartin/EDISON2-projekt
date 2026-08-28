namespace EDControls.EDInputForm
{
    public enum HelpTextMode
    {
        Tooltip,
        Column
    }

    public static class HelpTextModeWire
    {
        private const string TooltipWire = "tooltip";
        private const string ColumnWire = "column";

        public static string ToWire(this HelpTextMode mode) => mode switch
        {
            HelpTextMode.Column => ColumnWire,
            _ => TooltipWire
        };

        public static HelpTextMode FromWire(string? wire) => wire switch
        {
            ColumnWire => HelpTextMode.Column,
            _ => HelpTextMode.Tooltip
        };
    }
}
