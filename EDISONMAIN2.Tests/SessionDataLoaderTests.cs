using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Loader pouští větve souběžně, takže mocky tady musí být ručně psané a thread-safe
/// (NSubstitute není). Kryje kontrakt: kompletní data, klon per větev,
/// selhání větve propadne volajícímu (VM pak nic neaplikuje) a chyba preferencí degraduje.
/// </summary>
public class SessionDataLoaderTests
{
    private static readonly string[] TreeNames = ["main", "system", "partners"];

    // Sdílený záznam volání napříč klony - kdo (identita instance) co volal.
    private sealed class CallLog
    {
        private readonly ConcurrentBag<(string Instance, string Method)> _calls = [];
        public void Add(string instance, string method) => _calls.Add((instance, method));
        public IReadOnlyList<(string Instance, string Method)> Calls => [.. _calls];
    }

    private sealed class FakeMLRest(CallLog log, string instance, string? failOnTree = null) : IMLRest
    {
        private int _cloneCount;

        public string Instance => instance;

        public object[] QueryOrThrow(string cacheClass, string method, object[] input)
        {
            log.Add(instance, $"{cacheClass}.{method}");
            return (cacheClass, method) switch
            {
                ("ALVA.SYS.Login", "GetEdisonAccess") => MLRestMocks.Output("3"),
                ("ALVA.USERS.ApiEdison", "GetUserDefaultForm") => MLRestMocks.Output("42"),
                _ => MLRestMocks.Output(),
            };
        }

        public CVar QueryOrThrow(string cacheClass, string method, CVar input)
        {
            log.Add(instance, $"{cacheClass}.{method}");
            return new CVar();
        }

        public IMLRest With(int? cacheLocks = null, string? idfa = null, string? dataNamespace = null) =>
            new FakeMLRest(log, $"{instance}/clone{Interlocked.Increment(ref _cloneCount)}", failOnTree);

        public string DataNamespace { get; set; } = "NS";
        public string Idfa { get; set; } = "CB_DP";
        public string Server => "srv";
        public int Port => 1;
        public string LoginName => "login";
        public string UserID => "_mline";
        public string FullName => "Full Name";
        public string Mlp => "mlp";
        public string Guid => "guid";
        public char Delimiter => '~';
        public int CacheLocks { get; set; }
    }

    // Strom se čte přes IModuleTreeLoader, ne přes MLRest → vlastní fake, taky thread-safe.
    private sealed class FakeTreeLoader(CallLog log, string? failOnTree = null) : IModuleTreeLoader
    {
        public IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest, string treeName = "main")
        {
            log.Add(((FakeMLRest)mlRest).Instance, $"GetTree.{treeName}");
            if (treeName == failOnTree) throw new InvalidOperationException($"strom {treeName} selhal");
            return [new ExecutableModule { DisplayedName = treeName, ExecutablePath = $"{treeName}.exe", FormName = "" }];
        }
    }

    private sealed class FakeUserPrefs(bool fail = false) : IUserPrefsService
    {
        private static readonly Dictionary<string, string> Blobs = new()
        {
            ["Shortcuts"] = """[{"Kind":"MenuModule","Id":"100","ElementId":"100","Name":"A","Col":1,"Row":0}]""",
            ["ModuleUsage"] = """{"100":{"Count":7,"LastUsedUtc":"2026-01-01T00:00:00"}}""",
            ["TreeExpansion"] = """["main:0"]""",
            ["SelectedTab"] = "\"partners\"",
            ["ChangelogSnooze"] = """{"AD":"HASH1"}""",
        };

        public Task<JsonNode?> LoadAsync(string scope, string key)
        {
            if (fail) return Task.FromException<JsonNode?>(new InvalidOperationException("prefs selhaly"));
            return Task.FromResult(Blobs.TryGetValue(key, out var json) ? JsonNode.Parse(json) : null);
        }

        public Task SaveAsync(string scope, string key, JsonNode data) => Task.CompletedTask;
        public Task DeleteAsync(string scope, string key) => Task.CompletedTask;
    }

    private static (SessionDataLoader Loader, ModulePreferencesService Prefs, CallLog Log) Create(
        string? failOnTree = null, bool failPrefs = false)
    {
        var log = new CallLog();
        var rest = new FakeMLRest(log, "shared", failOnTree);
        var loader = new SessionDataLoader(new RestCallRunner(() => rest), new FakeTreeLoader(log, failOnTree), TreeNames);
        return (loader, new ModulePreferencesService(new FakeUserPrefs(failPrefs)), log);
    }

    [Fact]
    public async Task LoadAsync_ReturnsEverySection()
    {
        var (loader, prefs, _) = Create();

        var data = await loader.LoadAsync(licence: null, isAdmin: false, prefs);

        Assert.True(data.AccessTable.IsAllAccess);            // apId=3
        Assert.Equal(42, data.DefaultWindowId);
        Assert.Equal(TreeNames.Order(), data.Trees.Keys.Order());
        Assert.All(TreeNames, name => Assert.Single(data.Trees[name]));
        Assert.Equal("100", Assert.Single(data.Shortcuts).ElementId);
        Assert.Equal(7, data.Usage["100"].Count);
        Assert.Equal(["main:0"], data.Expansion);
        Assert.Equal("partners", data.SelectedTab);
        Assert.Equal("HASH1", data.ChangelogSnooze["AD"]);
    }

    [Fact]
    public async Task LoadAsync_EachParallelBranchGetsOwnClone()
    {
        // Klon per větev není kosmetika: QueryOrThrow čte RestStatus až po Query, takže dvě
        // souběžná volání na téže instanci si stavy prohodí.
        var (loader, prefs, log) = Create();

        await loader.LoadAsync(licence: null, isAdmin: false, prefs);

        var instances = log.Calls.Select(c => c.Instance).ToList();
        Assert.DoesNotContain("shared", instances);
        Assert.Equal(5, instances.Distinct().Count());        // access + 3 stromy + výchozí okno
        Assert.All(instances.GroupBy(i => i), g => Assert.Single(g));
    }

    [Fact]
    public async Task LoadAsync_TreeFailure_PropagatesAndRunsRemainingBranches()
    {
        var (loader, prefs, log) = Create(failOnTree: "system");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => loader.LoadAsync(licence: null, isAdmin: false, prefs));

        Assert.Contains("system", ex.Message);
        // WhenAll nechá dobíhnout i ostatní větve → jejich případné chyby jsou observované.
        Assert.Contains(log.Calls, c => c.Method == "GetTree.main");
        Assert.Contains(log.Calls, c => c.Method == "ALVA.SYS.Login.GetEdisonAccess");
    }

    [Fact]
    public async Task LoadAsync_PrefsFailure_DegradesToEmptyState_ShortcutsSignalFailure()
    {
        // Preference jsou best-effort (persistence nesmí shodit shell) - strom musí dorazit i tak.
        // Plocha je výjimka: null místo prázdna, aby ji VM po výpadku nepřepsala prázdným blobem.
        var (loader, prefs, _) = Create(failPrefs: true);

        var data = await loader.LoadAsync(licence: null, isAdmin: false, prefs);

        Assert.Single(data.Trees["main"]);
        Assert.Null(data.Shortcuts);
        Assert.Empty(data.Usage);
        Assert.Null(data.Expansion);
        Assert.Null(data.SelectedTab);
        Assert.Empty(data.ChangelogSnooze);
    }
}
