using EDControls.EDList;

namespace EDControls.Tests;

public class EDListFilterTests
{
    private static EDListRow Row(params (string Key, string Value)[] cells)
    {
        var dict = cells.ToDictionary(c => c.Key, c => (string?)c.Value);
        return new EDListRow("id1", dict);
    }

    [Fact]
    public void EmptySearch_MatchesAll()
    {
        var row = Row(("name", "Praha"), ("count", "42"));
        Assert.True(EDListFilter.Matches(row, "", ["name", "count"]));
        Assert.True(EDListFilter.Matches(row, null, ["name", "count"]));
        Assert.True(EDListFilter.Matches(row, "   ", ["name", "count"]));
    }

    [Fact]
    public void Match_AnyColumn_Substring()
    {
        var row = Row(("name", "Praha hl.n."), ("count", "42"));
        Assert.True(EDListFilter.Matches(row, "Pra",     ["name", "count"]));
        Assert.True(EDListFilter.Matches(row, "hl",      ["name", "count"]));
        Assert.True(EDListFilter.Matches(row, "42",      ["name", "count"]));
    }

    [Fact]
    public void NoMatch_ReturnsFalse()
    {
        var row = Row(("name", "Praha"), ("count", "42"));
        Assert.False(EDListFilter.Matches(row, "Brno", ["name", "count"]));
    }

    [Fact]
    public void CaseInsensitive()
    {
        var row = Row(("name", "Praha"));
        Assert.True(EDListFilter.Matches(row, "praha", ["name"]));
        Assert.True(EDListFilter.Matches(row, "PRAHA", ["name"]));
        Assert.True(EDListFilter.Matches(row, "PrAhA", ["name"]));
    }

    [Fact]
    public void NullCellValue_Skipped()
    {
        var row = new EDListRow("id1", new Dictionary<string, string?>
        {
            ["a"] = null,
            ["b"] = "match",
        });
        Assert.True(EDListFilter.Matches(row, "match", ["a", "b"]));
        Assert.False(EDListFilter.Matches(row, "missing", ["a", "b"]));
    }

    [Fact]
    public void MissingColumnKey_Skipped()
    {
        var row = Row(("name", "Praha"));
        // klíč "nonexistent" v řádku není — Matches ho prostě přeskočí
        Assert.True(EDListFilter.Matches(row, "Pra", ["nonexistent", "name"]));
        Assert.False(EDListFilter.Matches(row, "Brno", ["nonexistent"]));
    }

    [Fact]
    public void Whitespace_TrimmedFromSearch()
    {
        var row = Row(("name", "Praha"));
        Assert.True(EDListFilter.Matches(row, "  Pra  ", ["name"]));
    }

    [Fact]
    public void EmptyColumnKeys_NoMatch()
    {
        var row = Row(("name", "Praha"));
        Assert.False(EDListFilter.Matches(row, "Pra", []));
    }
}
