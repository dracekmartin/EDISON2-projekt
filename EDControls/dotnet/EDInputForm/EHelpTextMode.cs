namespace EDControls.EDInputForm
{
    public enum EHelpTextMode
    {
        Tooltip,
        Column
    }

    public static class EHelpTextModeWire
    {
        private const string TooltipWire = "tooltip";
        private const string ColumnWire = "column";

        public static string ToWire(this EHelpTextMode mode) => mode switch
        {
            EHelpTextMode.Column => ColumnWire,
            _ => TooltipWire
        };

        public static EHelpTextMode FromWire(string? wire) => wire switch
        {
            ColumnWire => EHelpTextMode.Column,
            _ => EHelpTextMode.Tooltip
        };
    }
}
