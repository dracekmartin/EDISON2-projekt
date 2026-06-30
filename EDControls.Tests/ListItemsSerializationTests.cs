using EDControls;
using EDControls.EDInputForm;

namespace EDControls.Tests;

/// <summary>
/// Testy round-tripu ListItems wire formátu (oddělovač položek <c>|</c>, klíč~popisek <c>~</c>).
/// </summary>
public class ListItemsSerializationTests
{
    [Fact]
    public void Serialize_KeyAndLabel_JoinsWithSeparators()
    {
        var wire = EDInputForm.EDInputForm.SerializeListItems(
            [new ListItem("A", "Aktivní"), new ListItem("N", "Neaktivní")]);

        Assert.Equal("A~Aktivní|N~Neaktivní", wire);
    }

    [Fact]
    public void Serialize_LabelEqualsKey_OmitsLabel()
    {
        var wire = EDInputForm.EDInputForm.SerializeListItems([new ListItem("X", "X")]);

        Assert.Equal("X", wire);   // bez "~X"
    }

    [Fact]
    public void Serialize_Empty_ReturnsEmptyString()
    {
        Assert.Equal("", EDInputForm.EDInputForm.SerializeListItems([]));
    }

    [Fact]
    public void Parse_RoundTripsSerialized()
    {
        var items = new List<ListItem> { new("A", "Aktivní"), new("N", "Neaktivní") };

        var roundTripped = EDInputForm.EDInputForm.ParseListItems(EDInputForm.EDInputForm.SerializeListItems(items));

        Assert.Equal(items, roundTripped);
    }

    [Fact]
    public void Parse_EntryWithoutSeparator_LabelDefaultsToKey()
    {
        var items = EDInputForm.EDInputForm.ParseListItems("CB");

        var item = Assert.Single(items);
        Assert.Equal("CB", item.Key);
        Assert.Equal("CB", item.Label);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(EDInputForm.EDInputForm.ParseListItems(""));
    }
}
