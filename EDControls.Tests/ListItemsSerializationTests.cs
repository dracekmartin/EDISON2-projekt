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
        var wire = EDInputForm.EDInputFormWire.SerializeListItems(
            [new ListItem("A", "Aktivní"), new ListItem("N", "Neaktivní")]);

        Assert.Equal("A~Aktivní|N~Neaktivní", wire);
    }

    [Fact]
    public void Serialize_LabelEqualsKey_OmitsLabel()
    {
        var wire = EDInputForm.EDInputFormWire.SerializeListItems([new ListItem("X", "X")]);

        Assert.Equal("X", wire);   // bez "~X"
    }

    [Fact]
    public void Serialize_Empty_ReturnsEmptyString()
    {
        Assert.Equal("", EDInputForm.EDInputFormWire.SerializeListItems([]));
    }

    [Fact]
    public void Parse_RoundTripsSerialized()
    {
        var items = new List<ListItem> { new("A", "Aktivní"), new("N", "Neaktivní") };

        var roundTripped = EDInputForm.EDInputFormWire.ParseListItems(EDInputForm.EDInputFormWire.SerializeListItems(items));

        Assert.Equal(items, roundTripped);
    }

    [Fact]
    public void Parse_EntryWithoutSeparator_LabelDefaultsToKey()
    {
        var items = EDInputForm.EDInputFormWire.ParseListItems("CB");

        var item = Assert.Single(items);
        Assert.Equal("CB", item.Key);
        Assert.Equal("CB", item.Label);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(EDInputForm.EDInputFormWire.ParseListItems(""));
    }

    [Fact]
    public void Parse_EmptyEntriesSkipped()
    {
        // "A||B" → prázdná položka mezi oddělovači se zahodí (Where entry.Length > 0).
        Assert.Equal(2, EDInputForm.EDInputFormWire.ParseListItems("A||B").Count);
        // Trailing oddělovač nevytvoří prázdnou položku.
        Assert.Single(EDInputForm.EDInputFormWire.ParseListItems("A|"));
    }

    [Fact]
    public void Parse_LeadingSeparator_EmptyKey()
    {
        // "~Label" → IndexOf('~') == 0 → prázdný klíč, popisek "Label".
        var item = Assert.Single(EDInputForm.EDInputFormWire.ParseListItems("~Label"));
        Assert.Equal("", item.Key);
        Assert.Equal("Label", item.Label);
    }

    [Fact]
    public void Parse_LabelWithPipe_CorruptsRoundTrip()
    {
        // Známý limit wire formátu: oddělovač položek '|' v popisku se neescapuje, takže
        // round-trip se rozpadne na víc položek ("A~a|b" → "A"/"a" + "b"/"b"). Dokumentační
        // pin - produkci NEměnit (escaping by změnil kontrakt na obou stranách).
        var original = new List<ListItem> { new("A", "a|b") };

        var wire = EDInputForm.EDInputFormWire.SerializeListItems(original);
        var roundTripped = EDInputForm.EDInputFormWire.ParseListItems(wire);

        Assert.NotEqual(original, roundTripped);
        Assert.Equal(2, roundTripped.Count);
    }
}
