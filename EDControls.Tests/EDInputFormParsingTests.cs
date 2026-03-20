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
        Assert.True(g.IsVisible);
    }

    [Fact]
    public void ParseItems_ParsesHiddenGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "5", "skryta", "Skrytá", "0");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(5, g.Number);
        Assert.False(g.IsVisible);
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
        var group = new Group { Number = 1, Key = "sekce", GroupName = "Sekce", IsVisible = true };
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
        Assert.True(g.IsVisible);

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
}
