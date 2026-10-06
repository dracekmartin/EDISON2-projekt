using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Persistence rozbalení: klíč uzlu je index-cesta per strom. Změna formátu klíče
/// tiše zahodí uložený stav všech uživatelů - proto je tu zafixovaný.
/// </summary>
public class ModuleTreeStateCollectExpandedTests
{
    private static ExecutableModule Exe(string name = "A") =>
        new() { Name = name, ExecutablePath = "a.exe" };

    private static ModuleFamily Fam(string name, bool expanded, params IModule[] kids)
    {
        var f = new ModuleFamily { Name = name, IsExpanded = expanded };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    [Fact]
    public void ExpansionPathKey_IsTreeNameColonDottedIndexes()
    {
        Assert.Equal("main:5.2", ModuleTreeState.ExpansionPathKey("main", [5, 2]));
        Assert.Equal("favorites:0", ModuleTreeState.ExpansionPathKey("favorites", [0]));
    }

    [Fact]
    public void CollectExpanded_CollectsOnlyExpandedFamiliesWithIndexPaths()
    {
        var tree = new List<IModule>
        {
            Fam("Rozbalená", expanded: true,
                Fam("Sbalená", expanded: false, Exe()),
                Fam("Vnořená rozbalená", expanded: true, Exe())),
            Exe("Mimo rodiny"),
            Fam("Sbalený kořen", expanded: false, Exe()),
        };

        var keys = new List<string>();
        ModuleTreeState.CollectExpanded(tree, "main", keys);

        Assert.Equal(["main:0", "main:0.1"], keys);
    }
}
