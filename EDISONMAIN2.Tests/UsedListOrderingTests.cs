using EDISONMAIN2.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;

namespace EDISONMAIN2.Tests;

/// <summary>Řazení záložky Používané s deterministickými časy (RecordUsage bere DateTime.UtcNow).</summary>
public class UsedListOrderingTests
{
    private static ExecutableModule Exe(string id) =>
        new() { Name = id, ElementId = id, ExecutablePath = id + ".exe" };

    [Fact]
    public void BuildUsedList_OrdersByCountThenLastUsed_CapsAndSkipsModulesOutsideTree()
    {
        var trees = new List<IEnumerable<IModule>> { new IModule[] { Exe("1"), Exe("2"), Exe("3") } };
        var t0 = new DateTime(2026, 9, 8, 8, 0, 0, DateTimeKind.Utc);
        var usage = new Dictionary<string, UsageEntry>
        {
            ["1"] = new(Count: 1, LastUsedUtc: t0.AddHours(2)),  // stejný count jako "2", novější → dřív
            ["2"] = new(Count: 1, LastUsedUtc: t0),
            ["3"] = new(Count: 5, LastUsedUtc: t0),              // vyšší count vede bez ohledu na čas
            ["99"] = new(Count: 9, LastUsedUtc: t0),             // mimo strom (licence/přístup) → přeskočit
        };

        var used = EdisonMainViewModel.BuildUsedList(trees, usage, cap: 2);

        Assert.Equal(["3", "1"], used.Select(m => m.ElementId));
    }
}
