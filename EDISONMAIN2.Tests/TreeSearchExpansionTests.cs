using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Rozbalení vynucené hledáním je přechodné: po vymazání textu se strom vrátí do stavu před
/// hledáním (ne do stavu z loginu) a změny rozbalení během hledání se nepersistují.
/// </summary>
public class TreeSearchExpansionTests
{
    private const string Scope = "EDISONMAIN2";
    private const string ExpansionKey = "TreeExpansion";

    private static ExecutableModule Exe(string id, string name) =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe", FormName = "ALVA.A.frm" };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    private static IUserPrefsService PrefsWithExpansion(string? json)
    {
        var prefs = Substitute.For<IUserPrefsService>();
        if (json != null)
            prefs.LoadAsync(Scope, ExpansionKey).Returns(Task.FromResult<JsonNode?>(JsonNode.Parse(json)));
        return prefs;
    }

    // Dvě rodiny, obě s modulem obsahujícím „turnus" - hledání rozbalí obě.
    private static IReadOnlyList<IModule> TwoFamilies() =>
        [Fam("Doprava", Exe("1", "Turnusy")), Fam("Mzdy", Exe("2", "Turnusové skupiny"))];

    private static void Search(EdisonMainViewModel vm, string text)
    {
        vm.TreeSearchText = text;
        vm.FlushPendingSearch();
    }

    [Fact]
    public void Clear_RestoresExpansionFromBeforeSearch_NotLoginState()
    {
        var prefs = PrefsWithExpansion("""["main:0"]""");
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith(TwoFamilies()));
        var families = vm.MainMenuTree.OfType<ModuleFamily>().ToList();
        var (doprava, mzdy) = (families[0], families[1]);
        Assert.True(doprava.IsExpanded);    // stav z loginu
        Assert.False(mzdy.IsExpanded);

        doprava.IsExpanded = false;         // uživatel po loginu změní obě
        mzdy.IsExpanded = true;

        Search(vm, "turnus");
        Assert.True(doprava.IsExpanded);    // hledání rozbalí nálezy
        Assert.True(mzdy.IsExpanded);

        Search(vm, "");

        Assert.False(doprava.IsExpanded);   // zpět do stavu před hledáním, ne z loginu
        Assert.True(mzdy.IsExpanded);
    }

    [Fact]
    public void Clear_WithoutSavedExpansion_RestoresServerDefault()
    {
        var prefs = PrefsWithExpansion(null);
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith(TwoFamilies()));
        var family = vm.MainMenuTree.OfType<ModuleFamily>().First();
        Assert.False(family.IsExpanded);    // serverové výchozí = sbaleno
        prefs.ClearReceivedCalls();

        Search(vm, "turnus");
        Assert.True(family.IsExpanded);

        Search(vm, "");

        Assert.False(family.IsExpanded);
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void DuringSearch_ExpansionChangesAreTransientAndNotPersisted()
    {
        var prefs = PrefsWithExpansion(null);
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith(TwoFamilies()));
        vm.SelectedTab = vm.TreeTabs.First(t => t.Expandable);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().First();
        family.IsExpanded = true;           // uživatelská volba před hledáním (persistuje se)
        prefs.ClearReceivedCalls();

        Search(vm, "turnus");
        family.IsExpanded = false;          // klik na rodinu během hledání
        vm.ExpandAllCommand.Execute(null);  // Rozbalit vše během hledání
        vm.CollapseAllCommand.Execute(null);
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());

        Search(vm, "");

        Assert.True(family.IsExpanded);     // změny během hledání se zahodí
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void ClearWithinDebounce_LeavesExpansionUntouched()
    {
        var prefs = PrefsWithExpansion("""["main:0"]""");
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith(TwoFamilies()));
        var family = vm.MainMenuTree.OfType<ModuleFamily>().First();
        family.IsExpanded = false;
        prefs.ClearReceivedCalls();

        vm.TreeSearchText = "turnus";       // napsáno a smazáno dřív, než debounce doběhne
        vm.TreeSearchText = "";
        vm.FlushPendingSearch();

        Assert.False(family.IsExpanded);
        Assert.All(vm.MainMenuTree, m => Assert.True(m.IsVisibleBySearch));
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public async Task SwitchCompany_DuringSearch_TakesFreshSnapshotOfNewSession()
    {
        var prefs = PrefsWithExpansion("""["main:0"]""");
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith(TwoFamilies()));
        var before = vm.MainMenuTree.OfType<ModuleFamily>().First();
        before.IsExpanded = false;          // snímek staré firmy = sbaleno

        Search(vm, "turnus");
        await vm.SwitchCompanyAsync(1);     // nové instance uzlů s persistovaným rozbalením (main:0)
        var after = vm.MainMenuTree.OfType<ModuleFamily>().First();
        Assert.NotSame(before, after);
        Assert.True(after.IsExpanded);      // hledání trvá dál

        Search(vm, "");

        Assert.True(after.IsExpanded);      // persistovaný stav nové session, ne snímek staré
    }

    [Fact]
    public void ToggleFavorite_RebuildUsesCurrentPersistedExpansion_NotLoginState()
    {
        var prefs = PrefsWithExpansion(null);
        var vm = CreateVM(userPrefs: prefs, treeLoader: TreeLoaderWith([Fam("MHD", Exe("1", "Linky"), Exe("2", "Spoje"))]));
        var leaves = vm.MainMenuTree.OfType<ModuleFamily>().Single().NestedModules.OfType<ExecutableModule>().ToList();

        vm.ToggleFavoriteCommand.Execute(leaves[0]);
        var favFamily = Assert.IsType<ModuleFamily>(Assert.Single(vm.FavoritesTree));
        Assert.True(favFamily.IsExpanded);  // výchozí wrapper Oblíbených je rozbalený
        favFamily.IsExpanded = false;       // uživatel sbalí (persistuje se)

        vm.ToggleFavoriteCommand.Execute(leaves[1]);   // přestavba Oblíbených

        var rebuilt = Assert.IsType<ModuleFamily>(Assert.Single(vm.FavoritesTree));
        Assert.NotSame(favFamily, rebuilt);
        Assert.False(rebuilt.IsExpanded);   // bez aktualizace _expansion by se vrátilo rozbaleno
    }
}
