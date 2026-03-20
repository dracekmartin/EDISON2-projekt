namespace EDControls.EDInputForm
{
    public enum EFieldType
    {
        Text,
        Number
    }

    public static class EFieldTypeWire
    {
        private const string TextWire = "TEXT";
        private const string NumberWire = "NUMBER";

        public static string ToWire(this EFieldType type) => type switch
        {
            EFieldType.Number => NumberWire,
            _ => TextWire
        };

        public static EFieldType FromWire(string wire) =>
            wire == NumberWire ? EFieldType.Number : EFieldType.Text;
    }
}
