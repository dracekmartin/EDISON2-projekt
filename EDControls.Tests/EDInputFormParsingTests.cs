using ALWA.SYS;
using EDControls.EDInputForm;

namespace EDControls.Tests;

public class EDInputFormParsingTests
{
    [Fact]
    public void ParseItems_EmptyOutput_ReturnsEmptyList()
    {
        var output = new CVar();
        var items = EDInputForm.EDInputForm.ParseItems(output);
        Assert.Empty(items);
    }

    [Fact]
    public void ParseItems_SkipsERRKey()
    {
        var output = new CVar();
        output["ERR"] = new CVar("some error metadata");
        var items = EDInputForm.EDInputForm.ParseItems(output);
        Assert.Empty(items);
    }

    [Fact]
    public void ParseItems_ParsesGroup()
    {
        var output = new CVar();
        output["1"] = new CList("GROUP", "1", "Osobní údaje");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        var group = Assert.Single(items);
        var g = Assert.IsType<Group>(group);
        Assert.Equal(1, g.Number);
        Assert.Equal("Osobní údaje", g.GroupName);
        Assert.True(g.IsVisible);
    }

    [Fact]
    public void ParseItems_ParsesHiddenGroup()
    {
        var output = new CVar();
        output["1"] = new CList("GROUP", "5", "Skrytá", "0");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        var g = Assert.IsType<Group>(Assert.Single(items));
        Assert.Equal(5, g.Number);
        Assert.False(g.IsVisible);
    }

    [Fact]
    public void ParseItems_ParsesTextField()
    {
        var output = new CVar();
        output["1"] = new CList("FIELD", "2", "jmeno", "Jméno", "TEXT", "Jan", "0", "1");

        var items = EDInputForm.EDInputForm.ParseItems(output);

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
        var output = new CVar();
        output["1"] = new CList("FIELD", "3", "vek", "Věk", "NUMBER", "42", "1", "0");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal(EFieldType.Number, f.FieldType);
        Assert.Equal("42", f.Value);
        Assert.True(f.ValidateOnChange);
        Assert.False(f.Required);
    }

    [Fact]
    public void ParseItems_EmptyValueBecomesNull()
    {
        var output = new CVar();
        output["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Null(f.Value);
    }

    [Fact]
    public void ParseItems_ParsesValidationError()
    {
        var output = new CVar();
        output["1"] = new CList("FIELD", "1", "key", "Label", "TEXT", "val", "0", "0", "Povinné pole");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        var f = Assert.IsType<Field>(Assert.Single(items));
        Assert.Equal("Povinné pole", f.ValidationError);
        Assert.True(f.HasError);
    }

    [Fact]
    public void ParseItems_FieldsAfterGroupGetParentGroup()
    {
        var output = new CVar();
        output["1"] = new CList("GROUP", "1", "Sekce A");
        output["2"] = new CList("FIELD", "2", "a1", "Pole A1", "TEXT", "", "0", "0");
        output["3"] = new CList("FIELD", "3", "a2", "Pole A2", "TEXT", "", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(output);

        Assert.Equal(3, items.Count);
        var group = Assert.IsType<Group>(items[0]);
        var f1 = Assert.IsType<Field>(items[1]);
        var f2 = Assert.IsType<Field>(items[2]);
        Assert.Equal("Sekce A", f1.ParentGroup?.GroupName);
        Assert.Equal("Sekce A", f2.ParentGroup?.GroupName);
    }

    [Fact]
    public void ParseItems_FieldsWithoutGroupHaveNullParent()
    {
        var output = new CVar();
        output["1"] = new CList("FIELD", "1", "top", "Top", "TEXT", "val", "0", "0");

        var items = EDInputForm.EDInputForm.ParseItems(output);

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
        var items = new Item[] { group, field };

        CVar result = EDInputForm.EDInputForm.SerializeItemsCVar(items);

        // Verify the CVar has two entries keyed "1" and "2"
        CList g = result["1"];
        Assert.Equal("GROUP", g.GetString(1));
        Assert.Equal("1", g.GetString(2));
        Assert.Equal("Sekce", g.GetString(3));
        Assert.Equal("1", g.GetString(4)); // IsVisible

        CList f = result["2"];
        Assert.Equal("FIELD", f.GetString(1));
        Assert.Equal("2", f.GetString(2));
        Assert.Equal("k", f.GetString(3));
        Assert.Equal("Lbl", f.GetString(4));
        Assert.Equal("TEXT", f.GetString(5));
        Assert.Equal("hello", f.GetString(6));
        Assert.Equal("1", f.GetString(7)); // ValidateOnChange
        Assert.Equal("1", f.GetString(8)); // Required
    }

    [Fact]
    public void SerializeItemsCVar_NullItems_ReturnsEmptyCVar()
    {
        CVar result = EDInputForm.EDInputForm.SerializeItemsCVar(null);

        // Should not throw and should be a valid (empty) CVar
        int count = 0;
        foreach (var _ in result) count++;
        Assert.Equal(0, count);
    }
}
