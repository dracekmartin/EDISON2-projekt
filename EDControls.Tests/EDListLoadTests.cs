using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using ALWA.SYS;
using EDControls.EDList;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDTesting;
using NSubstitute;
using List = EDControls.EDList.EDList;

namespace EDControls.Tests;

/// <summary>
/// Dokumentovaná invarianta LoadListAsync (EDList/CLAUDE.md): dotazy se serializují
/// a odpověď staršího dotazu už do seznamu nedosedne - rychlé přepnutí filtru nezamíchá řádky.
/// </summary>
public class EDListLoadTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void LoadListAsync_StaleResponse_DoesNotLandAfterNewerRequest()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            var firstQueryStarted = new ManualResetEventSlim();
            var releaseFirst = new ManualResetEventSlim();
            int calls = 0;
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstQueryStarted.Set();
                    releaseFirst.Wait(TimeSpan.FromSeconds(10));
                    return RowsOutput("stara");
                }
                return RowsOutput("nova");
            });
            var list = InitializedList(mock);

            var first = list.LoadListAsync();
            Assert.True(firstQueryStarted.Wait(TimeSpan.FromSeconds(10)));
            var second = list.LoadListAsync();   // novější požadavek - starší je ještě v letu
            releaseFirst.Set();
            PumpUntilCompleted(first, second);

            Assert.True(first.IsCompletedSuccessfully);
            Assert.True(second.IsCompletedSuccessfully);
            var row = Assert.Single(list.Rows);
            Assert.Equal("nova", row.Id);   // stará odpověď se zahodila
        });
    }

    /// <summary>Bez sloupců by se řádky naplnily, ale DataGrid zůstal prázdný - host to musí poznat hned.</summary>
    [Fact]
    public void LoadList_WithoutInitialize_Throws()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(RowsOutput("1"));
            var list = NewList(mock);

            Assert.Throws<InvalidOperationException>(() => list.LoadList());
            Assert.Empty(list.Rows);
            mock.DidNotReceive().QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>());   // sync cesta nečeká na server zbytečně
        });
    }

    /// <summary>TD picker pouští InitializeListAsync a refresh EDFilteru souběžně - kontrola sloupců
    /// před dotazem by tu hodila, přestože sloupce dorazí dřív než řádky.</summary>
    [Fact]
    public void LoadListAsync_ConcurrentWithInitializeListAsync_LandsRows()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            var releaseInit = new ManualResetEventSlim();
            mock.QueryOrThrow("ALVA.MockList", "InitializeList", Arg.Any<CVar>()).Returns(_ =>
            {
                releaseInit.Wait(TimeSpan.FromSeconds(10));
                return ColumnsOutput();
            });
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(RowsOutput("1"));
            var list = NewList(mock);

            var init = list.InitializeListAsync();
            var load = list.LoadListAsync();
            releaseInit.Set();
            PumpUntilCompleted(init, load);

            Assert.True(init.IsCompletedSuccessfully);
            Assert.True(load.IsCompletedSuccessfully);
            Assert.Equal("1", Assert.Single(list.Rows).Id);
        });
    }

    [Fact]
    public void LoadListAsync_WithoutInitialize_Throws()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(RowsOutput("1"));
            var list = NewList(mock);

            var load = list.LoadListAsync();
            PumpUntilCompleted(load);

            Assert.IsType<InvalidOperationException>(load.Exception?.InnerException);
            Assert.Empty(list.Rows);
        });
    }

    /// <summary>Dva seznamy na jedné obrazovce: async dotazy jdou jen na klony. Sdílenou instanci souběžně
    /// drží i synchronní EDInputForm a RestStatus je per-instance - jinak by si prohodily chyby.</summary>
    [Fact]
    public void TwoLists_ConcurrentAsyncLoads_QueryOnlyClones()
    {
        RunSta(() =>
        {
            var shared = new CloningMLRest();
            var services = new EDServices(shared, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
            List[] lists =
            [
                new() { CacheClass = "ALVA.MockList", ServicesOverride = services },
                new() { CacheClass = "ALVA.MockList", ServicesOverride = services },
            ];

            var tasks = lists
                .SelectMany(l => new[] { l.InitializeListAsync() }.Concat(Enumerable.Range(0, 5).Select(_ => l.LoadListAsync())))
                .ToArray();
            PumpUntilCompleted(tasks);

            Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
            Assert.Equal(0, shared.SharedQueries);
            Assert.Equal(12, shared.Clones);
            Assert.Equal(12, shared.CloneQueries);
            Assert.All(lists, l => Assert.Equal("1", Assert.Single(l.Rows).Id));
        });
    }

    /// <summary>Thread-safe fake: počítá dotazy na sdílené instanci a na klonech z <c>With()</c>.</summary>
    private sealed class CloningMLRest : IMLRest
    {
        private readonly CloningMLRest? _root;
        private int _sharedQueries, _clones, _cloneQueries;

        public CloningMLRest() { }
        private CloningMLRest(CloningMLRest root) => _root = root;

        public int SharedQueries => _sharedQueries;
        public int Clones => _clones;
        public int CloneQueries => _cloneQueries;

        public CVar QueryOrThrow(string cacheClass, string method, CVar input)
        {
            if (_root is null) Interlocked.Increment(ref _sharedQueries);
            else Interlocked.Increment(ref _root._cloneQueries);
            return method == "InitializeList" ? ColumnsOutput() : RowsOutput("1");
        }

        public object[] QueryOrThrow(string cacheClass, string method, object[] input) => throw new NotSupportedException();

        public IMLRest With(int? cacheLocks = null, string? idfa = null, string? dataNamespace = null)
        {
            var root = _root ?? this;
            Interlocked.Increment(ref root._clones);
            return new CloningMLRest(root);
        }

        public string DataNamespace { get; set; } = "";
        public string Idfa { get; set; } = "";
        public string Server => "";
        public int Port => 0;
        public string LoginName => "";
        public string FullName => "";
        public string UserID => "";
        public string Mlp => "";
        public string Guid => "";
        public char Delimiter => '\0';
        public int CacheLocks { get; set; }
    }

    /// <summary>Nový dataset po LoadList: hledání i uložený layout platí dál (EDList/CLAUDE.md).</summary>
    [Fact]
    public void LoadList_ReappliesSearchAndLayout()
    {
        RunSta(() =>
        {
            var mock = MLRestMocks.Create();
            var prefs = Substitute.For<IUserPrefsService>();
            prefs.LoadAsync("EDControls.EDList", "Tests.EDList").Returns(Task.FromResult<JsonNode?>(
                ColumnLayoutSerializer.Serialize(new ColumnLayout { Columns = [new ColumnPref { Key = "name", Width = 222 }] })));
            var list = InitializedList(mock, prefs);
            list.HostId = "Tests.EDList";
            list.SearchText = "praha";
            var output = new CVar();
            output["1"] = new CVar("""{"id":"1","name":"Praha"}""");
            output["2"] = new CVar("""{"id":"2","name":"Brno"}""");
            mock.QueryOrThrow("ALVA.MockList", "LoadList", Arg.Any<CVar>()).Returns(output);

            list.LoadList();

            Assert.Equal(["1"], list.DataGrid.Items.OfType<EDListRow>().Select(r => r.Id));
            Assert.Equal(222, list.DataGrid.Columns.Single().Width.Value);
        });
    }

    private static List NewList(IMLRest mock, IUserPrefsService? prefs = null)
    {
        // Klon = tentýž mock: tyhle testy hlídají pořadí a starou odpověď, klonování má vlastní test.
        mock.With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>()).Returns(mock);
        return new()
        {
            CacheClass = "ALVA.MockList",
            ServicesOverride = new EDServices(mock,
                Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>())
            { UserPrefs = prefs ?? new NullUserPrefsService() },
        };
    }

    private static List InitializedList(IMLRest mock, IUserPrefsService? prefs = null)
    {
        mock.QueryOrThrow("ALVA.MockList", "InitializeList", Arg.Any<CVar>()).Returns(ColumnsOutput());
        var list = NewList(mock, prefs);
        list.InitializeList();
        return list;
    }

    private static CVar ColumnsOutput()
    {
        var columns = new CVar();
        columns["1"] = new CVar("""[{"key":"name","label":"Název","type":"TEXT"}]""");
        return columns;
    }

    private static CVar RowsOutput(string id)
    {
        var output = new CVar();
        output["1"] = new CVar($$"""{"id":"{{id}}","name":"radek {{id}}"}""");
        return output;
    }

    // Awaity LoadListAsync pokračují přes DispatcherSynchronizationContext - bez pumpování
    // by uvázly, dokud drží test vlákno uvnitř Invoke. Timeout mění případný deadlock
    // na viditelné selhání assertu.
    private static void PumpUntilCompleted(params Task[] tasks)
    {
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
        timeout.Start();
        Task.WhenAll(tasks).ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        timeout.Stop();
    }

    // EDList.xaml čte StaticResource EDIcons/EDStrings → Application se slovníky appky;
    // trvalé app vlákno viz EDComponentToolbarTests.RunSta.
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
