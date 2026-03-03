using EDControls.EDInputForm;
using System.Collections.Generic;

namespace EDControls.EDInputForm.DesignTimeData
{
    /// <summary>
    /// Design-time only sample data for the <see cref="InputForm"/> control.
    /// Referenced exclusively via <c>d:ItemsSource</c> in InputForm.xaml;
    /// never instantiated at runtime.
    /// </summary>
    public class EDInputFormDesignTimeData
    {
        public IEnumerable<Item> Items { get; } =
        [
            new Group { GroupName = "Osobní údaje" },
            new Field { Key = "jmeno",    Label = "Jméno",    FieldType = EFieldType.Text,   Required = true,  ValidateOnChange = true,  Value = "Jan" },
            new Field { Key = "prijmeni", Label = "Příjmení", FieldType = EFieldType.Text,   Required = true,  ValidateOnChange = true,  Value = "Novák" },
            new Field { Key = "vek",      Label = "Věk",      FieldType = EFieldType.Number, Required = false, Value = "42" },

            new Group { GroupName = "Adresa" },
            new Field { Key = "ulice", Label = "Ulice", FieldType = EFieldType.Text,   Required = false, Value = "Hlavní 1" },
            new Field { Key = "mesto", Label = "Město", FieldType = EFieldType.Text,   Required = false, Value = "Praha" },
            new Field { Key = "psc",   Label = "PSČ",   FieldType = EFieldType.Number, Required = false, Value = "10000", ValidateOnChange = true, ValidationError = "PSČ musí mít 5 číslic" },

            new Group { GroupName = "Kontakt" },
            new Field { Key = "email",   Label = "E-mail",  FieldType = EFieldType.Text,   Required = false, Value = "jan.novak@example.com" },
            new Field { Key = "telefon", Label = "Telefon", FieldType = EFieldType.Number, Required = false, Value = "" },
        ];
    }
}
