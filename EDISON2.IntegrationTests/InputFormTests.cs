using ALWA.SYS;
using EDControls;
using EDControls.EDInputForm;

namespace EDISON2.IntegrationTests;

/// <summary>EDInputForm: skutečné kódování řádků GROUP / FIELD a round trip hodnot, triggeru a changedKey.</summary>
[Trait("Category", "Integration")]
public class InputFormTests
{
    private const string UserProfileClass = "ALVA.EDISONMAIN2.EDInputForm.UserProfile";
    private const string ExampleClass = "ALVA.EDControls.EDInputForm.EDIFExample";

    // UserProfileWindow do těchto polí po Init doplňuje hodnoty a SetField neznámý klíč tiše přeskočí.
    private static readonly string[] HostPrefilledKeys =
        ["fullName", "loginName", "mlp", "usePersonDir", "personDir", "centralDir", "defaultWindow"];

    [DevServerFact]
    public void Init_UserProfile_ContainsFieldsTheHostPrefills()
    {
        var form = new EDInputFormController(DevServer.Connect(), UserProfileClass);

        form.Init();

        var keys = form.Items.OfType<Field>().Select(f => f.Key).ToList();
        Assert.All(HostPrefilledKeys, key => Assert.Contains(key, keys));
    }

    // ParseItems krátký řádek doplní defaulty a neznámý typ zahodí; posun pozic hlídá jen surový výstup.
    [DevServerFact]
    public void Init_RawRows_MatchParserLayout()
    {
        CVar raw = DevServer.Connect().QueryOrThrow(UserProfileClass, "Init", new CVar());

        Assert.Contains(raw, kvp => int.TryParse(kvp.Key, out _));
        foreach (KeyValuePair<string, CVar> kvp in raw)
        {
            if (!int.TryParse(kvp.Key, out _)) continue;
            CList row = kvp.Value;
            switch (row.GetString(1))
            {
                case "GROUP":
                    Assert.Equal(6, row.Length);
                    break;
                case "FIELD":
                    Assert.Equal(19, row.Length);
                    break;
                default:
                    Assert.Fail($"Řádek {kvp.Key}: neznámý typ '{row.GetString(1)}', ParseItems by ho zahodil.");
                    break;
            }
        }
    }

    // Pole s validateOnChange: hodnota, trigger a changedKey jdou na server a editovatelnost zpět.
    [DevServerFact]
    public void ValidateAndSave_TogglePersonDir_ServerDrivesEditabilityAndErrors()
    {
        var form = new EDInputFormController(DevServer.Connect(), UserProfileClass);
        form.Init();
        Assert.False(FieldOf(form, "personDir").Editable);

        FieldOf(form, "usePersonDir").Value = "1";
        form.Validate("usePersonDir");
        Assert.True(FieldOf(form, "personDir").Editable);

        // BeforeSave: zapnutý osobní adresář bez cesty server odmítne a Save se nezavolá.
        Assert.False(form.Save());
        Assert.NotNull(FieldOf(form, "personDir").ValidationError);

        FieldOf(form, "personDir").Value = @"C:\Export";
        Assert.True(form.Save());
    }

    // Pole každého typu plně vyplněné projde serverem (ReadFromIn → WriteToOut) a parserem beze změny. Atribut, který
    // jedna strana zapíše a druhá nepřečte (B44: pozice 19 v ReadFromIn) nebo přečte z vedlejší pozice, test shodí.
    [DevServerFact]
    public void Validate_FilledFieldOfEveryType_RoundTripsAllPositions()
    {
        List<Item> items =
        [
            new Group { Number = 0, Key = "g", GroupName = "Skupina", Visibility = ItemVisibility.Collapsed, HelpText = "Nápověda skupiny" },
            // EDIFExample.Validate tyto klíče čte bez kontroly, hodnoty jsou zvolené tak, aby je nezměnil.
            new Field { Key = "editToggle", FieldType = FieldType.Bool, Value = "1" },
            new Field { Key = "readonlyText", FieldType = FieldType.Text },
            new Field { Key = "comboDynamic", FieldType = FieldType.Bool, Value = "1" },
            new Field { Key = "comboDepA", FieldType = FieldType.ComboBox },
            new Field { Key = "comboDepB", FieldType = FieldType.ComboBox },
        ];
        var types = Enum.GetValues<FieldType>();
        for (int i = 0; i < types.Length; i++)
            items.Add(FilledField(types[i], i + 1));
        int number = 0;
        foreach (var field in items.OfType<Field>()) field.Number = ++number;   // server pole přečísluje stejně
        CVar sent = EDInputFormWire.SerializeItemsCVar(items);
        AssertEveryPositionFilled(sent);
        CVar input = EDInputFormWire.SerializeItemsCVar(items);
        input["trigger"] = new CVar(nameof(ValidationTrigger.Init));

        CVar output = DevServer.Connect().QueryOrThrow(ExampleClass, "Validate", input);

        CVar back = EDInputFormWire.SerializeItemsCVar(EDInputFormWire.ParseItems(output));
        Assert.Equal(Rows(sent), Rows(back));
    }

    // validationError (pozice 9) ReadFromIn vědomě nečte, proto prázdná. Image hodnota na server nejde. Booleovské
    // pozice a viditelnost se mezi řádky střídají, aby čtení ze sousední pozice neprošlo.
    private static Field FilledField(FieldType type, int row) => new()
    {
        Key = "pole" + type, Label = "Pole " + type, FieldType = type,
        Value = type switch
        {
            FieldType.Text => "hodnota",
            FieldType.Number => "5",
            FieldType.Date => "2026-10-12",
            FieldType.Decimal => "3.5",
            FieldType.Bool => "1",
            FieldType.Button => "Klikni",
            FieldType.List => "b",
            FieldType.File => @"C:\a.txt",
            FieldType.Color => "#FF112233",
            FieldType.ComboBox => "vlastní",
            FieldType.Image => "",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Nový typ pole: doplň ukázkovou hodnotu."),
        },
        ValidateOnChange = Bit(row, 0), Required = Bit(row, 1), Editable = Bit(row, 2),
        Visibility = Visibilities[row % Visibilities.Length], HelpText = "nápověda",
        ListItems = type is FieldType.List or FieldType.ComboBox ? [new ListItem("a", "Áčko"), new ListItem("b", "b")] : [],
        FileFilter = type == FieldType.File ? "Text|*.txt" : "",
        Min = 2, Max = 9, Format = "0.00", Unit = "ks", Placeholders = Bit(row, 3),
    };

    private static readonly ItemVisibility[] Visibilities =
        [ItemVisibility.Visible, ItemVisibility.Collapsed, ItemVisibility.UserHidden, ItemVisibility.ProgrammerHidden];

    private static bool Bit(int row, int bit) => ((row >> bit) & 1) == 1;

    // Nová pozice musí v ukázce dostat jinou než výchozí hodnotu, jinak by její nečtení na serveru prošlo.
    private static void AssertEveryPositionFilled(CVar wire)
    {
        CList empty = EDInputFormWire.SerializeItemsCVar([new Field()])["1"];
        var fields = wire.Select(kvp => { CList row = kvp.Value; return row; }).Where(r => r.GetString(1) == "FIELD").ToList();
        for (int p = 3; p <= empty.Length; p++)
        {
            if (p == 9) continue;   // validationError server nečte
            Assert.True(fields.Any(r => r.GetString(p) != empty.GetString(p)), $"Pozice {p} má ve všech řádcích výchozí hodnotu.");
        }
    }

    private static List<string> Rows(CVar wire) =>
    [
        .. wire.Select(kvp =>
        {
            CList row = kvp.Value;
            return kvp.Key + ": " + string.Join(" ¦ ", Enumerable.Range(1, row.Length).Select(row.GetString));
        }),
    ];

    private static Field FieldOf(EDInputFormController form, string key) =>
        form.Items.OfType<Field>().Single(f => f.Key == key);
}
