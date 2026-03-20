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
        data["1"] = new CList("GROUP", "1", "Osobní údaje");

        var items = EDInputForm.EDInputForm.ParseItems(data);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(1, g.Number);
        Assert.Equal("Osobní údaje", g.GroupName);
        Assert.True(g.IsVisible);
    }

    [Fact]
    public void ParseItems_ParsesHiddenGroup()
    {
        var data = new CVar();
        data["1"] = new CList("GROUP", "5", "Skrytá", "0");

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
        data["1"] = new CList("GROUP", "1", "Sekce A");
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
        var group = new Group { Number = 1, GroupName = "Sekce", IsVisible = true };
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
    public void SerializeItemsCVar_NullItems_ReturnsEmptyCVar()
    {
        CVar result = EDInputForm.EDInputForm.SerializeItemsCVar(null);

        int count = 0;
        foreach (var _ in result) count++;
        Assert.Equal(0, count);
    }
}
