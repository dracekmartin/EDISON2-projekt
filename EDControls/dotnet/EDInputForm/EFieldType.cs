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
        Color,
        /// <summary>
        /// Editable ComboBox s autocomplete (live filtering). Sdílí storage s
        /// <see cref="List"/> (<c>Field.ListItems</c>), ale na rozdíl od List dovoluje
        /// uživateli napsat libovolný text mimo list — items jsou nápověda, ne striktní enum.
        /// Použití: placeholder picker, volné textové pole s číselníkem doporučených hodnot.
        ///
        /// <para>
        /// Rozdíl od <see cref="List"/>: List = strict dropdown (jen items), ComboBox = editable
        /// + autocomplete (custom value allowed). Analogie HTML `&lt;select&gt;` vs
        /// `&lt;input list="..."&gt;` nebo Material UI Select vs Autocomplete.
        /// </para>
        /// </summary>
        ComboBox
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
        private const string ComboBoxWire = "COMBOBOX";

        public static string ToWire(this EFieldType type) => type switch
        {
            EFieldType.Number   => NumberWire,
            EFieldType.Date     => DateWire,
            EFieldType.Decimal  => DecimalWire,
            EFieldType.Bool     => BoolWire,
            EFieldType.Button   => ButtonWire,
            EFieldType.List     => ListWire,
            EFieldType.File     => FileWire,
            EFieldType.Color    => ColorWire,
            EFieldType.ComboBox => ComboBoxWire,
            _                   => TextWire
        };

        public static EFieldType FromWire(string wire) => wire switch
        {
            NumberWire   => EFieldType.Number,
            DateWire     => EFieldType.Date,
            DecimalWire  => EFieldType.Decimal,
            BoolWire     => EFieldType.Bool,
            ButtonWire   => EFieldType.Button,
            ListWire     => EFieldType.List,
            FileWire     => EFieldType.File,
            ColorWire    => EFieldType.Color,
            ComboBoxWire => EFieldType.ComboBox,
            _            => EFieldType.Text
        };
    }
}
