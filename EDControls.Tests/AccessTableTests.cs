using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// <see cref="AccessTable.Contains"/> je autorizační short-circuit (filtruje strom modulů
/// i runtime checky). Admin (<see cref="AccessTable.IsAllAccess"/>) obchází whitelist úplně.
/// </summary>
public class AccessTableTests
{
    [Fact]
    public void Contains_Admin_AlwaysTrue_EvenForUnlistedElement()
    {
        var table = new AccessTable([], isAllAccess: true);

        Assert.True(table.Contains("anything"));
    }

    [Fact]
    public void Contains_NonAdmin_AllowsWhitelistedElement()
    {
        var table = new AccessTable(["100", "200"], isAllAccess: false);

        Assert.True(table.Contains("100"));
    }

    [Fact]
    public void Contains_NonAdmin_DeniesUnlistedElement()
    {
        var table = new AccessTable(["100"], isAllAccess: false);

        Assert.False(table.Contains("999"));
    }

    [Fact]
    public void Contains_NonAdmin_EmptyWhitelist_DeniesAll()
    {
        var table = new AccessTable([], isAllAccess: false);

        Assert.False(table.Contains("100"));
    }
}
