namespace EDControls.EDInputForm
{
    public enum EFieldType
    {
        Text,
        Number,
        Date,
        Decimal,
        Bool,
        Button,
        List,
        File,
        /// <summary>
        /// Color picker — <see cref="Field.Value"/> drží hex string <c>"#AARRGGBB"</c>
        /// (SkiaSharp formát, ARGB with alpha). UI zobrazuje barevný čtvereček +
        /// hex TextBox + tlačítko otevírající color picker dialog.
        /// </summary>
        Color
    }

    public static class EFieldTypeWire
    {
        private const string TextWire = "TEXT";
        private const string NumberWire = "NUMBER";
        private const string DateWire = "DATE";
        private const string DecimalWire = "DECIMAL";
        private const string BoolWire = "BOOL";
        private const string ButtonWire = "BUTTON";
        private const string ListWire = "LIST";
        private const string FileWire = "FILE";
        private const string ColorWire = "COLOR";

        public static string ToWire(this EFieldType type) => type switch
        {
            EFieldType.Number  => NumberWire,
            EFieldType.Date    => DateWire,
            EFieldType.Decimal => DecimalWire,
            EFieldType.Bool    => BoolWire,
            EFieldType.Button  => ButtonWire,
            EFieldType.List    => ListWire,
            EFieldType.File    => FileWire,
            EFieldType.Color   => ColorWire,
            _                  => TextWire
        };

        public static EFieldType FromWire(string wire) => wire switch
        {
            NumberWire  => EFieldType.Number,
            DateWire    => EFieldType.Date,
            DecimalWire => EFieldType.Decimal,
            BoolWire    => EFieldType.Bool,
            ButtonWire  => EFieldType.Button,
            ListWire    => EFieldType.List,
            FileWire    => EFieldType.File,
            ColorWire   => EFieldType.Color,
            _           => EFieldType.Text
        };
    }
}
