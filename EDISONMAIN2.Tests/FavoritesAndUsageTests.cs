using System.Text.Json;
using System.Text.Json.Nodes;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy featury Oblíbené (ruční ★, ořezaný strom) + Používané (automatický žebříček).
/// Statické buildery jsou čisté funkce (jako TryFilter); VM integrace ověřuje toggle + usage hook.
/// </summary>
public class FavoritesAndUsageTests
{
    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static ModuleFamily Family(string name, params IModule[] children)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    // ---------- BuildFavoritesTree ----------------------------------------

    [Fact]
    public void BuildFavoritesTree_KeepsOnlyFavoritedLeaves_PrunesEmptyFamilies()
    {
        var keep = Exe("100", "Keep");
        var drop = Exe("200", "Drop");
        var tree = new IModule[] { Family("A", keep, drop), Family("B", Exe("300")) };

        var result = EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        // Jen rodina A zůstane (má oblíbené dítě), uvnitř jen "Keep". Rodina B i "Drop" zmizí.
        var family = Assert.IsType<ModuleFamily>(Assert.Single(result));
        Assert.Equal("A", family.DisplayedName);
        var leaf = Assert.IsType<ExecutableModule>(Assert.Single(family.NestedModules));
        Assert.Equal("100", leaf.ElementId);
    }

    [Fact]
    public void BuildFavoritesTree_SharesLeafInstances_NotCopies()
    {
        var fav = Exe("100");
        var tree = new IModule[] { Family("A", fav) };

        var result = EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        var leaf = (ExecutableModule)((ModuleFamily)result[0]).NestedModules[0];
        // Sdílená instance → přepnutí IsFavorite v jednom stromě se projeví ve druhém.
        Assert.Same(fav, leaf);
    }

    [Fact]
    public void BuildFavoritesTree_EmptyWhenNoFavorites()
    {
        var tree = new IModule[] { Family("A", Exe("100")) };

        Assert.Empty(EdisonMainViewModel.BuildFavoritesTree([tree], new HashSet<string>()));
    }

    [Fact]
    public void BuildFavoritesTree_SpansMultipleSourceTrees()
    {
        var main = new IModule[] { Exe("100") };
        var system = new IModule[] { Exe("200") };

        var result = EdisonMainViewModel.BuildFavoritesTree([main, system], new HashSet<string> { "100", "200" });

        Assert.Equal(2, result.Count);
    }

    // ---------- BuildUsedList ---------------------------------------------

    [Fact]
    public void BuildUsedList_RanksByCountDesc_ThenRecency()
    {
        var a = Exe("A"); var b = Exe("B"); var c = Exe("C");
        var tree = new IModule[] { a, b, c };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(Count: 3, LastUsedUtc: new DateTime(2026, 1, 1)),
            ["B"] = new(Count: 5, LastUsedUtc: new DateTime(2026, 1, 1)),
            ["C"] = new(Count: 1, LastUsedUtc: new DateTime(2026, 1, 1)),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["B", "A", "C"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_TieBreakByLastUsed()
    {
        var a = Exe("A"); var d = Exe("D");
        var tree = new IModule[] { a, d };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(Count: 2, LastUsedUtc: new DateTime(2026, 1, 2)),
            ["D"] = new(Count: 2, LastUsedUtc: new DateTime(2026, 1, 1)),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["A", "D"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_CapsResultCount()
    {
        var tree = new IModule[] { Exe("A"), Exe("B"), Exe("C") };
        var usage = new Dictionary<string, UsageEntry>
        {
            ["A"] = new(3, default), ["B"] = new(2, default), ["C"] = new(1, default),
        };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 2);

        Assert.Equal(["A", "B"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_ExcludesModulesNotInTree()
    {
        // Modul, který už není viditelný (licence/přístup) → vypadne ze žebříčku.
        var tree = new IModule[] { Exe("A") };
        var usage = new Dictionary<string, UsageEntry> { ["A"] = new(1, default), ["999"] = new(99, default) };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Equal(["A"], result.Select(m => m.ElementId));
    }

    [Fact]
    public void BuildUsedList_SharesTreeInstances()
    {
        var a = Exe("A");
        var tree = new IModule[] { Family("F", a) };
        var usage = new Dictionary<string, UsageEntry> { ["A"] = new(1, default) };

        var result = EdisonMainViewModel.BuildUsedList([tree], usage, cap: 10);

        Assert.Same(a, Assert.Single(result));
    }

    // ---------- ModulePreferencesService ----------------------------------

    [Fact]
    public void Prefs_LoadFavorites_ParsesList()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Favorites")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""["100","200"]""")));
        var svc = new ModulePreferencesService(prefs);

        Assert.Equal(["100", "200"], svc.LoadFavorites());
    }

    [Fact]
    public void Prefs_LoadFavorites_NullBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();   // LoadAsync default → Task<null>
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty(svc.LoadFavorites());
    }

    [Fact]
    public void Prefs_LoadFavorites_CorruptBlob_ReturnsEmpty()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Favorites")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""{"not":"a list"}""")));
        var svc = new ModulePreferencesService(prefs);

        Assert.Empty(svc.LoadFavorites());
    }

    [Fact]
    public void Prefs_LoadUsage_ParsesMap()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "ModuleUsage")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""{"100":{"Count":5,"LastUsedUtc":"2026-01-01T00:00:00"}}""")));
        var svc = new ModulePreferencesService(prefs);

        var usage = svc.LoadUsage();
        Assert.Equal(5, usage["100"].Count);
    }

    [Fact]
    public async Task Prefs_SaveFavorites_WritesScopedBlob()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var svc = new ModulePreferencesService(prefs);

        await svc.SaveFavoritesAsync(["100", "200"]);

        await prefs.Received().SaveAsync("EDISONMAIN2", "Favorites",
            Arg.Is<JsonNode>(n => n.ToJsonString() == """["100","200"]"""));
    }

    // ---------- VM integrace: toggle + usage ------------------------------

    [Fact]
    public void ToggleFavorite_AddsAndRemoves_FromFavoritesTree_AndPersists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        var module = vm.MainMenuTree.OfType<ExecutableModule>().Single();

        Assert.Empty(vm.FavoritesTree);
        Assert.False(module.IsFavorite);

        vm.ToggleFavoriteCommand.Execute(module);

        Assert.True(module.IsFavorite);
        Assert.Same(module, Assert.Single(vm.FavoritesTree));
        prefs.Received().SaveAsync("EDISONMAIN2", "Favorites", Arg.Any<JsonNode>());

        vm.ToggleFavoriteCommand.Execute(module);

        Assert.False(module.IsFavorite);
        Assert.Empty(vm.FavoritesTree);
    }

    [Fact]
    public void ToggleFavorite_Family_FavoritesAllChildren_AndAggregatesFlag()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"), Exe("2"))]);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();

        Assert.False(family.IsFavorite);
        Assert.Empty(vm.FavoritesTree);

        vm.ToggleFavoriteCommand.Execute(family);

        Assert.True(family.IsFavorite);
        var favFamily = Assert.IsType<ModuleFamily>(Assert.Single(vm.FavoritesTree));
        Assert.Equal(2, favFamily.NestedModules.Count);
        Assert.All(family.NestedModules.OfType<ExecutableModule>(), m => Assert.True(m.IsFavorite));
        prefs.Received().SaveAsync("EDISONMAIN2", "Favorites", Arg.Any<JsonNode>());

        vm.ToggleFavoriteCommand.Execute(family);

        Assert.False(family.IsFavorite);
        Assert.Empty(vm.FavoritesTree);
    }

    [Fact]
    public void OpenModule_RecordsUsage_AppearsInUsedList_AndPersists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        var module = vm.MainMenuTree.OfType<ExecutableModule>().Single();

        Assert.Empty(vm.UsedModules);

        vm.OpenModuleCommand.Execute(module);

        Assert.Same(module, Assert.Single(vm.UsedModules));
        prefs.Received().SaveAsync("EDISONMAIN2", "ModuleUsage", Arg.Any<JsonNode>());
    }

    [Fact]
    public void TreeExpansion_RestoredFromPrefs()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "TreeExpansion")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""["main:0"]""")));
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"))]);

        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        Assert.True(family.IsExpanded);   // index 0 v "main" → rozbaleno z uložených prefs
    }

    [Fact]
    public void TreeExpansion_UserExpand_Persists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Family("MHD", Exe("1"))]);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        prefs.ClearReceivedCalls();   // ignoruj načítání během konstrukce

        family.IsExpanded = true;     // simuluje TwoWay zápis z TreeViewItem při rozbalení

        prefs.Received().SaveAsync("EDISONMAIN2", "TreeExpansion", Arg.Any<JsonNode>());
    }

    [Fact]
    public void FavoritesLoadedAtStartup_PrebuildsFavoritesTree()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "Favorites")
            .Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("""["100"]""")));
        var vm = CreateVM(prefs, tree: [Exe("100", "App"), Exe("200", "Other")]);

        var fav = Assert.IsType<ExecutableModule>(Assert.Single(vm.FavoritesTree));
        Assert.Equal("100", fav.ElementId);
        Assert.False(vm.HasNoFavorites);
        Assert.Equal(3, vm.SelectedTreeTabIndex);   // bez uložené volby výchozí Oblíbené (3) když nějaké jsou
    }

    [Fact]
    public void SelectedTab_RestoredFromPrefs()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        prefs.LoadAsync("EDISONMAIN2", "SelectedTab").Returns(Task.FromResult<JsonNode?>(JsonNode.Parse("2")));
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);

        Assert.Equal(2, vm.SelectedTreeTabIndex);   // uložená volba má přednost před výchozí
    }

    [Fact]
    public void SelectedTab_UserChange_Persists()
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(prefs, tree: [Exe("100", "App")]);
        prefs.ClearReceivedCalls();

        vm.SelectedTreeTabIndex = 4;

        prefs.Received().SaveAsync("EDISONMAIN2", "SelectedTab", Arg.Any<JsonNode>());
    }

    // ---------- harness (mirror EdisonMainViewModelTests) -----------------

    private static readonly CompanyStruct[] Companies = [new() { Id = "CB_DP", Database = "CB_DP" }];

    private static EdisonMainViewModel CreateVM(IUserPrefsService prefs, IReadOnlyList<IModule> tree)
    {
        var mlRest = MLRestMocks.Create("CB_DP");
        mlRest.QueryOrThrow("ALVA.SYS.Login", "GetEdisonAccess", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("3"));

        var treeGetter = Substitute.For<IModuleTreeGetter>();
        treeGetter.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(tree);
        treeGetter.GetModuleTree(Arg.Any<IMLRest>(), "system").Returns([]);
        treeGetter.GetModuleTree(Arg.Any<IMLRest>(), "partners").Returns([]);

        var svc = new EDServices(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>())
        {
            UserPrefs = prefs,
        };
        var localPrefs = Substitute.For<ILocalPrefs>();
        var switcher = new CompanySwitcher(svc, localPrefs, () => { });

        EDServices.Initialize(svc);
        try
        {
            return new EdisonMainViewModel(Companies, mlRest, treeGetter, initialLicence: null, switcher,
                () => new(SwitchUserResult.Cancelled, null), localPrefs, () => [], () => { }, MiniAppCatalog.Build([]));
        }
        finally
        {
            EDServices.Reset();
        }
    }
}
