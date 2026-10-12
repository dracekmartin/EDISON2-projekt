using ALWA.SYS;
using EDControls.EDInputForm;

namespace EDControls.Tests;

public class EDInputFormParsingTests
{
    [Fact]
    public void ParseItems_EmptyOutput_ReturnsEmptyList()
    {
        var items = EDInputForm.EDInputFormWire.ParseItems(new CVar());

        Assert.Empty(items);
    }

    [Fact]
    public void ParseItems_SkipsERRKey()
    {
        var data = new CVar();
        data["ERR"] = new CVar("some error metadata");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        Assert.Empty(items);
    }

    /// <summary>Nový pojmenovaný výstup serveru nesmí shodit starší klient.</summary>
    [Fact]
    public void ParseItems_SkipsNamedOutputKeys()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0");
        data["title"] = new CVar("Vlastnosti prvku");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        Assert.Equal("k", Assert.IsType<Field>(Assert.Single(items)).Key);
    }

    [Fact]
    public void ParseItems_ParsesGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "osobni", "Osobní údaje");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(1, g.Number);
        Assert.Equal("osobni", g.Key);
        Assert.Equal("Osobní údaje", g.GroupName);
        Assert.Equal(ItemVisibility.Visible, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesCollapsedGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "5", "skryta", "Skrytá", "1");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(5, g.Number);
        Assert.Equal(ItemVisibility.Collapsed, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesGroupWithProgrammerHidden()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "G", "3");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(ItemVisibility.ProgrammerHidden, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesTextField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "2", "jmeno", "Jméno", "TEXT", "Jan", "0", "1");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(2, f.Number);
        Assert.Equal("jmeno", f.Key);
        Assert.Equal("Jméno", f.Label);
        Assert.Equal(FieldType.Text, f.FieldType);
        Assert.Equal("Jan", f.Value);
        Assert.False(f.ValidateOnChange);
        Assert.True(f.Required);
        Assert.Null(f.ValidationError);
    }

    [Fact]
    public void ParseItems_ParsesNumberField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "3", "vek", "Věk", "NUMBER", "42", "1", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.Number, f.FieldType);
        Assert.Equal("42", f.Value);
        Assert.True(f.ValidateOnChange);
        Assert.False(f.Required);
    }

    [Fact]
    public void ParseItems_EmptyValueBecomesNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.Value);
    }

    [Fact]
    public void ParseItems_ParsesValidationError()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0", "Povinné pole");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal("Povinné pole", f.ValidationError);
        Assert.True(f.HasError);
    }

    [Fact]
    public void ParseItems_FieldsAfterGroupGetParentGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "sekcea", "Sekce A");
        data["2"] = new CList("FIELD", "2", "a1", "Pole A1", "TEXT", "", "0", "0");
        data["3"] = new CList("FIELD", "3", "a2", "Pole A2", "TEXT", "", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        Assert.Equal(3, items.Count);
        var f1 = Assert.IsType<Field>(items[1]);
        var f2 = Assert.IsType<Field>(items[2]);
        Assert.Equal("Sekce A", f1.ParentGroup?.GroupName);
        Assert.Equal("Sekce A", f2.ParentGroup?.GroupName);
    }

    [Fact]
    public void ParseItems_FieldsWithoutGroupHaveNullParent()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "top", "Top", "TEXT", "val", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.ParentGroup);
    }

    /// <summary>
    /// Přečtené a znovu zapsané řádky jsou stejné jako na drátě, pro plně vyplněné pole každého typu.
    /// Atribut, který kodek zapíše a nepřečte (B44: pozice 19) nebo přečte z vedlejší pozice, test shodí.
    /// </summary>
    [Fact]
    public void SerializeItemsCVar_OfParsedWire_EqualsWire()
    {
        var wire = new CVar();
        wire["1"] = new CList("GROUP", "0", "g", "Skupina", "1", "Nápověda skupiny");
        int number = 0;
        foreach (var type in Enum.GetValues<FieldType>())
        {
            number++;
            // Booleovské pozice a viditelnost se mezi řádky střídají, aby čtení ze sousední pozice neprošlo.
            wire[(number + 1).ToString()] = new CList(
                "FIELD", number.ToString(), "pole" + number, "Pole " + number, type.ToWire(), SampleValue(type),
                Bit(number, 0), Bit(number, 1), "chyba", Bit(number, 2), (number % 4).ToString(), "nápověda",
                type is FieldType.List or FieldType.ComboBox ? "a~Áčko|b" : "",
                type == FieldType.File ? "Text|*.txt" : "",
                "2", "9", "0.00", "ks", Bit(number, 3));
        }

        var again = EDInputForm.EDInputFormWire.SerializeItemsCVar(EDInputForm.EDInputFormWire.ParseItems(wire));

        Assert.Equal(Rows(wire), Rows(again));
        AssertEveryPositionFilled(wire);
    }

    private static string Bit(int number, int bit) => ((number >> bit) & 1).ToString();

    // Nová pozice musí v ukázce dostat jinou než výchozí hodnotu, jinak by její nečtení prošlo.
    private static void AssertEveryPositionFilled(CVar wire)
    {
        CList empty = EDInputForm.EDInputFormWire.SerializeItemsCVar([new Field()])["1"];
        var fields = wire.Select(kvp => { CList row = kvp.Value; return row; }).Where(r => r.GetString(1) == "FIELD").ToList();
        for (int p = 3; p <= empty.Length; p++)
            Assert.True(fields.Any(r => r.GetString(p) != empty.GetString(p)), $"Pozice {p} má ve všech řádcích výchozí hodnotu.");
    }

    // Image hodnota na server nikdy nejde (limit MLRest), proto prázdná.
    private static string SampleValue(FieldType type) => type switch
    {
        FieldType.Text     => "hodnota",
        FieldType.Number   => "5",
        FieldType.Date     => "2026-10-12",
        FieldType.Decimal  => "3.5",
        FieldType.Bool     => "1",
        FieldType.Button   => "Klikni",
        FieldType.List     => "b",
        FieldType.File     => @"C:\a.txt",
        FieldType.Color    => "#FF112233",
        FieldType.ComboBox => "vlastní",
        FieldType.Image    => "",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Nový typ pole: doplň ukázkovou hodnotu."),
    };

    private static List<string> Rows(CVar wire) =>
    [
        .. wire.Select(kvp =>
        {
            CList row = kvp.Value;
            return kvp.Key + ": " + string.Join(" ¦ ", Enumerable.Range(1, row.Length).Select(row.GetString));
        }),
    ];

    [Fact]
    public void SerializeItemsCVar_RoundTripsGroupAndField()
    {
        var group = new Group { Number = 1, Key = "sekce", GroupName = "Sekce" };
        var field = new Field
        {
            Number = 2, Key = "k", Label = "Lbl", FieldType = FieldType.Text,
            Value = "hello", ValidateOnChange = true, Required = true
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([group, field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        Assert.Equal(2, parsed.Count);

        var g = Assert.IsType<Group>(parsed[0]);
        Assert.Equal(1, g.Number);
        Assert.Equal("sekce", g.Key);
        Assert.Equal("Sekce", g.GroupName);
        Assert.Equal(ItemVisibility.Visible, g.Visibility);

        var f = Assert.IsType<Field>(parsed[1]);
        Assert.Equal(2, f.Number);
        Assert.Equal("k", f.Key);
        Assert.Equal("Lbl", f.Label);
        Assert.Equal(FieldType.Text, f.FieldType);
        Assert.Equal("hello", f.Value);
        Assert.True(f.ValidateOnChange);
        Assert.True(f.Required);
    }

    [Fact]
    public void ParseItems_ParsesDateField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "narozen", "Narozen", "DATE", "2024-05-15", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.Date, f.FieldType);
        Assert.Equal("2024-05-15", f.Value);
    }

    [Fact]
    public void ParseItems_ParsesDecimalField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "plat", "Plat", "DECIMAL", "45000.50", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.Decimal, f.FieldType);
        Assert.Equal("45000.50", f.Value);
    }

    [Fact]
    public void ParseItems_ParsesBoolField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "aktivni", "Aktivní", "BOOL", "1", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.Bool, f.FieldType);
        Assert.Equal("1", f.Value);
        Assert.True(f.BoolValue);
    }

    [Fact]
    public void BoolValue_SetTrue_SetsValueTo1()
    {
        var field = new Field { FieldType = FieldType.Bool, Value = "0" };

        field.BoolValue = true;

        Assert.Equal("1", field.Value);
        Assert.True(field.BoolValue);
    }

    [Fact]
    public void BoolValue_SetFalse_SetsValueTo0()
    {
        var field = new Field { FieldType = FieldType.Bool, Value = "1" };

        field.BoolValue = false;

        Assert.Equal("0", field.Value);
        Assert.False(field.BoolValue);
    }

    [Theory]
    [InlineData("DATE", FieldType.Date)]
    [InlineData("DECIMAL", FieldType.Decimal)]
    [InlineData("BOOL", FieldType.Bool)]
    [InlineData("TEXT", FieldType.Text)]
    [InlineData("NUMBER", FieldType.Number)]
    [InlineData("BUTTON", FieldType.Button)]
    [InlineData("LIST", FieldType.List)]
    [InlineData("FILE", FieldType.File)]
    [InlineData("COLOR", FieldType.Color)]
    [InlineData("COMBOBOX", FieldType.ComboBox)]
    [InlineData("IMAGE", FieldType.Image)]
    [InlineData("UNKNOWN", FieldType.Text)]
    public void EFieldTypeWire_RoundTrips(string wire, FieldType expected)
    {
        Assert.Equal(expected, FieldTypeWire.FromWire(wire));
        if (wire != "UNKNOWN")
            Assert.Equal(wire, expected.ToWire());
    }

    [Fact]
    public void DateValue_Get_ParsesYyyyMmDd()
    {
        var field = new Field { FieldType = FieldType.Date, Value = "2024-05-15" };
        Assert.Equal(new System.DateTime(2024, 5, 15), field.DateValue);
    }

    [Fact]
    public void DateValue_Get_InvalidReturnsNull()
    {
        var field = new Field { FieldType = FieldType.Date, Value = "not-a-date" };
        Assert.Null(field.DateValue);
    }

    [Fact]
    public void DateValue_Set_FormatsToWire()
    {
        var field = new Field { FieldType = FieldType.Date };
        field.DateValue = new System.DateTime(2024, 12, 31);
        Assert.Equal("2024-12-31", field.Value);
    }

    [Fact]
    public void DateValue_SetNull_ClearsValue()
    {
        var field = new Field { FieldType = FieldType.Date, Value = "2024-05-15" };
        field.DateValue = null;
        Assert.Equal("", field.Value);
    }

    [Theory]
    [InlineData(FieldType.Number, "42", null)]
    [InlineData(FieldType.Number, "-5", null)]
    [InlineData(FieldType.Number, "3.14", "Musí být celé číslo")]
    [InlineData(FieldType.Number, "abc", "Musí být celé číslo")]
    [InlineData(FieldType.Decimal, "42", null)]
    [InlineData(FieldType.Decimal, "3.14", null)]
    [InlineData(FieldType.Decimal, "-1.5", null)]
    [InlineData(FieldType.Decimal, "abc", "Musí být číslo")]
    [InlineData(FieldType.Date, "2024-05-15", null)]
    [InlineData(FieldType.Date, "not-a-date", "Neplatné datum")]
    [InlineData(FieldType.Bool, "1", null)]
    [InlineData(FieldType.Bool, "0", null)]
    [InlineData(FieldType.Bool, "yes", "Musí být 0 nebo 1")]
    [InlineData(FieldType.Text, "anything", null)]
    [InlineData(FieldType.List, "anything", null)]
    [InlineData(FieldType.File, @"C:\some\path.pdf", null)]
    public void GetTypeError_ValidatesValueByFieldType(FieldType type, string value, string? expected)
    {
        var field = new Field { FieldType = type, Value = value };
        Assert.Equal(expected, field.GetTypeError());
    }

    [Theory]
    [InlineData(FieldType.Number, null)]
    [InlineData(FieldType.Decimal, null)]
    [InlineData(FieldType.Date, null)]
    [InlineData(FieldType.Bool, null)]
    [InlineData(FieldType.List, null)]
    [InlineData(FieldType.File, null)]
    public void GetTypeError_EmptyValue_ReturnsNull(FieldType type, string? value)
    {
        var field = new Field { FieldType = type, Value = value };
        Assert.Null(field.GetTypeError());
    }

    [Fact]
    public void ParseItems_ParsesNonEditableField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0", "", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.False(f.Editable);
    }

    [Fact]
    public void NonEditableIndicatorVisibility_ReflectsEditable()
    {
        Assert.Equal(System.Windows.Visibility.Collapsed,
            new Field { Editable = true }.NonEditableIndicatorVisibility);
        Assert.Equal(System.Windows.Visibility.Visible,
            new Field { Editable = false }.NonEditableIndicatorVisibility);
    }

    [Fact]
    public void ParseItems_FieldWithoutEditableDefaultsToTrue()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.True(f.Editable);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsEditableField()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = FieldType.Text,
            Value = "v", Editable = false
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.False(f.Editable);
    }

    [Fact]
    public void SerializeItemsCVar_NullItems_ReturnsEmptyCVar()
    {
        CVar result = EDInputForm.EDInputFormWire.SerializeItemsCVar(null);

        int count = 0;
        foreach (var _ in result) count++;
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData("0", ItemVisibility.Visible)]
    [InlineData("1", ItemVisibility.Collapsed)]
    [InlineData("2", ItemVisibility.UserHidden)]
    [InlineData("3", ItemVisibility.ProgrammerHidden)]
    [InlineData("UNKNOWN", ItemVisibility.Visible)]
    public void EItemVisibilityWire_RoundTrips(string wire, ItemVisibility expected)
    {
        Assert.Equal(expected, ItemVisibilityWire.FromWire(wire));
        if (wire != "UNKNOWN")
            Assert.Equal(wire, expected.ToWire());
    }

    [Fact]
    public void ParseItems_ParsesFieldWithUserHidden()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "2");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(ItemVisibility.UserHidden, f.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesFieldWithProgrammerHidden()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "3");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(ItemVisibility.ProgrammerHidden, f.Visibility);
    }

    [Fact]
    public void ParseItems_FieldWithoutVisibilityDefaultsToVisible()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(ItemVisibility.Visible, f.Visibility);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFieldVisibility()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = FieldType.Text,
            Value = "v", Visibility = ItemVisibility.ProgrammerHidden
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(ItemVisibility.ProgrammerHidden, f.Visibility);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsGroupVisibility()
    {
        var group = new Group
        {
            Number = 1, Key = "g", GroupName = "G",
            Visibility = ItemVisibility.Collapsed
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([group]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var g = Assert.IsType<Group>(Assert.Single(parsed));
        Assert.Equal(ItemVisibility.Collapsed, g.Visibility);
    }

    [Fact]
    public void EffectiveVisibility_HiddenField_IsCollapsed()
    {
        var field = new Field { Visibility = ItemVisibility.ProgrammerHidden };
        Assert.Equal(System.Windows.Visibility.Collapsed, field.EffectiveVisibility);
    }

    [Fact]
    public void EffectiveVisibility_VisibleFieldNoParent_IsVisible()
    {
        var field = new Field { Visibility = ItemVisibility.Visible };
        Assert.Equal(System.Windows.Visibility.Visible, field.EffectiveVisibility);
    }

    [Fact]
    public void ParseItems_ParsesFieldHelpText()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0", "Zadejte hodnotu");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal("Zadejte hodnotu", f.HelpText);
    }

    [Fact]
    public void ParseItems_ParsesGroupHelpText()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "Skupina", "0", "Vyplňte tuto sekci");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal("Vyplňte tuto sekci", g.HelpText);
    }

    [Fact]
    public void ParseItems_FieldWithoutHelpTextDefaultsToNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.HelpText);
    }

    [Fact]
    public void ParseItems_GroupWithoutHelpTextDefaultsToNull()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "Skupina", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Null(g.HelpText);
    }

    [Fact]
    public void ParseItems_FieldEmptyHelpTextBecomesNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0", "");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.HelpText);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFieldHelpText()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = FieldType.Text,
            Value = "v", HelpText = "Help for field"
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal("Help for field", f.HelpText);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsGroupHelpText()
    {
        var group = new Group
        {
            Number = 1, Key = "g", GroupName = "G", HelpText = "Help for group"
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([group]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var g = Assert.IsType<Group>(Assert.Single(parsed));
        Assert.Equal("Help for group", g.HelpText);
    }

    [Fact]
    public void SerializeItemsCVar_NullHelpText_RoundTripsAsNull()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = FieldType.Text,
            Value = "v", HelpText = null
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Null(f.HelpText);
    }

    [Fact]
    public void HelpTextVisibility_WithHelpText_IsVisible()
    {
        var field = new Field { HelpText = "Some help" };
        Assert.Equal(System.Windows.Visibility.Visible, field.HelpTextVisibility);
    }

    [Fact]
    public void HelpTextVisibility_WithoutHelpText_IsCollapsed()
    {
        var field = new Field { HelpText = null };
        Assert.Equal(System.Windows.Visibility.Collapsed, field.HelpTextVisibility);
    }

    [Fact]
    public void ParseHelpTextMode_Tooltip_ReturnsTooltip()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("tooltip");

        Assert.Equal(HelpTextMode.Tooltip, EDInputForm.EDInputFormWire.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseHelpTextMode_Column_ReturnsColumn()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("column");

        Assert.Equal(HelpTextMode.Column, EDInputForm.EDInputFormWire.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseHelpTextMode_Missing_DefaultsToTooltip()
    {
        var data = new CVar();

        Assert.Equal(HelpTextMode.Tooltip, EDInputForm.EDInputFormWire.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseItems_SkipsHelpTextModeKey()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("column");
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        Assert.Single(items);
        Assert.IsType<Field>(items[0]);
    }

    [Theory]
    [InlineData("tooltip", HelpTextMode.Tooltip)]
    [InlineData("column", HelpTextMode.Column)]
    [InlineData("UNKNOWN", HelpTextMode.Tooltip)]
    public void EHelpTextModeWire_FromWire(string wire, HelpTextMode expected)
    {
        Assert.Equal(expected, HelpTextModeWire.FromWire(wire));
    }

    [Fact]
    public void EHelpTextModeWire_RoundTrips()
    {
        Assert.Equal("tooltip", HelpTextMode.Tooltip.ToWire());
        Assert.Equal("column", HelpTextMode.Column.ToWire());
    }

    [Fact]
    public void ParseItems_ParsesListField()
    {
        var data = new CVar();
        // Position 12 = helpText (empty), position 13 = listItems
        data["1"] = new CList("FIELD", "1", "stav", "Stav", "LIST", "A", "0", "0", "", "1", "0", "",
            "A~Aktivní|B~Neaktivní");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.List, f.FieldType);
        Assert.Equal("A", f.Value);
        Assert.Equal(2, f.ListItems.Count);
        Assert.Equal("A", f.ListItems[0].Key);
        Assert.Equal("Aktivní", f.ListItems[0].Label);
        Assert.Equal("B", f.ListItems[1].Key);
        Assert.Equal("Neaktivní", f.ListItems[1].Label);
    }

    [Fact]
    public void ParseItems_ListFieldEmptyOptions()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "stav", "Stav", "LIST", "", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.List, f.FieldType);
        Assert.Empty(f.ListItems);
    }

    [Fact]
    public void ParseItems_ListFieldKeyOnlyOptions()
    {
        var data = new CVar();
        // Position 12 = helpText (empty), position 13 = listItems (key-only)
        data["1"] = new CList("FIELD", "1", "barva", "Barva", "LIST", "Červená", "0", "0", "", "1", "0", "",
            "Červená|Zelená|Modrá");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(3, f.ListItems.Count);
        Assert.Equal("Červená", f.ListItems[0].Key);
        Assert.Equal("Červená", f.ListItems[0].Label);
        Assert.Equal("Zelená", f.ListItems[1].Key);
        Assert.Equal("Zelená", f.ListItems[1].Label);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsListField()
    {
        var field = new Field
        {
            Number = 1, Key = "stav", Label = "Stav", FieldType = FieldType.List,
            Value = "A", ListItems = [new ListItem("A", "Aktivní"), new ListItem("N", "Neaktivní")]
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(FieldType.List, f.FieldType);
        Assert.Equal("A", f.Value);
        Assert.Equal(2, f.ListItems.Count);
        Assert.Equal("A", f.ListItems[0].Key);
        Assert.Equal("Aktivní", f.ListItems[0].Label);
        Assert.Equal("N", f.ListItems[1].Key);
        Assert.Equal("Neaktivní", f.ListItems[1].Label);
    }

    [Fact]
    public void ParseItems_ParsesFileFieldWithFilter()
    {
        var data = new CVar();
        // positions: 1-type, 2-number, 3-key, 4-label, 5-fieldType, 6-value, 7-voc, 8-req,
        //            9-valErr, 10-editable, 11-visibility, 12-helpText, 13-listItems, 14-fileFilter
        data["1"] = new CList("FIELD", "1", "soubor", "Soubor", "FILE", @"C:\test.pdf", "0", "0",
            "", "1", "0", "", "", "PDF (*.pdf)|*.pdf");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.File, f.FieldType);
        Assert.Equal(@"C:\test.pdf", f.Value);
        Assert.Equal("PDF (*.pdf)|*.pdf", f.FileFilter);
    }

    [Fact]
    public void ParseItems_ParsesFileFolderMode()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "slozka", "Složka", "FILE", @"C:\Output", "0", "0",
            "", "1", "0", "", "", "$FOLDER");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.File, f.FieldType);
        Assert.Equal(@"C:\Output", f.Value);
        Assert.Equal("$FOLDER", f.FileFilter);
    }

    [Fact]
    public void ParseItems_FileFieldWithoutFilter_DefaultsToEmpty()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "soubor", "Soubor", "FILE", "", "0", "0");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.File, f.FieldType);
        Assert.Equal("", f.FileFilter);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFileField()
    {
        var field = new Field
        {
            Number = 1, Key = "soubor", Label = "Soubor", FieldType = FieldType.File,
            Value = @"C:\test.pdf", FileFilter = "PDF (*.pdf)|*.pdf"
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(FieldType.File, f.FieldType);
        Assert.Equal(@"C:\test.pdf", f.Value);
        Assert.Equal("PDF (*.pdf)|*.pdf", f.FileFilter);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFileFolderMode()
    {
        var field = new Field
        {
            Number = 1, Key = "slozka", Label = "Složka", FieldType = FieldType.File,
            Value = @"C:\Output", FileFilter = "$FOLDER"
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(FieldType.File, f.FieldType);
        Assert.Equal(@"C:\Output", f.Value);
        Assert.Equal("$FOLDER", f.FileFilter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetRequiredError_RequiredEmptyField_ReturnsError(string? value)
    {
        var field = new Field { Required = true, Value = value };
        Assert.Equal("Povinné pole", field.GetRequiredError());
    }

    [Fact]
    public void GetRequiredError_RequiredFilledField_ReturnsNull()
    {
        var field = new Field { Required = true, Value = "Jan" };
        Assert.Null(field.GetRequiredError());
    }

    [Fact]
    public void GetRequiredError_NotRequired_ReturnsNull()
    {
        var field = new Field { Required = false, Value = null };
        Assert.Null(field.GetRequiredError());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(null)]
    public void GetRequiredError_RequiredBoolField_ReturnsNull(string? value)
    {
        var field = new Field { Required = true, FieldType = FieldType.Bool, Value = value };
        Assert.Null(field.GetRequiredError());
    }

    private static (List<Group> groups, List<Field> fields) BuildTwoGroupForm()
    {
        var g1 = new Group { Number = 1, Key = "params", GroupName = "Parametry" };
        var g2 = new Group { Number = 4, Key = "export", GroupName = "Export" };
        var f1 = new Field { Number = 2, Key = "datum",  Label = "Datum platnosti", ParentGroup = g1 };
        var f2 = new Field { Number = 3, Key = "linka",  Label = "Číslo linky",     ParentGroup = g1 };
        var f3 = new Field { Number = 5, Key = "soubor", Label = "Název souboru",   ParentGroup = g2 };
        return ([g1, g2], [f1, f2, f3]);
    }

    [Fact]
    public void ComputeSearchMatches_FieldLabelMatch_ShowsOnlyMatchingField()
    {
        var (groups, fields) = BuildTwoGroupForm();

        var matches = EDInputForm.EDInputForm.ComputeSearchMatches(groups, fields, "linky");

        Assert.Equal("Číslo linky", Assert.Single(matches).Label);
        Assert.False(fields[1].IsFilteredBySearch);          // Číslo linky prošlo
        Assert.True(fields[0].IsFilteredBySearch);           // Datum platnosti odfiltrováno
        Assert.True(fields[2].IsFilteredBySearch);           // Název souboru odfiltrováno
        Assert.False(groups[0].IsFilteredBySearch);          // Parametry zůstávají (mají match)
        Assert.True(groups[1].IsFilteredBySearch);           // Export zmizí
    }

    [Fact]
    public void ComputeSearchMatches_GroupNameMatch_ShowsAllFieldsOfThatGroup()
    {
        var (groups, fields) = BuildTwoGroupForm();

        var matches = EDInputForm.EDInputForm.ComputeSearchMatches(groups, fields, "parametry");

        Assert.Equal(2, matches.Count);                      // obě pole skupiny Parametry
        Assert.False(fields[0].IsFilteredBySearch);
        Assert.False(fields[1].IsFilteredBySearch);
        Assert.True(fields[2].IsFilteredBySearch);           // Export pole skryto
        Assert.False(groups[0].IsFilteredBySearch);
        Assert.True(groups[0].IsExpanded);                   // matchnutá skupina se rozbalí
        Assert.True(groups[1].IsFilteredBySearch);
    }

    [Fact]
    public void ComputeSearchMatches_GroupNameMatch_ExpandsCollapsedGroup()
    {
        var (groups, fields) = BuildTwoGroupForm();
        groups[1].IsExpanded = false;                        // Export sbalený

        EDInputForm.EDInputForm.ComputeSearchMatches(groups, fields, "export");

        Assert.False(groups[1].IsFilteredBySearch);
        Assert.True(groups[1].IsExpanded);                   // rozbalí se, ať je nalezené pole vidět
        Assert.False(fields[2].IsFilteredBySearch);
    }

    [Fact]
    public void ComputeSearchMatches_DiacriticsInsensitive()
    {
        var (groups, fields) = BuildTwoGroupForm();

        var matches = EDInputForm.EDInputForm.ComputeSearchMatches(groups, fields, "cislo");

        Assert.Equal("Číslo linky", Assert.Single(matches).Label);
    }

    [Fact]
    public void ComputeSearchMatches_NoMatch_FiltersEverything()
    {
        var (groups, fields) = BuildTwoGroupForm();

        var matches = EDInputForm.EDInputForm.ComputeSearchMatches(groups, fields, "xyzzy");

        Assert.Empty(matches);
        Assert.All(fields, f => Assert.True(f.IsFilteredBySearch));
        Assert.All(groups, g => Assert.True(g.IsFilteredBySearch));
    }

    [Fact]
    public void FormatShortcutsHelp_ListsEveryShortcut()
    {
        var text = EDInputForm.EDInputForm.FormatShortcutsHelp();

        Assert.False(string.IsNullOrWhiteSpace(text));
        foreach (var (keys, description) in EDInputForm.EDInputForm.KeyboardShortcuts)
        {
            Assert.Contains(keys, text);
            Assert.Contains(description, text);
        }
    }

    [Fact]
    public void FormatShortcutsHelp_OmitsUnwiredSearchKeys()
    {
        // Search Enter/Shift+Enter/Esc jsou zatím jen v tooltipech, nefungují →
        // nesmí se vypsat, ať nápověda nelže.
        var text = EDInputForm.EDInputForm.FormatShortcutsHelp();

        Assert.DoesNotContain("Shift+Enter", text);
        Assert.DoesNotContain("Esc", text);
    }

    [Fact]
    public void ParseItems_ParsesMinMaxFormatUnit()
    {
        var data = new CVar();
        // positions: ...12-helpText, 13-listItems, 14-fileFilter, 15-min, 16-max, 17-format, 18-unit
        // Min/Max jsou InvariantCulture decimaly (tečka).
        data["1"] = new CList("FIELD", "1", "cena", "Cena", "DECIMAL", "5.5", "0", "0",
            "", "1", "0", "", "", "", "0.5", "9.5", "N2", "mm");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(0.5m, f.Min);
        Assert.Equal(9.5m, f.Max);
        Assert.Equal("N2", f.Format);
        Assert.Equal("mm", f.Unit);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsMinMaxFormatUnit()
    {
        var field = new Field
        {
            Number = 1, Key = "cena", Label = "Cena", FieldType = FieldType.Decimal,
            Value = "5.5", Min = 0.5m, Max = 9.5m, Format = "N2", Unit = "mm"
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(0.5m, f.Min);
        Assert.Equal(9.5m, f.Max);
        Assert.Equal("N2", f.Format);
        Assert.Equal("mm", f.Unit);
    }

    [Fact]
    public void ParseItems_ParsesComboBoxListItems()
    {
        var data = new CVar();
        // Position 13 = listItems - ComboBox sdílí storage s List.
        data["1"] = new CList("FIELD", "1", "mesto", "Město", "COMBOBOX", "Praha", "0", "0",
            "", "1", "0", "", "Praha|Brno|Ostrava");

        var items = EDInputForm.EDInputFormWire.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(FieldType.ComboBox, f.FieldType);
        Assert.Equal(3, f.ListItems.Count);
        Assert.Equal("Praha", f.ListItems[0].Key);
        Assert.Equal("Brno", f.ListItems[1].Key);
        Assert.Equal("Ostrava", f.ListItems[2].Key);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsComboBoxListItems()
    {
        var field = new Field
        {
            Number = 1, Key = "mesto", Label = "Město", FieldType = FieldType.ComboBox,
            Value = "Praha", ListItems = [new ListItem("Praha", "Praha"), new ListItem("Brno", "Brno")]
        };

        CVar serialized = EDInputForm.EDInputFormWire.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputFormWire.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(FieldType.ComboBox, f.FieldType);
        Assert.Equal(2, f.ListItems.Count);
        Assert.Equal("Praha", f.ListItems[0].Key);
        Assert.Equal("Brno", f.ListItems[1].Key);
    }
}

/// <summary>
/// Drátový kontrakt příznaku „pole přijímá {POLE} placeholdery" (sloupec 19). Přidával se
/// **aditivně** - starší server ho neposílá a čtečka to musí přežít, jinak by nová EDControls
/// rozbila formuláře v EDISONu.
/// </summary>
public class EDInputFormPlaceholderWireTests
{
    private static Field ParseSingleField(CList row)
    {
        var data = new CVar();
        data["1"] = new CVar(row);
        return Assert.IsType<Field>(Assert.Single(EDInputForm.EDInputFormWire.ParseItems(data)));
    }

    private static CList FieldRow(params object[] tail)
    {
        var head = new object[] { "FIELD", 1, "Text", "Obsah", "TEXT", "", "0", "0", "", "1", "0", "" };
        return new CList([.. head, .. tail]);
    }

    [Fact]
    public void OlderServerWithoutColumn_DoesNotBreakParsing()
    {
        // 12 sloupců = stav před rozšířením; příznak musí vyjít jako vypnutý, ne spadnout.
        var field = ParseSingleField(FieldRow());

        Assert.False(field.Placeholders);
    }

    [Fact]
    public void ParseItems_CarriesPlaceholdersFlag()
    {
        // 13-18 = listItems, fileFilter, min, max, format, unit; 19 = placeholders
        var field = ParseSingleField(FieldRow("", "", "", "", "", "", "1"));

        Assert.True(field.Placeholders);
    }

    /// <summary>
    /// Formulář chodí tam a zpět (Validate), takže příznak musí přežít i **zpáteční** cestu.
    /// Caché ho zpočátku při čtení zahazovalo (četlo jen po pozici 18) → tlačítko „vložit pole"
    /// se po prvním kole schovalo. Tenhle test hlídá .NET stranu round-tripu.
    /// </summary>
    [Fact]
    public void PlaceholdersFlag_SurvivesRoundTrip()
    {
        var sent = new Field { Key = "Text", Label = "Obsah", FieldType = FieldType.Text, Placeholders = true };

        var wire = EDInputForm.EDInputFormWire.SerializeItemsCVar([sent]);
        var returned = Assert.IsType<Field>(Assert.Single(EDInputForm.EDInputFormWire.ParseItems(wire)));

        Assert.True(returned.Placeholders);
    }

    /// <summary>
    /// Po drátě jede jen příznak, ne seznam nabízených polí - ten zná jen klient
    /// (<c>EDInputForm.PlaceholderFields</c>) a server by ho na zpáteční cestě přepsal prázdnem.
    /// </summary>
    [Fact]
    public void PlaceholderFields_NotSentOverWire()
    {
        var sent = new Field { Key = "Text", Label = "Obsah", FieldType = FieldType.Text, Placeholders = true };

        var wire = EDInputForm.EDInputFormWire.SerializeItemsCVar([sent]);
        var returned = Assert.IsType<Field>(Assert.Single(EDInputForm.EDInputFormWire.ParseItems(wire)));

        Assert.Empty(returned.ListItems);
    }
}
