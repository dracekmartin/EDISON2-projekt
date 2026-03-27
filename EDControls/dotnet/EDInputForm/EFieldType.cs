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
        List
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

        public static string ToWire(this EFieldType type) => type switch
        {
            EFieldType.Number  => NumberWire,
            EFieldType.Date    => DateWire,
            EFieldType.Decimal => DecimalWire,
            EFieldType.Bool    => BoolWire,
            EFieldType.Button  => ButtonWire,
            EFieldType.List    => ListWire,
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
            _           => EFieldType.Text
        };
    }
}
