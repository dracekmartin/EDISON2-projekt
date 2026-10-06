using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Instanční část <see cref="ModuleTreeState"/> bez ViewModelu: záložky, persistence rozbalení
/// a hledání. Rozbalení vynucené hledáním je přechodné - po vymazání textu se strom vrátí do stavu
/// před hledáním (ne do stavu z loginu) a změny rozbalení během hledání se nepersistují.
/// Návaznost na session (přepnutí firmy, přestavba Oblíbených) testuje VM.
/// </summary>
public class ModuleTreeStateTests
{
    private const string Scope = "EDISONMAIN2";
    private const string ExpansionKey = "TreeExpansion";

    private static ExecutableModule Exe(string id, string name) =>
        new() { Name = name, ElementId = id, ExecutablePath = id + ".exe", FormName = "ALVA.A.frm" };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    // Dvě rodiny, obě s modulem obsahujícím „turnus" - hledání rozbalí obě.
    private static IReadOnlyList<IModule> TwoFamilies() =>
        [Fam("Doprava", Exe("1", "Turnusy")), Fam("Mzdy", Exe("2", "Turnusové skupiny"))];

    // Stav po session loadu: hlavní strom naplněný, uložené rozbalení aplikované a napojené na persistenci.
    private static (ModuleTreeState State, IUserPrefsService Prefs) Loaded(
        IReadOnlyList<IModule> main, List<string>? savedExpansion = null)
    {
        var prefs = Substitute.For<IUserPrefsService>();
        var modulePrefs = new ModulePreferencesService(prefs);
        var state = new ModuleTreeState(() => modulePrefs, EdisonMainViewModelHarness.NeutralStation);
        state.Load(new Dictionary<string, IReadOnlyList<IModule>> { ["main"] = main, ["system"] = [], ["partners"] = [] },
                   licence: null, access: null, savedExpansion);
        state.ApplySavedExpansion();
        prefs.ClearReceivedCalls();
        return (state, prefs);
    }

    private static void Search(ModuleTreeState state, string text)
    {
        state.SearchText = text;
        state.FlushPendingSearch();
    }

    [Fact]
    public void TreeTabs_HaveUniqueKeys_AndExactlyOneNonExpandable()
    {
        // Identita záložky je persistovaný klíč; duplicita by tiše přepsala uloženou volbu.
        // Používané jsou ListBox bez stromu - jediná záložka, kam Rozbalit/Sbalit vše necílí.
        var state = new ModuleTreeState(() => null);

        Assert.Equal(state.TreeTabs.Count, state.TreeTabs.Select(t => t.Key).Distinct().Count());
        Assert.Single(state.TreeTabs, t => !t.Expandable);
        Assert.All(state.TreeTabs, t => Assert.Equal(t.Expandable, t.Tree != null));
    }

    [Fact]
    public void SuppressExpansionSave_RefCounts_InnerDisposeKeepsOuterSuppression()
    {
        // Cesty potlačující persistenci se vnořují (přestavba Oblíbených uvnitř session loadu,
        // re-filtrace při hledání). Plochý bool by po konci vnitřního bloku zápis odemkl
        // a rozbalení vynucené programově by se uložilo jako uživatelská volba.
        var (state, prefs) = Loaded([Fam("Rodina", Exe("23", "Modul"))]);
        var family = state.MainMenuTree.OfType<ModuleFamily>().Single();

        var outer = state.SuppressExpansionSave();
        var inner = state.SuppressExpansionSave();
        inner.Dispose();
        inner.Dispose(); // dvojité Dispose nesmí odemknout vnější scope
        family.IsExpanded = !family.IsExpanded;
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());

        outer.Dispose();
        family.IsExpanded = !family.IsExpanded;
        prefs.Received(1).SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void ClearSearch_RestoresExpansionFromBeforeSearch_NotLoginState()
    {
        var (state, _) = Loaded(TwoFamilies(), ["main:0"]);
        var families = state.MainMenuTree.OfType<ModuleFamily>().ToList();
        var (doprava, mzdy) = (families[0], families[1]);
        Assert.True(doprava.IsExpanded);    // stav z loginu
        Assert.False(mzdy.IsExpanded);

        doprava.IsExpanded = false;         // uživatel po loginu změní obě
        mzdy.IsExpanded = true;

        Search(state, "turnus");
        Assert.True(doprava.IsExpanded);    // hledání rozbalí nálezy
        Assert.True(mzdy.IsExpanded);

        Search(state, "");

        Assert.False(doprava.IsExpanded);   // zpět do stavu před hledáním, ne z loginu
        Assert.True(mzdy.IsExpanded);
    }

    [Fact]
    public void ClearSearch_WithoutSavedExpansion_RestoresServerDefault()
    {
        var (state, prefs) = Loaded(TwoFamilies());
        var family = state.MainMenuTree.OfType<ModuleFamily>().First();
        Assert.False(family.IsExpanded);    // serverové výchozí = sbaleno

        Search(state, "turnus");
        Assert.True(family.IsExpanded);

        Search(state, "");

        Assert.False(family.IsExpanded);
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void DuringSearch_ExpansionChangesAreTransientAndNotPersisted()
    {
        var (state, prefs) = Loaded(TwoFamilies());
        var family = state.MainMenuTree.OfType<ModuleFamily>().First();
        family.IsExpanded = true;           // uživatelská volba před hledáním (persistuje se)
        prefs.ClearReceivedCalls();

        Search(state, "turnus");
        family.IsExpanded = false;          // klik na rodinu během hledání
        state.SetTreeExpanded(state.MainMenuTree, true);    // Rozbalit vše během hledání
        state.SetTreeExpanded(state.MainMenuTree, false);
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());

        Search(state, "");

        Assert.True(family.IsExpanded);     // změny během hledání se zahodí
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }

    [Fact]
    public void ClearWithinDebounce_LeavesExpansionUntouched()
    {
        var (state, prefs) = Loaded(TwoFamilies(), ["main:0"]);
        var family = state.MainMenuTree.OfType<ModuleFamily>().First();
        family.IsExpanded = false;
        prefs.ClearReceivedCalls();

        state.SearchText = "turnus";        // napsáno a smazáno dřív, než debounce doběhne
        state.SearchText = "";
        state.FlushPendingSearch();

        Assert.False(family.IsExpanded);
        Assert.All(state.MainMenuTree, m => Assert.True(m.IsVisibleBySearch));
        prefs.DidNotReceive().SaveAsync(Scope, ExpansionKey, Arg.Any<JsonNode>());
    }
}
