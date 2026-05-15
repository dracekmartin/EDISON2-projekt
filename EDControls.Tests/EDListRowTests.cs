using System.ComponentModel;
using EDControls.EDList;

namespace EDControls.Tests;

public class EDListRowTests
{
    [Fact]
    public void Indexer_ReturnsValueForKey()
    {
        var row = new EDListRow("1", new() { ["name"] = "Praha", ["count"] = "42" });
        Assert.Equal("Praha", row["name"]);
        Assert.Equal("42",    row["count"]);
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsNull()
    {
        var row = new EDListRow("1", new() { ["a"] = "x" });
        Assert.Null(row["nonexistent"]);
    }

    [Fact]
    public void Id_StoredCorrectly()
    {
        var row = new EDListRow("abc-123", new());
        Assert.Equal("abc-123", row.Id);
    }

    [Fact]
    public void IsChecked_DefaultsFalse()
    {
        var row = new EDListRow("1", new());
        Assert.False(row.IsChecked);
    }

    [Fact]
    public void IsChecked_Set_FiresPropertyChanged()
    {
        var row = new EDListRow("1", new());
        string? raised = null;
        row.PropertyChanged += (_, e) => raised = e.PropertyName;

        row.IsChecked = true;
        Assert.Equal(nameof(EDListRow.IsChecked), raised);
    }

    [Fact]
    public void IsChecked_SameValue_NoPropertyChanged()
    {
        var row = new EDListRow("1", new()) { IsChecked = true };
        int fired = 0;
        row.PropertyChanged += (_, _) => fired++;

        row.IsChecked = true;  // už je true
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Values_IsReadOnly()
    {
        var row = new EDListRow("1", new() { ["x"] = "y" });
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string?>>(row.Values);
    }
}
