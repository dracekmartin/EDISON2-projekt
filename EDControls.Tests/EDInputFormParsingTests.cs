using ALWA.SYS;
using EDControls.EDInputForm;

namespace EDControls.Tests;

public class EDInputFormParsingTests
{
    [Fact]
    public void ParseItems_EmptyOutput_ReturnsEmptyList()
    {
        var items = EDInputForm.EDInputForm.ParseItems(new CVar());

        Assert.Empty(items);
    }

    [Fact]
    public void ParseItems_SkipsERRKey()
    {
        var data = new CVar();
        data["ERR"] = new CVar("some error metadata");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        Assert.Empty(items);
    }

    [Fact]
    public void ParseItems_ParsesGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "osobni", "Osobní údaje");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(1, g.Number);
        Assert.Equal("osobni", g.Key);
        Assert.Equal("Osobní údaje", g.GroupName);
        Assert.Equal(EItemVisibility.Visible, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesCollapsedGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "5", "skryta", "Skrytá", "1");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(5, g.Number);
        Assert.Equal(EItemVisibility.Collapsed, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesGroupWithProgrammerHidden()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "G", "3");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(EItemVisibility.ProgrammerHidden, g.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesTextField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "2", "jmeno", "Jméno", "TEXT", "Jan", "0", "1");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(2, f.Number);
        Assert.Equal("jmeno", f.Key);
        Assert.Equal("Jméno", f.Label);
        Assert.Equal(EFieldType.Text, f.FieldType);
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

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.Number, f.FieldType);
        Assert.Equal("42", f.Value);
        Assert.True(f.ValidateOnChange);
        Assert.False(f.Required);
    }

    [Fact]
    public void ParseItems_EmptyValueBecomesNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.Value);
    }

    [Fact]
    public void ParseItems_ParsesValidationError()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0", "Povinné pole");

        var items = EDInputForm.EDInputForm.ParseItems(data);

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

        var items = EDInputForm.EDInputForm.ParseItems(data);

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

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.ParentGroup);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsGroupAndField()
    {
        var group = new Group { Number = 1, Key = "sekce", GroupName = "Sekce" };
        var field = new Field
        {
            Number = 2, Key = "k", Label = "Lbl", FieldType = EFieldType.Text,
            Value = "hello", ValidateOnChange = true, Required = true
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([group, field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        Assert.Equal(2, parsed.Count);

        var g = Assert.IsType<Group>(parsed[0]);
        Assert.Equal(1, g.Number);
        Assert.Equal("sekce", g.Key);
        Assert.Equal("Sekce", g.GroupName);
        Assert.Equal(EItemVisibility.Visible, g.Visibility);

        var f = Assert.IsType<Field>(parsed[1]);
        Assert.Equal(2, f.Number);
        Assert.Equal("k", f.Key);
        Assert.Equal("Lbl", f.Label);
        Assert.Equal(EFieldType.Text, f.FieldType);
        Assert.Equal("hello", f.Value);
        Assert.True(f.ValidateOnChange);
        Assert.True(f.Required);
    }

    [Fact]
    public void ParseItems_ParsesDateField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "narozen", "Narozen", "DATE", "2024-05-15", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.Date, f.FieldType);
        Assert.Equal("2024-05-15", f.Value);
    }

    [Fact]
    public void ParseItems_ParsesDecimalField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "plat", "Plat", "DECIMAL", "45000.50", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.Decimal, f.FieldType);
        Assert.Equal("45000.50", f.Value);
    }

    [Fact]
    public void ParseItems_ParsesBoolField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "aktivni", "Aktivní", "BOOL", "1", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.Bool, f.FieldType);
        Assert.Equal("1", f.Value);
        Assert.True(f.BoolValue);
    }

    [Fact]
    public void BoolValue_SetTrue_SetsValueTo1()
    {
        var field = new Field { FieldType = EFieldType.Bool, Value = "0" };

        field.BoolValue = true;

        Assert.Equal("1", field.Value);
        Assert.True(field.BoolValue);
    }

    [Fact]
    public void BoolValue_SetFalse_SetsValueTo0()
    {
        var field = new Field { FieldType = EFieldType.Bool, Value = "1" };

        field.BoolValue = false;

        Assert.Equal("0", field.Value);
        Assert.False(field.BoolValue);
    }

    [Theory]
    [InlineData("DATE", EFieldType.Date)]
    [InlineData("DECIMAL", EFieldType.Decimal)]
    [InlineData("BOOL", EFieldType.Bool)]
    [InlineData("TEXT", EFieldType.Text)]
    [InlineData("NUMBER", EFieldType.Number)]
    [InlineData("BUTTON", EFieldType.Button)]
    [InlineData("LIST", EFieldType.List)]
    [InlineData("FILE", EFieldType.File)]
    [InlineData("UNKNOWN", EFieldType.Text)]
    public void EFieldTypeWire_RoundTrips(string wire, EFieldType expected)
    {
        Assert.Equal(expected, EFieldTypeWire.FromWire(wire));
        if (wire != "UNKNOWN")
            Assert.Equal(wire, expected.ToWire());
    }

    [Fact]
    public void DateValue_Get_ParsesYyyyMmDd()
    {
        var field = new Field { FieldType = EFieldType.Date, Value = "2024-05-15" };
        Assert.Equal(new System.DateTime(2024, 5, 15), field.DateValue);
    }

    [Fact]
    public void DateValue_Get_InvalidReturnsNull()
    {
        var field = new Field { FieldType = EFieldType.Date, Value = "not-a-date" };
        Assert.Null(field.DateValue);
    }

    [Fact]
    public void DateValue_Set_FormatsToWire()
    {
        var field = new Field { FieldType = EFieldType.Date };
        field.DateValue = new System.DateTime(2024, 12, 31);
        Assert.Equal("2024-12-31", field.Value);
    }

    [Fact]
    public void DateValue_SetNull_ClearsValue()
    {
        var field = new Field { FieldType = EFieldType.Date, Value = "2024-05-15" };
        field.DateValue = null;
        Assert.Equal("", field.Value);
    }

    [Theory]
    [InlineData(EFieldType.Number, "42", null)]
    [InlineData(EFieldType.Number, "-5", null)]
    [InlineData(EFieldType.Number, "3.14", "Musí být celé číslo")]
    [InlineData(EFieldType.Number, "abc", "Musí být celé číslo")]
    [InlineData(EFieldType.Decimal, "42", null)]
    [InlineData(EFieldType.Decimal, "3.14", null)]
    [InlineData(EFieldType.Decimal, "-1.5", null)]
    [InlineData(EFieldType.Decimal, "abc", "Musí být číslo")]
    [InlineData(EFieldType.Date, "2024-05-15", null)]
    [InlineData(EFieldType.Date, "not-a-date", "Neplatné datum")]
    [InlineData(EFieldType.Bool, "1", null)]
    [InlineData(EFieldType.Bool, "0", null)]
    [InlineData(EFieldType.Bool, "yes", "Musí být 0 nebo 1")]
    [InlineData(EFieldType.Text, "anything", null)]
    [InlineData(EFieldType.List, "anything", null)]
    [InlineData(EFieldType.File, @"C:\some\path.pdf", null)]
    public void GetTypeError_ValidatesValueByFieldType(EFieldType type, string value, string? expected)
    {
        var field = new Field { FieldType = type, Value = value };
        Assert.Equal(expected, field.GetTypeError());
    }

    [Theory]
    [InlineData(EFieldType.Number, null)]
    [InlineData(EFieldType.Decimal, null)]
    [InlineData(EFieldType.Date, null)]
    [InlineData(EFieldType.Bool, null)]
    [InlineData(EFieldType.List, null)]
    [InlineData(EFieldType.File, null)]
    public void GetTypeError_EmptyValue_ReturnsNull(EFieldType type, string? value)
    {
        var field = new Field { FieldType = type, Value = value };
        Assert.Null(field.GetTypeError());
    }

    [Fact]
    public void ParseItems_ParsesNonEditableField()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0", "", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.False(f.Editable);
    }

    [Fact]
    public void ParseItems_FieldWithoutEditableDefaultsToTrue()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.True(f.Editable);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsEditableField()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = EFieldType.Text,
            Value = "v", Editable = false
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.False(f.Editable);
    }

    [Fact]
    public void SerializeItemsCVar_NullItems_ReturnsEmptyCVar()
    {
        CVar result = EDInputForm.EDInputForm.SerializeItemsCVar(null);

        int count = 0;
        foreach (var _ in result) count++;
        Assert.Equal(0, count);
    }

    // ─── Visibility tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData("0", EItemVisibility.Visible)]
    [InlineData("1", EItemVisibility.Collapsed)]
    [InlineData("2", EItemVisibility.UserHidden)]
    [InlineData("3", EItemVisibility.ProgrammerHidden)]
    [InlineData("UNKNOWN", EItemVisibility.Visible)]
    public void EItemVisibilityWire_RoundTrips(string wire, EItemVisibility expected)
    {
        Assert.Equal(expected, EItemVisibilityWire.FromWire(wire));
        if (wire != "UNKNOWN")
            Assert.Equal(wire, expected.ToWire());
    }

    [Fact]
    public void ParseItems_ParsesFieldWithUserHidden()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "2");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EItemVisibility.UserHidden, f.Visibility);
    }

    [Fact]
    public void ParseItems_ParsesFieldWithProgrammerHidden()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "3");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EItemVisibility.ProgrammerHidden, f.Visibility);
    }

    [Fact]
    public void ParseItems_FieldWithoutVisibilityDefaultsToVisible()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EItemVisibility.Visible, f.Visibility);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFieldVisibility()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = EFieldType.Text,
            Value = "v", Visibility = EItemVisibility.ProgrammerHidden
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(EItemVisibility.ProgrammerHidden, f.Visibility);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsGroupVisibility()
    {
        var group = new Group
        {
            Number = 1, Key = "g", GroupName = "G",
            Visibility = EItemVisibility.Collapsed
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([group]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var g = Assert.IsType<Group>(Assert.Single(parsed));
        Assert.Equal(EItemVisibility.Collapsed, g.Visibility);
    }

    [Fact]
    public void EffectiveVisibility_HiddenField_IsCollapsed()
    {
        var field = new Field { Visibility = EItemVisibility.ProgrammerHidden };
        Assert.Equal(System.Windows.Visibility.Collapsed, field.EffectiveVisibility);
    }

    [Fact]
    public void EffectiveVisibility_VisibleFieldNoParent_IsVisible()
    {
        var field = new Field { Visibility = EItemVisibility.Visible };
        Assert.Equal(System.Windows.Visibility.Visible, field.EffectiveVisibility);
    }

    // ─── HelpText tests ──────────────────────────────────────────────────────

    [Fact]
    public void ParseItems_ParsesFieldHelpText()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0", "Zadejte hodnotu");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal("Zadejte hodnotu", f.HelpText);
    }

    [Fact]
    public void ParseItems_ParsesGroupHelpText()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "Skupina", "0", "Vyplňte tuto sekci");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal("Vyplňte tuto sekci", g.HelpText);
    }

    [Fact]
    public void ParseItems_FieldWithoutHelpTextDefaultsToNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.HelpText);
    }

    [Fact]
    public void ParseItems_GroupWithoutHelpTextDefaultsToNull()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "1", "g", "Skupina", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Null(g.HelpText);
    }

    [Fact]
    public void ParseItems_FieldEmptyHelpTextBecomesNull()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0", "", "1", "0", "");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.HelpText);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFieldHelpText()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = EFieldType.Text,
            Value = "v", HelpText = "Help for field"
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

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

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([group]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var g = Assert.IsType<Group>(Assert.Single(parsed));
        Assert.Equal("Help for group", g.HelpText);
    }

    [Fact]
    public void SerializeItemsCVar_NullHelpText_RoundTripsAsNull()
    {
        var field = new Field
        {
            Number = 1, Key = "k", Label = "L", FieldType = EFieldType.Text,
            Value = "v", HelpText = null
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

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

    // ─── HelpTextMode tests ──────────────────────────────────────────────────

    [Fact]
    public void ParseHelpTextMode_Tooltip_ReturnsTooltip()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("tooltip");

        Assert.Equal(EHelpTextMode.Tooltip, EDInputForm.EDInputForm.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseHelpTextMode_Column_ReturnsColumn()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("column");

        Assert.Equal(EHelpTextMode.Column, EDInputForm.EDInputForm.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseHelpTextMode_Missing_DefaultsToTooltip()
    {
        var data = new CVar();

        Assert.Equal(EHelpTextMode.Tooltip, EDInputForm.EDInputForm.ParseHelpTextMode(data));
    }

    [Fact]
    public void ParseItems_SkipsHelpTextModeKey()
    {
        var data = new CVar();
        data["helpTextMode"] = new CVar("column");
        data["1"] = new CList("FIELD", "1", "k", "L", "TEXT", "v", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        Assert.Single(items);
        Assert.IsType<Field>(items[0]);
    }

    [Theory]
    [InlineData("tooltip", EHelpTextMode.Tooltip)]
    [InlineData("column", EHelpTextMode.Column)]
    [InlineData("UNKNOWN", EHelpTextMode.Tooltip)]
    public void EHelpTextModeWire_FromWire(string wire, EHelpTextMode expected)
    {
        Assert.Equal(expected, EHelpTextModeWire.FromWire(wire));
    }

    [Fact]
    public void EHelpTextModeWire_RoundTrips()
    {
        Assert.Equal("tooltip", EHelpTextMode.Tooltip.ToWire());
        Assert.Equal("column", EHelpTextMode.Column.ToWire());
    }

    // ─── List field tests ─────────────────────────────────────────────────────

    [Fact]
    public void ParseItems_ParsesListField()
    {
        var data = new CVar();
        // Position 12 = helpText (empty), position 13 = listItems
        data["1"] = new CList("FIELD", "1", "stav", "Stav", "LIST", "A", "0", "0", "", "1", "0", "",
            "A~Aktivní|B~Neaktivní");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.List, f.FieldType);
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

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.List, f.FieldType);
        Assert.Empty(f.ListItems);
    }

    [Fact]
    public void ParseItems_ListFieldKeyOnlyOptions()
    {
        var data = new CVar();
        // Position 12 = helpText (empty), position 13 = listItems (key-only)
        data["1"] = new CList("FIELD", "1", "barva", "Barva", "LIST", "Červená", "0", "0", "", "1", "0", "",
            "Červená|Zelená|Modrá");

        var items = EDInputForm.EDInputForm.ParseItems(data);

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
            Number = 1, Key = "stav", Label = "Stav", FieldType = EFieldType.List,
            Value = "A", ListItems = [new ListItem("A", "Aktivní"), new ListItem("N", "Neaktivní")]
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(EFieldType.List, f.FieldType);
        Assert.Equal("A", f.Value);
        Assert.Equal(2, f.ListItems.Count);
        Assert.Equal("A", f.ListItems[0].Key);
        Assert.Equal("Aktivní", f.ListItems[0].Label);
        Assert.Equal("N", f.ListItems[1].Key);
        Assert.Equal("Neaktivní", f.ListItems[1].Label);
    }

    // ─── File field tests ─────────────────────────────────────────────────────

    [Fact]
    public void ParseItems_ParsesFileFieldWithFilter()
    {
        var data = new CVar();
        // positions: 1-type, 2-number, 3-key, 4-label, 5-fieldType, 6-value, 7-voc, 8-req,
        //            9-valErr, 10-editable, 11-visibility, 12-helpText, 13-listItems, 14-fileFilter
        data["1"] = new CList("FIELD", "1", "soubor", "Soubor", "FILE", @"C:\test.pdf", "0", "0",
            "", "1", "0", "", "", "PDF (*.pdf)|*.pdf");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.File, f.FieldType);
        Assert.Equal(@"C:\test.pdf", f.Value);
        Assert.Equal("PDF (*.pdf)|*.pdf", f.FileFilter);
    }

    [Fact]
    public void ParseItems_ParsesFileFolderMode()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "slozka", "Složka", "FILE", @"C:\Output", "0", "0",
            "", "1", "0", "", "", "$FOLDER");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.File, f.FieldType);
        Assert.Equal(@"C:\Output", f.Value);
        Assert.Equal("$FOLDER", f.FileFilter);
    }

    [Fact]
    public void ParseItems_FileFieldWithoutFilter_DefaultsToEmpty()
    {
        var data = new CVar();
        data["1"] = new CList("FIELD", "1", "soubor", "Soubor", "FILE", "", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.File, f.FieldType);
        Assert.Equal("", f.FileFilter);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFileField()
    {
        var field = new Field
        {
            Number = 1, Key = "soubor", Label = "Soubor", FieldType = EFieldType.File,
            Value = @"C:\test.pdf", FileFilter = "PDF (*.pdf)|*.pdf"
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(EFieldType.File, f.FieldType);
        Assert.Equal(@"C:\test.pdf", f.Value);
        Assert.Equal("PDF (*.pdf)|*.pdf", f.FileFilter);
    }

    [Fact]
    public void SerializeItemsCVar_RoundTripsFileFolderMode()
    {
        var field = new Field
        {
            Number = 1, Key = "slozka", Label = "Složka", FieldType = EFieldType.File,
            Value = @"C:\Output", FileFilter = "$FOLDER"
        };

        CVar serialized = EDInputForm.EDInputForm.SerializeItemsCVar([field]);
        var parsed = EDInputForm.EDInputForm.ParseItems(serialized);

        var f = Assert.IsType<Field>(Assert.Single(parsed));
        Assert.Equal(EFieldType.File, f.FieldType);
        Assert.Equal(@"C:\Output", f.Value);
        Assert.Equal("$FOLDER", f.FileFilter);
    }
}
