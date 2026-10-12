using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>Start shellu po prvním vykreslení: načtení session → novinky shellu → výchozí okno.</summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainViewModelStartTests
{
    // Load session čeká, dokud test bránu neotevře.
    private sealed class GatedRunner(IMLRest rest) : IRestCallRunner
    {
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            await Gate.Task;
            return call(rest);
        }
    }

    private sealed class RecordingChangelogDialog(List<string> events) : IShellDialogs
    {
        public (EDControls.MiniApps.MiniAppDescriptor Descriptor, EDISONMAIN2.MiniApps.MiniAppPlacement? Placement)? PickMiniApp(
            EDControls.MiniApps.MiniAppCatalog catalog, IReadOnlyList<EDISONMAIN2.MiniApps.MiniAppTarget> targets) => null;

        public bool ShowChangelog(string title, ChangelogContent content, bool snoozed)
        {
            events.Add("novinky");
            return false;
        }

        public void ShowUserProfile(string defaultWindowDisplay, Func<bool> changePassword) { }
        public EDISONMAIN2.Windows.ConnectionWindow.ConnectionChoice? PickConnection(
            ILocalPrefs prefs, EDISONMAIN2.Windows.ConnectionWindow.ConnectionChoice defaultConnection) => null;
        public bool ChangePassword(IMLRest rest, string userName) => false;
    }

    // Výchozí okno 23 je ve stromu jako modul a.exe.
    private static (IMLRest Session, IModuleTreeLoader Trees) SessionWithStartupWindow()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("23"));
        var trees = TreeLoaderWith([new ExecutableModule { Name = "A", ExecutablePath = "a.exe", FormName = "ALVA.A.frm", ElementId = "23" }]);
        return (ml, trees);
    }

    [Fact]
    public async Task StartAsync_OpensStartupWindowAfterTreeLoadAndShellChangelog()
    {
        var events = new List<string>();
        var nav = Substitute.For<IWindowNavigator>();
        nav.ShowLegacyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>())
           .Returns(_ => { events.Add("výchozí okno"); return Task.CompletedTask; });
        // Produkční zdroj čte INF na poolu: načtení novinek visí, dokud ho test nepustí.
        var changelogRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changelog = new TaskCompletionSource<ChangelogContent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = Substitute.For<IChangelogSource>();
        source.LoadAsync("EDISONMAIN").Returns(_ => { changelogRequested.TrySetResult(); return changelog.Task; });
        var (ml, trees) = SessionWithStartupWindow();
        var runner = new GatedRunner(ml);
        var vm = CreateVM(services: MockServices(nav: nav), mlRest: ml, treeLoader: trees, restRunner: runner,
                          changelogSource: source, dialogs: new RecordingChangelogDialog(events), initialize: false);
        Assert.True(vm.OpensStartupWindow);   // předpoklad: produkční výchozí, E2E ho vypíná

        var starting = vm.StartAsync();
        Assert.Empty(events);   // strom ještě není načtený, výchozí okno by v něm nebylo

        runner.Gate.SetResult();
        await changelogRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Start čeká na novinky: bez nich nesmí doběhnout ani otevřít výchozí okno.
        await Task.WhenAny(starting, Task.Delay(200));
        Assert.False(starting.IsCompleted);
        Assert.Empty(events);

        changelog.SetResult(new ChangelogContent("EDISONMAIN", "nové změny", "historie", "hash-1"));
        await starting.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["novinky", "výchozí okno"], events);
    }

    [Fact]
    public async Task StartAsync_SessionLoadFails_ReportsErrorAndOpensNothing()
    {
        // Volá ho async void handler ContentRendered: výjimka by skončila v globálním handleru.
        var nav = Substitute.For<IWindowNavigator>();
        var popup = Substitute.For<IPopupDisplayer>();
        var failure = new InvalidOperationException("strom nejde načíst");
        var trees = Substitute.For<IModuleTreeLoader>();
        trees.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns(_ => throw failure);
        var source = Substitute.For<IChangelogSource>();
        var events = new List<string>();
        var vm = CreateVM(services: MockServices(nav: nav, popup: popup), treeLoader: trees, changelogSource: source,
                          dialogs: new RecordingChangelogDialog(events), initialize: false);

        await vm.StartAsync();

        popup.Received(1).Error(failure);
        Assert.Empty(events);
        await source.DidNotReceive().LoadAsync(Arg.Any<string>());
        await nav.DidNotReceive().ShowLegacyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }
}
