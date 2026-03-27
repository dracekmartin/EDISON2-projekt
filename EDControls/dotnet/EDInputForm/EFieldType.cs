namespace EDControls.EDInputForm
{
    public enum EFieldType
    {
        Text,
        Number,
        Date,
        Decimal,
        Bool,
        Button
    }

    public static class EFieldTypeWire
    {
        private const string TextWire = "TEXT";
        private const string NumberWire = "NUMBER";
        private const string DateWire = "DATE";
        private const string DecimalWire = "DECIMAL";
        private const string BoolWire = "BOOL";
        private const string ButtonWire = "BUTTON";

        public static string ToWire(this EFieldType type) => type switch
        {
            EFieldType.Number  => NumberWire,
            EFieldType.Date    => DateWire,
            EFieldType.Decimal => DecimalWire,
            EFieldType.Bool    => BoolWire,
            EFieldType.Button  => ButtonWire,
            _                  => TextWire
        };

        public static EFieldType FromWire(string wire) => wire switch
        {
            NumberWire  => EFieldType.Number,
            DateWire    => EFieldType.Date,
            DecimalWire => EFieldType.Decimal,
            BoolWire    => EFieldType.Bool,
            ButtonWire  => EFieldType.Button,
            _           => EFieldType.Text
        };
    }
}
