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
            new Field { Number =  1, Key = "poznamka", Label = "Poznámka", FieldType = EFieldType.Text },

            new Group { Number =  2, Key = "osobni", GroupName = "Osobní údaje" },
            new Field { Number =  3, Key = "jmeno",    Label = "Jméno",    FieldType = EFieldType.Text,   Required = true,  ValidateOnChange = true,  Value = "Jan" },
            new Field { Number =  4, Key = "prijmeni", Label = "Příjmení", FieldType = EFieldType.Text,   Required = true,  ValidateOnChange = true,  Value = "Novák" },
            new Field { Number =  5, Key = "vek",      Label = "Věk",      FieldType = EFieldType.Number, Required = false, Value = "42", Editable = false },

            new Group { Number =  6, Key = "adresa", GroupName = "Adresa" },
            new Field { Number =  7, Key = "ulice", Label = "Ulice", FieldType = EFieldType.Text,   Required = false, Value = "Hlavní 1" },
            new Field { Number =  8, Key = "mesto", Label = "Město", FieldType = EFieldType.Text,   Required = false, Value = "Praha" },
            new Field { Number =  9, Key = "psc",   Label = "PSČ",   FieldType = EFieldType.Number, Required = false, Value = "10000", ValidateOnChange = true, ValidationError = "PSČ musí mít 5 číslic" },

            new Group { Number = 10, Key = "skryta", GroupName = "Skrytá sekce", Visibility = EItemVisibility.Collapsed },
            new Field { Number = 11, Key = "skryte", Label = "Skryté pole", FieldType = EFieldType.Text, Value = "never shown" },
            new Field { Number = 12, Key = "programhidden", Label = "Programmer-hidden", FieldType = EFieldType.Text, Value = "hidden", Visibility = EItemVisibility.ProgrammerHidden },

            new Group { Number = 13, Key = "kontakt", GroupName = "Kontakt" },
            new Field { Number = 14, Key = "email",   Label = "E-mail",  FieldType = EFieldType.Text,   Required = false, Value = "jan.novak@example.com" },
            new Field { Number = 15, Key = "telefon", Label = "Telefon", FieldType = EFieldType.Number, Required = false, Value = "" },

            new Group { Number = 16, Key = "dalsi", GroupName = "Další údaje" },
            new Field { Number = 17, Key = "narozen",  Label = "Narozen",       FieldType = EFieldType.Date,    Value = "1984-05-15" },
            new Field { Number = 18, Key = "plat",     Label = "Plat",          FieldType = EFieldType.Decimal, Value = "45000.50" },
            new Field { Number = 19, Key = "aktivni",  Label = "Aktivní",       FieldType = EFieldType.Bool,    Value = "1" },
            new Field { Number = 20, Key = "smazano",  Label = "Smazáno",       FieldType = EFieldType.Bool,    Value = "0" },
        ];
    }
}
