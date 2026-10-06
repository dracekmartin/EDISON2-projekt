namespace EDControls.Tests;

/// <summary>Konvence výstupu legacy endpointů: index 0 je ERR slot, data od indexu 1.</summary>
public class LegacyOutputTests
{
    private const char D = '~';

    [Fact]
    public void Values_SkipErrSlot_NullBecomesEmpty()
    {
        object?[] output = ["1~chyba", "apId", null, "26001"];

        Assert.Equal(["apId", "", "26001"], LegacyOutput.Values(output));
    }

    [Fact]
    public void Rows_SkipErrSlotAndEmptyRows()
    {
        // ERR slot nese chybový text ve stejném tvaru jako data - nesmí se vydávat za řádek.
        object?[] output = [$"database{D}ERR", $"database{D}ALVA", "", null, $"pending{D}1"];

        var rows = LegacyOutput.Rows(output, D).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(["database", "ALVA"], rows[0]);
        Assert.Equal(["pending", "1"], rows[1]);
    }

    [Fact]
    public void Named_ValueAfterKeyOfFirstMatchingRow()
    {
        object?[] output = ["", "PocZam", $"PocZam{D}12", $"PocZam{D}99"];

        // Řádek jen s klíčem (bez hodnoty) se nepočítá.
        Assert.Equal("12", LegacyOutput.Named(output, D, "PocZam"));
    }

    [Fact]
    public void Named_MissingKey_ReturnsNull()
    {
        Assert.Null(LegacyOutput.Named(["", $"jine{D}1"], D, "PocZam"));
    }

    [Fact]
    public void EmptyOrNullOutput_YieldsNothing()
    {
        Assert.Empty(LegacyOutput.Values(null));
        Assert.Empty(LegacyOutput.Rows([""], D));
        Assert.Null(LegacyOutput.Named([], D, "x"));
    }
}
