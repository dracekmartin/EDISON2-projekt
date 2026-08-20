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
    public void GetSortKey_NoSortKeys_FallsBackToFormattedValue()
    {
        // 2-arg ctor (testy / ruční konstrukce) nemá typované klíče → formátovaná hodnota.
        var row = new EDListRow("1", new() { ["k"] = "Praha" });
        Assert.Equal("Praha", row.GetSortKey("k"));
    }

    [Fact]
    public void GetSortKey_WithSortKeys_ReturnsTypedKey()
    {
        var row = new EDListRow("1",
            new Dictionary<string, string?> { ["k"] = "142,5" },
            new Dictionary<string, object?> { ["k"] = 142.5m });
        Assert.Equal(142.5m, row.GetSortKey("k"));
    }
}
