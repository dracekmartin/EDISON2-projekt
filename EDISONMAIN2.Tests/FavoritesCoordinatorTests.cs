using System.Text.Json.Nodes;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Plocha a záložka Oblíbené bez ViewModelu: ořezaný strom, ★ ve stromu, změny z miniaplikace,
/// přenačtení po exitu agendy a ochrana uživatelovy plochy po selhaném loadu.
/// </summary>
public class FavoritesCoordinatorTests
{
    private const string Scope = "EDISONMAIN2";
    private const string ShortcutsKey = "Shortcuts";

    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static ModuleFamily Family(string name, params IModule[] children)
    {
        var f = new ModuleFamily { DisplayedName = name };
        foreach (var c in children) f.NestedModules.Add(c);
        return f;
    }

    private static DesktopShortcutDto Menu(string elementId) =>
        new(nameof(ShortcutKind.MenuModule), elementId, elementId, "M");

    // Stav po načtení stromů session; plochu dodá test přes Apply (jako ApplySessionData ve VM).
    private sealed class Fixture
    {
        public IUserPrefsService Prefs { get; } = Substitute.For<IUserPrefsService>();
        public IWindowNavigator Nav { get; } = Substitute.For<IWindowNavigator>();
        public ModuleShortcutsService Shortcuts { get; } = new();
        public ModuleTreeState Trees { get; }
        public FavoritesCoordinator Coordinator { get; }
        public int Rebuilds { get; private set; }

        public Fixture(params IModule[] main)
        {
            var modulePrefs = new ModulePreferencesService(Prefs);
            Trees = new ModuleTreeState(() => modulePrefs, EdisonMainViewModelHarness.NeutralStation);
            Trees.Load(new Dictionary<string, IReadOnlyList<IModule>> { ["main"] = main, ["system"] = [], ["partners"] = [] },
                       licence: null, access: null, expansion: null);
            Coordinator = new FavoritesCoordinator(Shortcuts, Trees, () => modulePrefs, Nav, () => Rebuilds++);
        }

        // Load staví filtrované instance, proto se modul hledá až ve stromu.
        public ExecutableModule Module(string id) => Trees.AllExecutables().Single(e => e.ElementId == id);
    }

    [Fact]
    public void BuildFavoritesTree_KeepsOnlyFavoritedLeaves_PrunesEmptyFamilies()
    {
        var keep = Exe("100", "Keep");
        var drop = Exe("200", "Drop");
        var tree = new IModule[] { Family("A", keep, drop), Family("B", Exe("300")) };

        var result = FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string> { "100" });

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

        var result = FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string> { "100" });

        var leaf = (ExecutableModule)((ModuleFamily)result[0]).NestedModules[0];
        // Sdílená instance → přepnutí IsFavorite v jednom stromě se projeví ve druhém.
        Assert.Same(fav, leaf);
    }

    [Fact]
    public void BuildFavoritesTree_EmptyWhenNoFavorites()
    {
        var tree = new IModule[] { Family("A", Exe("100")) };

        Assert.Empty(FavoritesCoordinator.BuildFavoritesTree([tree], new HashSet<string>()));
    }

    [Fact]
    public void BuildFavoritesTree_SpansMultipleSourceTrees()
    {
        var main = new IModule[] { Exe("100") };
        var system = new IModule[] { Exe("200") };

        var result = FavoritesCoordinator.BuildFavoritesTree([main, system], new HashSet<string> { "100", "200" });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_SeedsFavoritesTree_AndReportsRebuild()
    {
        // Callback řídí HasNoFavorites ve VM - bez něj by empty-state záložky Oblíbené visel.
        var fx = new Fixture(Exe("100", "App"), Exe("200", "Other"));

        fx.Coordinator.Apply([Menu("100")]);

        var fav = Assert.IsType<ExecutableModule>(Assert.Single(fx.Trees.FavoritesTree));
        Assert.Equal("100", fav.ElementId);
        Assert.Equal(1, fx.Rebuilds);
        fx.Prefs.DidNotReceive().SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());   // načtené se neukládá
    }

    [Fact]
    public void Toggle_Family_FavoritesAllChildren_AggregatesFlag_AndSavesOnce()
    {
        var fx = new Fixture(Family("MHD", Exe("1"), Exe("2")));
        fx.Coordinator.Apply([]);
        var family = fx.Trees.MainMenuTree.OfType<ModuleFamily>().Single();

        fx.Coordinator.Toggle(family);

        Assert.True(family.IsFavorite);
        var favFamily = Assert.IsType<ModuleFamily>(Assert.Single(fx.Trees.FavoritesTree));
        Assert.Equal(2, favFamily.NestedModules.Count);
        Assert.All(family.NestedModules.OfType<ExecutableModule>(), m => Assert.True(m.IsFavorite));
        // Hromadná změna = jeden zápis plochy, ne jeden per modul rodiny.
        fx.Prefs.Received(1).SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());

        fx.Coordinator.Toggle(family);

        Assert.False(family.IsFavorite);
        Assert.Empty(fx.Trees.FavoritesTree);
    }

    [Fact]
    public void DesktopRemove_ViaSharedService_RebuildsFavoritesAndPersists()
    {
        // Miniaplikace Plocha mění sdílenou službu přímo, strom o tom ví jen z jejího eventu.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([Menu("100")]);
        Assert.Single(fx.Trees.FavoritesTree);   // předpoklad

        fx.Shortcuts.Remove(fx.Shortcuts.Items.Single());

        Assert.Empty(fx.Trees.FavoritesTree);
        fx.Prefs.Received(1).SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void NonMenuShortcut_NotInFavoritesTree_ButSurvivesFavoriteToggle()
    {
        // Tisková sestava na ploše není položkou hlavní nabídky → do záložky Oblíbené nepatří,
        // ale změna oblíbených ji nesmí z uloženého blobu vyhodit.
        JsonNode? written = null;
        var fx = new Fixture(Exe("100", "App"));
        fx.Prefs.SaveAsync(Scope, ShortcutsKey, Arg.Do<JsonNode>(n => written = n));
        fx.Coordinator.Apply([new DesktopShortcutDto(nameof(ShortcutKind.PrintReport), "PrehledSkod", "31001", "Přehled škod")]);

        Assert.Empty(fx.Trees.FavoritesTree);

        fx.Coordinator.Toggle(fx.Module("100"));

        Assert.Contains("PrehledSkod", written!.ToJsonString());
        Assert.Contains("\"100\"", written.ToJsonString());
    }

    [Fact]
    public void ShortcutForModuleMissingFromTree_SurvivesFavoriteToggle()
    {
        // Modul, který strom aktuálně nezná (odebraná licence) - po toggle jiného modulu
        // musí zůstat v uloženém blobu, jinak by o zástupce uživatel tiše přišel.
        JsonNode? written = null;
        var fx = new Fixture(Exe("100", "App"));
        fx.Prefs.SaveAsync(Scope, ShortcutsKey, Arg.Do<JsonNode>(n => written = n));
        fx.Coordinator.Apply([Menu("999")]);

        fx.Coordinator.Toggle(fx.Module("100"));

        Assert.Contains("\"999\"", written!.ToJsonString());
    }

    [Fact]
    public void ReloadAfterLegacyExit_LoadFails_KeepsDesktopAndDoesNotSave()
    {
        // Exit agendy během výpadku dřív vyprázdnil plochu v paměti a první toggle prázdno zapsal.
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([Menu("100")]);
        Assert.Single(fx.Trees.FavoritesTree);

        fx.Prefs.LoadAsync(Scope, ShortcutsKey)
            .Returns(Task.FromException<JsonNode?>(new InvalidOperationException("server down")));
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();

        Assert.Single(fx.Trees.FavoritesTree);
        fx.Prefs.DidNotReceive().SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());

        fx.Coordinator.Toggle(fx.Module("100"));

        // Blob se v této session načetl, změna z shellu se ukládat smí.
        fx.Prefs.Received(1).SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void SessionLoadFailed_ToggleWorksInMemory_SaveResumesAfterSuccessfulReload()
    {
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply(null);   // load plochy v session selhal
        var module = fx.Module("100");

        fx.Coordinator.Toggle(module);

        Assert.True(module.IsFavorite);   // v paměti plocha funguje
        fx.Prefs.DidNotReceive().SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());   // ale nepřepisuje neznámý blob

        fx.Prefs.LoadAsync(Scope, ShortcutsKey).Returns(Task.FromResult<JsonNode?>(new JsonArray()));
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();
        fx.Coordinator.Toggle(module);

        fx.Prefs.Received(1).SaveAsync(Scope, ShortcutsKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void Detach_StopsReloadAndPersistence()
    {
        var fx = new Fixture(Exe("100", "App"));
        fx.Coordinator.Apply([]);
        fx.Prefs.ClearReceivedCalls();

        fx.Coordinator.Detach();
        fx.Nav.LegacyModuleExited += Raise.Event<Action>();
        fx.Shortcuts.Add(fx.Module("100"));                       // členství
        fx.Shortcuts.PlaceAt(fx.Shortcuts.Items.Single(), 3, 2);  // pozice

        fx.Prefs.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<string>());
        fx.Prefs.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<JsonNode>());
    }
}
