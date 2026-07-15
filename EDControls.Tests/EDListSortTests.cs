using EDControls.EDList;

namespace EDControls.Tests;

/// <summary>
/// Typové řazení EDList — sort klíče z raw hodnot (EDListSort.BuildSortKey)
/// a EDListRowComparer pro ListCollectionView.CustomSort.
/// </summary>
public class EDListSortTests
{
    // ─── BuildSortKey ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("142.5", "142,5")]
    [InlineData("98", "98")]
    public void BuildSortKey_Number_ParsesInvariantDecimal(string raw, string formatted)
    {
        var key = EDListSort.BuildSortKey(raw, EListColumnType.Number, formatted);
        Assert.IsType<decimal>(key);
    }

    [Fact]
    public void BuildSortKey_Number_UnparseableFallsBackToFormatted()
    {
        var key = EDListSort.BuildSortKey("n/a", EListColumnType.Number, "n/a");
        Assert.Equal("n/a", key);
    }

    [Fact]
    public void BuildSortKey_HHMM_IsNumeric()
    {
        var key = EDListSort.BuildSortKey("830", EListColumnType.HoursMinutes, "08:30");
        Assert.Equal(830m, key);
    }

    [Fact]
    public void BuildSortKey_Date_ParsesIso()
    {
        var key = EDListSort.BuildSortKey("2024-03-15", EListColumnType.Date, "15.03.2024");
        Assert.Equal(new DateTime(2024, 3, 15), key);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void BuildSortKey_Bool(string raw, bool expected)
    {
        Assert.Equal(expected, EDListSort.BuildSortKey(raw, EListColumnType.Bool, "Ano"));
    }

    [Fact]
    public void BuildSortKey_EmptyRaw_IsNull()
    {
        Assert.Null(EDListSort.BuildSortKey("", EListColumnType.Number, ""));
        Assert.Null(EDListSort.BuildSortKey(null, EListColumnType.Text, null));
    }

    // ─── EDListRowComparer ────────────────────────────────────────────────────

    private static EDListRow Row(string id, string col, string raw, EListColumnType type, string? formatted = null)
    {
        formatted ??= raw;
        return new EDListRow(id,
            new Dictionary<string, string?> { [col] = formatted },
            new Dictionary<string, object?> { [col] = EDListSort.BuildSortKey(raw, type, formatted) });
    }

    [Fact]
    public void Number_SortsNumerically_NotLexicographically()
    {
        // Lexikograficky by "142,5" < "98" — typově musí být 98 první
        var a = Row("a", "km", "142.5", EListColumnType.Number, "142,5");
        var b = Row("b", "km", "98", EListColumnType.Number, "98");
        var cmp = new EDListRowComparer([("km", false)]);
        Assert.True(cmp.Compare(b, a) < 0);
    }

    [Fact]
    public void Date_SortsChronologically()
    {
        // Formátovaně "02.01.2025" < "31.12.2024" lexikograficky — chronologicky je to obráceně
        var older = Row("a", "d", "2024-12-31", EListColumnType.Date, "31.12.2024");
        var newer = Row("b", "d", "2025-01-02", EListColumnType.Date, "02.01.2025");
        var cmp = new EDListRowComparer([("d", false)]);
        Assert.True(cmp.Compare(older, newer) < 0);
    }

    [Fact]
    public void Descending_InvertsOrder()
    {
        var small = Row("a", "n", "1", EListColumnType.Number);
        var big   = Row("b", "n", "2", EListColumnType.Number);
        var cmp = new EDListRowComparer([("n", true)]);
        Assert.True(cmp.Compare(big, small) < 0);
    }

    [Fact]
    public void MultiKey_SecondKeyBreaksTies()
    {
        EDListRow Make(string id, string typ, string km) => new(id,
            new Dictionary<string, string?> { ["typ"] = typ, ["km"] = km },
            new Dictionary<string, object?>
            {
                ["typ"] = typ,
                ["km"] = EDListSort.BuildSortKey(km, EListColumnType.Number, km),
            });
        var cmp = new EDListRowComparer([("typ", false), ("km", false)]);
        Assert.True(cmp.Compare(Make("a", "Bus", "98"), Make("b", "Bus", "142.5")) < 0);
        Assert.True(cmp.Compare(Make("a", "Bus", "999"), Make("b", "Vlak", "1")) < 0);
    }

    [Fact]
    public void EmptyValues_SortLast()
    {
        var empty = Row("a", "n", "", EListColumnType.Number);
        var value = Row("b", "n", "5", EListColumnType.Number);
        var cmp = new EDListRowComparer([("n", false)]);
        Assert.True(cmp.Compare(value, empty) < 0);
    }

    [Fact]
    public void RowWithoutSortKeys_FallsBackToFormattedValue()
    {
        // Starý ctor (testy, ruční konstrukce) — porovnává se formátovaný string
        var a = new EDListRow("a", new Dictionary<string, string?> { ["t"] = "Brno" });
        var b = new EDListRow("b", new Dictionary<string, string?> { ["t"] = "Praha" });
        var cmp = new EDListRowComparer([("t", false)]);
        Assert.True(cmp.Compare(a, b) < 0);
    }
}
