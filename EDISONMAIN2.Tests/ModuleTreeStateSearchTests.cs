using EDISONMAIN2.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Tests;

/// <summary>Testy filtrování stromu podle hledaného textu (ModuleTreeState.ApplySearchToModule).</summary>
public class ModuleTreeStateSearchTests
{
    private static ExecutableModule Exe(string name) => new() { Name = name };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    [Fact]
    public void EmptyText_AllVisible()
    {
        var exe = Exe("Cokoliv");
        Assert.True(ModuleTreeState.ApplySearchToModule(exe, ""));
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
        Assert.Equal(expected, ModuleTreeState.ApplySearchToModule(exe, text));
        Assert.Equal(expected, exe.IsVisibleBySearch);
    }

    // Suffix stavu nebo mlineOnly je jen pohled - hledání „stub" nemá vracet moduly podle dekorace.
    [Fact]
    public void DisplaySuffix_NotSearched()
    {
        var exe = new ExecutableModule { Name = "Turnusy", DisplaySuffix = " - stub" };
        var fam = new ModuleFamily { Name = "Interní", DisplaySuffix = " [### M-line ###]" };

        Assert.False(ModuleTreeState.ApplySearchToModule(exe, "stub"));
        Assert.True(ModuleTreeState.ApplySearchToModule(exe, "turnus"));
        Assert.False(ModuleTreeState.ApplySearchToModule(fam, "m-line"));
    }

    [Fact]
    public void Family_VisibleAndExpanded_WhenDescendantMatches()
    {
        var match = Exe("Turnusy");
        var other = Exe("Objednávky");
        var fam = Fam("Autobusová doprava", match, other);
        fam.IsExpanded = false;

        var visible = ModuleTreeState.ApplySearchToModule(fam, "turnus");

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
        Assert.True(ModuleTreeState.ApplySearchToModule(fam, "doprav"));
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

        Assert.True(ModuleTreeState.ApplySearchToModule(fam, "mzdy"));

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

        Assert.True(ModuleTreeState.ApplySearchToModule(fam, "personalistika"));

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

        ModuleTreeState.ApplySearchToModule(fam, "turnus");

        Assert.True(match.IsVisibleBySearch);
        Assert.False(other.IsVisibleBySearch);
    }

    [Fact]
    public void Family_MatchedByOwnName_EmptyFamilyStaysCollapsed()
    {
        var fam = Fam("Mzdy a personalistika");
        fam.IsExpanded = false;

        Assert.True(ModuleTreeState.ApplySearchToModule(fam, "mzdy"));

        Assert.True(fam.IsVisibleBySearch);
        Assert.False(fam.IsExpanded);   // není co ukázat
    }

    [Fact]
    public void Family_Hidden_WhenNothingMatches()
    {
        var fam = Fam("Doprava", Exe("Turnusy"), Exe("Objednávky"));
        Assert.False(ModuleTreeState.ApplySearchToModule(fam, "xyz"));
        Assert.False(fam.IsVisibleBySearch);
    }

    // FirstVisibleExecutable - cíl Enteru v poli „Hledat modul".

    [Fact]
    public void FirstVisibleExecutable_ReturnsFirstMatchInTreeOrder()
    {
        var deep = Exe("Turnusy");
        var later = Exe("Turnusové skupiny");
        IModule[] tree = [Fam("Doprava", Exe("Objednávky"), deep), later];
        foreach (var root in tree) ModuleTreeState.ApplySearchToModule(root, "turnus");

        Assert.Same(deep, ModuleTreeState.FirstVisibleExecutable(tree));
    }

    [Fact]
    public void FirstVisibleExecutable_SkipsFamiliesAndHiddenNodes()
    {
        var match = Exe("Turnusy");
        IModule[] tree = [Exe("Objednávky"), Fam("Doprava", match)];
        foreach (var root in tree) ModuleTreeState.ApplySearchToModule(root, "turnus");

        // Skrytý sourozenec se přeskočí a rodina sama se nevrací - spustit ji nejde.
        Assert.Same(match, ModuleTreeState.FirstVisibleExecutable(tree));
    }

    [Fact]
    public void FirstVisibleExecutable_NullWhenNothingMatches()
    {
        IModule[] tree = [Fam("Doprava", Exe("Turnusy"))];
        foreach (var root in tree) ModuleTreeState.ApplySearchToModule(root, "xyz");

        Assert.Null(ModuleTreeState.FirstVisibleExecutable(tree));
    }

    [Fact]
    public void FirstVisibleExecutable_NullForFamilyWithoutExecutable()
    {
        // Skupina nalezená podle vlastního názvu, ale bez modulu uvnitř - Enter nemá kam skočit.
        IModule[] tree = [Fam("Mzdy", Fam("Výpočet"))];
        foreach (var root in tree) ModuleTreeState.ApplySearchToModule(root, "mzdy");

        Assert.Null(ModuleTreeState.FirstVisibleExecutable(tree));
    }

    [Fact]
    public void EmptyText_ResetsVisibilityToTrue()
    {
        var match = Exe("Turnusy");
        var other = Exe("Objednávky");
        var fam = Fam("Doprava", match, other);
        ModuleTreeState.ApplySearchToModule(fam, "xyz");   // vše skryto
        Assert.False(other.IsVisibleBySearch);

        ModuleTreeState.ApplySearchToModule(fam, "");       // vymazání

        Assert.True(fam.IsVisibleBySearch);
        Assert.True(match.IsVisibleBySearch);
        Assert.True(other.IsVisibleBySearch);
    }
}
