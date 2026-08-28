namespace EDControls.EDInputForm
{
    public enum FieldType
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
        /// Color picker - <see cref="Field.Value"/> drží hex string <c>"#AARRGGBB"</c>
        /// (SkiaSharp formát, ARGB with alpha). UI zobrazuje barevný čtvereček +
        /// hex TextBox + tlačítko otevírající color picker dialog.
        /// </summary>
        Color,
        /// <summary>
        /// Editable ComboBox s autocomplete (live filtering). Sdílí storage s
        /// <see cref="List"/> (<c>Field.ListItems</c>), ale na rozdíl od List dovoluje
        /// uživateli napsat libovolný text mimo list - items jsou nápověda, ne striktní enum.
        /// Použití: placeholder picker, volné textové pole s číselníkem doporučených hodnot.
        /// </summary>
        ComboBox,
        /// <summary>
        /// Obrázek - <see cref="Field.Value"/> drží Base64 string (PNG/JPG). UI zobrazuje
        /// náhled + tlačítko „Nahrát…" (otevře file dialog) + „×" (vymazat). Výběr souboru a
        /// převod na Base64 (vč. downscale) řeší host přes <c>EDInputForm.PickImageImpl</c>
        /// (analogie <c>PickColorImpl</c>) - EDControls nemá obrázkové zpracování.
        /// </summary>
        Image
    }

    public static class FieldTypeWire
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
        private const string ImageWire = "IMAGE";

        public static string ToWire(this FieldType type) => type switch
        {
            FieldType.Number   => NumberWire,
            FieldType.Date     => DateWire,
            FieldType.Decimal  => DecimalWire,
            FieldType.Bool     => BoolWire,
            FieldType.Button   => ButtonWire,
            FieldType.List     => ListWire,
            FieldType.File     => FileWire,
            FieldType.Color    => ColorWire,
            FieldType.ComboBox => ComboBoxWire,
            FieldType.Image    => ImageWire,
            _                   => TextWire
        };

        public static FieldType FromWire(string wire) => wire switch
        {
            NumberWire   => FieldType.Number,
            DateWire     => FieldType.Date,
            DecimalWire  => FieldType.Decimal,
            BoolWire     => FieldType.Bool,
            ButtonWire   => FieldType.Button,
            ListWire     => FieldType.List,
            FileWire     => FieldType.File,
            ColorWire    => FieldType.Color,
            ComboBoxWire => FieldType.ComboBox,
            ImageWire    => FieldType.Image,
            _            => FieldType.Text
        };
    }
}
