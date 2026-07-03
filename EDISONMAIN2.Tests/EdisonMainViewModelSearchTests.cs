using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>Testy filtrování stromu podle hledaného textu (EdisonMainViewModel.ApplySearchToModule).</summary>
public class EdisonMainViewModelSearchTests
{
    private static ExecutableModule Exe(string name) => new() { DisplayedName = name };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    [Fact]
    public void EmptyText_AllVisible()
    {
        var exe = Exe("Cokoliv");
        Assert.True(EdisonMainViewModel.ApplySearchToModule(exe, ""));
        Assert.True(exe.IsVisibleBySearch);
    }

    [Theory]
    [InlineData("Objednávky", "obj", true)]    // case-insensitive substring
    [InlineData("Objednávky", "OBJEDN", true)]
    [InlineData("Objednávky", "xyz", false)]
    public void Leaf_MatchesByName(string name, string text, bool expected)
    {
        var exe = Exe(name);
        Assert.Equal(expected, EdisonMainViewModel.ApplySearchToModule(exe, text));
        Assert.Equal(expected, exe.IsVisibleBySearch);
    }

    [Fact]
    public void Family_VisibleAndExpanded_WhenDescendantMatches()
    {
        var match = Exe("Turnusy");
        var other = Exe("Objednávky");
        var fam = Fam("Autobusová doprava", match, other);
        fam.IsExpanded = false;

        var visible = EdisonMainViewModel.ApplySearchToModule(fam, "turnus");

        Assert.True(visible);
        Assert.True(fam.IsVisibleBySearch);
        Assert.True(fam.IsExpanded);            // rozbaleno, aby byl match vidět
        Assert.True(match.IsVisibleBySearch);
        Assert.False(other.IsVisibleBySearch);  // nevyhovující sourozenec skryt
    }

    [Fact]
    public void Family_VisibleByOwnName()
    {
        var fam = Fam("Doprava", Exe("Turnusy"));
        Assert.True(EdisonMainViewModel.ApplySearchToModule(fam, "doprav"));
        Assert.True(fam.IsVisibleBySearch);
    }

    [Fact]
    public void Family_Hidden_WhenNothingMatches()
    {
        var fam = Fam("Doprava", Exe("Turnusy"), Exe("Objednávky"));
        Assert.False(EdisonMainViewModel.ApplySearchToModule(fam, "xyz"));
        Assert.False(fam.IsVisibleBySearch);
    }

    [Fact]
    public void EmptyText_ResetsVisibilityToTrue()
    {
        var match = Exe("Turnusy");
        var other = Exe("Objednávky");
        var fam = Fam("Doprava", match, other);
        EdisonMainViewModel.ApplySearchToModule(fam, "xyz");   // vše skryto
        Assert.False(other.IsVisibleBySearch);

        EdisonMainViewModel.ApplySearchToModule(fam, "");       // vymazání

        Assert.True(fam.IsVisibleBySearch);
        Assert.True(match.IsVisibleBySearch);
        Assert.True(other.IsVisibleBySearch);
    }
}
