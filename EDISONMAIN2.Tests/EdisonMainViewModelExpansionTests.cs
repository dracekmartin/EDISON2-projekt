using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy rekurzivního rozbalování/sbalování stromu (<see cref="EdisonMainViewModel.SetExpandedRecursive"/>),
/// který pohání toolbar „Rozbalit/Sbalit vše" a kontextové „Rozbalit/Sbalit úroveň".
/// </summary>
public class EdisonMainViewModelExpansionTests
{
    private static ModuleFamily Family(string name, bool expanded, params IModule[] children)
    {
        var f = new ModuleFamily { DisplayedName = name, IsExpanded = expanded };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    private static ExecutableModule Exe(string name = "M") =>
        new() { DisplayedName = name };

    [Fact]
    public void SetExpandedRecursive_Expand_SetsFamilyAndAllNestedFamilies()
    {
        var inner = Family("Inner", expanded: false, Exe());
        var root = Family("Root", expanded: false, inner, Exe());

        EdisonMainViewModel.SetExpandedRecursive(root, expanded: true);

        Assert.True(root.IsExpanded);
        Assert.True(inner.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_Collapse_ClearsFamilyAndAllNestedFamilies()
    {
        var inner = Family("Inner", expanded: true, Exe());
        var root = Family("Root", expanded: true, inner);

        EdisonMainViewModel.SetExpandedRecursive(root, expanded: false);

        Assert.False(root.IsExpanded);
        Assert.False(inner.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_DeeplyNested_AllLevelsAffected()
    {
        var level3 = Family("L3", expanded: false);
        var level2 = Family("L2", expanded: false, level3);
        var level1 = Family("L1", expanded: false, level2);

        EdisonMainViewModel.SetExpandedRecursive(level1, expanded: true);

        Assert.True(level1.IsExpanded);
        Assert.True(level2.IsExpanded);
        Assert.True(level3.IsExpanded);
    }

    [Fact]
    public void SetExpandedRecursive_LeafModule_NoOpNoThrow()
    {
        // ExecutableModule nemá IsExpanded — musí se tiše přeskočit (ne spadnout).
        var leaf = Exe();

        var ex = Record.Exception(() => EdisonMainViewModel.SetExpandedRecursive(leaf, expanded: true));

        Assert.Null(ex);
    }

    [Fact]
    public void SetExpandedRecursive_MixedChildren_OnlyFamiliesToggled()
    {
        var childFamily = Family("Child", expanded: false, Exe());
        var root = Family("Root", expanded: false, Exe("leaf1"), childFamily, Exe("leaf2"));

        EdisonMainViewModel.SetExpandedRecursive(root, expanded: true);

        Assert.True(root.IsExpanded);
        Assert.True(childFamily.IsExpanded);
    }
}
