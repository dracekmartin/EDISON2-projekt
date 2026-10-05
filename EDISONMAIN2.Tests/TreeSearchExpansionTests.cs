using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Hledání a rozbalení napříč změnou session: přepnutí firmy během hledání a přestavba Oblíbených.
/// Samotné přechodné rozbalení hledáním testuje <see cref="ModuleTreeStateTests"/>.
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
