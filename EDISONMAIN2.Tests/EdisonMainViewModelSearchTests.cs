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
    [InlineData("Objednávky", "obj", true)]    // substring bez ohledu na velikost písmen
    [InlineData("Objednávky", "OBJEDN", true)]
    [InlineData("Objednávky", "xyz", false)]
    [InlineData("Řazení", "razeni", true)]     // hledání bez diakritiky najde uzel s diakritikou
    [InlineData("Objednávky", "OBJEDNAVKY", true)]
    [InlineData("Pošta", "posta", true)]
    [InlineData("Turnusy", "tůrnůsy", true)]   // i needle s diakritikou najde uzel bez ní
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

    // HD 13132/5: nález skupiny nesmí být slepá ulička - spustit ji nejde, takže musí jít
    // spustit modul z ní, i když hledané slovo v názvu nemá.
    [Fact]
    public void Family_MatchedByOwnName_RevealsWholeContent()
    {
        var kid = Exe("Zaměstnanci");
        var fam = Fam("Mzdy a personalistika", kid);
        fam.IsExpanded = false;

        Assert.True(EdisonMainViewModel.ApplySearchToModule(fam, "mzdy"));

        Assert.True(fam.IsVisibleBySearch);
        Assert.True(fam.IsExpanded);
        Assert.True(kid.IsVisibleBySearch);
    }

    [Fact]
    public void Family_MatchedByOwnName_RevealsNestedSubtree()
    {
        var deep = Exe("Výplatní lístky");
        var sub = Fam("Výpočet", deep);
        var other = Exe("Zaměstnanci");
        var fam = Fam("Mzdy a personalistika", sub, other);

        Assert.True(EdisonMainViewModel.ApplySearchToModule(fam, "personalistika"));

        Assert.True(sub.IsVisibleBySearch);
        Assert.True(sub.IsExpanded);      // rozbaleno rekurzivně, ať je list rovnou dosažitelný
        Assert.True(deep.IsVisibleBySearch);
        Assert.True(other.IsVisibleBySearch);
    }

    // Shoda na potomkovi zůstává filtrující - jinak by "turnus" ukázalo celou rodinu.
    [Fact]
    public void Family_MatchedByDescendant_StillHidesOtherChildren()
    {
        var match = Exe("Turnusy");
        var other = Exe("Objednávky");
        var fam = Fam("Autobusová doprava", match, other);

        EdisonMainViewModel.ApplySearchToModule(fam, "turnus");

        Assert.True(match.IsVisibleBySearch);
        Assert.False(other.IsVisibleBySearch);
    }

    [Fact]
    public void Family_MatchedByOwnName_EmptyFamilyStaysCollapsed()
    {
        var fam = Fam("Mzdy a personalistika");
        fam.IsExpanded = false;

        Assert.True(EdisonMainViewModel.ApplySearchToModule(fam, "mzdy"));

        Assert.True(fam.IsVisibleBySearch);
        Assert.False(fam.IsExpanded);   // není co ukázat
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
