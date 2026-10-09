using System.Diagnostics;
using System.Text.Json.Nodes;
using EDControls;
using EDControls.MiniApps;
using EDControls.UserPrefs;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.ConnectionWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;
using static EDISONMAIN2.Tests.MiniAppTestDoubles;

namespace EDISONMAIN2.Tests;

/// <summary>Kód shellu po dialogu: falešné dialogy vrací výsledek, jaký by vrátilo okno.</summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainViewModelDialogTests
{
    private sealed class FakeDialogs : IShellDialogs
    {
        public (MiniAppDescriptor Descriptor, MiniAppPlacement? Placement)? MiniAppToPick { get; set; }
        public bool SnoozeOnClose { get; set; }
        public ConnectionChoice? ConnectionToPick { get; set; }
        public bool PasswordChanges { get; set; }

        public IReadOnlyList<MiniAppTarget>? OfferedTargets { get; private set; }
        public List<(string Title, bool Snoozed)> ShownChangelogs { get; } = [];
        public string? ProfileDefaultWindow { get; private set; }
        public Func<bool>? ProfileChangePassword { get; private set; }
        public List<string> PasswordChangedFor { get; } = [];

        public (MiniAppDescriptor Descriptor, MiniAppPlacement? Placement)? PickMiniApp(
            MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets)
        {
            OfferedTargets = targets;
            return MiniAppToPick;
        }

        public bool ShowChangelog(string title, ChangelogContent content, bool snoozed)
        {
            ShownChangelogs.Add((title, snoozed));
            return SnoozeOnClose;
        }

        public void ShowUserProfile(string defaultWindowDisplay, Func<bool> changePassword)
        {
            ProfileDefaultWindow = defaultWindowDisplay;
            ProfileChangePassword = changePassword;
        }

        public ConnectionChoice? PickConnection(ILocalPrefs prefs, ConnectionChoice defaultConnection) => ConnectionToPick;

        public bool ChangePassword(IMLRest rest, string userName)
        {
            PasswordChangedFor.Add(userName);
            return PasswordChanges;
        }
    }

    private const string Server = "dev";
    private const int Port = 57770;

    private static IMLRest SessionOf(string login)
    {
        var ml = MockMLRest();
        ml.LoginName.Returns(login);
        ml.Server.Returns(Server);
        ml.Port.Returns(Port);
        return ml;
    }

    private static ILocalPrefs PrefsRemembering(string user)
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.SetRememberedPassword(Server, Port, PasswordProtector.Protect(user, "stare"));
        return prefs;
    }

    [Fact]
    public void PickMiniApp_OffersOpenPanes_OpensPickedAppAtPickedPlacement()
    {
        var svc = MockServices();
        var desktop = new MiniAppDescriptor("plocha", "Plocha", () => new StubMiniApp(svc), Pinned: true);
        var app = new MiniAppDescriptor("graf", "Graf", () => new StubMiniApp(svc));
        var placement = MiniAppPlacement.NewColumn;
        var dialogs = new FakeDialogs { MiniAppToPick = (app, placement) };
        var vm = CreateVM(services: svc, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(desktop, app)]),
                          dialogs: dialogs);
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);   // výchozí rozložení otevře Plochu

        vm.PickMiniAppCommand.Execute(null);

        Assert.StartsWith("plocha", Assert.Single(dialogs.OfferedTargets!).DocumentTitles[0]);
        var pane = host.Panes[^1];
        Assert.StartsWith("graf", pane.ContentId);
        Assert.Same(placement, pane.Placement);
    }

    [Fact]
    public void PickMiniApp_Cancelled_OpensNothing()
    {
        var svc = MockServices();
        var app = new MiniAppDescriptor("graf", "Graf", () => new StubMiniApp(svc));
        var vm = CreateVM(services: svc, miniAppCatalog: MiniAppCatalog.Build([new SingleProvider(app)]),
                          dialogs: new FakeDialogs());
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        vm.PickMiniAppCommand.Execute(null);

        Assert.Empty(host.Panes);
    }

    [Fact]
    public void ChangePassword_Changed_ForgetsOwnRememberedPassword()
    {
        var prefs = PrefsRemembering("novak");
        var dialogs = new FakeDialogs { PasswordChanges = true };
        var vm = CreateVM(mlRest: SessionOf("novak"), prefs: prefs, dialogs: dialogs);

        vm.ChangePasswordCommand.Execute(null);

        Assert.Equal(["novak"], dialogs.PasswordChangedFor);
        Assert.Null(prefs.RememberedPasswordFor(Server, Port));
    }

    [Fact]
    public void ChangePassword_Cancelled_KeepsRememberedPassword()
    {
        var prefs = PrefsRemembering("novak");
        var blob = prefs.RememberedPasswordFor(Server, Port);
        var vm = CreateVM(mlRest: SessionOf("novak"), prefs: prefs, dialogs: new FakeDialogs());

        vm.ChangePasswordCommand.Execute(null);

        Assert.Equal(blob, prefs.RememberedPasswordFor(Server, Port));
    }

    [Fact]
    public void UserProfile_ChangesPasswordThroughShell_ForgetsOwnRememberedPassword()
    {
        // Profil nemá preference shellu, změnu hesla dostane od shellu jako delegát.
        var prefs = PrefsRemembering("novak");
        var dialogs = new FakeDialogs { PasswordChanges = true };
        var vm = CreateVM(mlRest: SessionOf("novak"), prefs: prefs, dialogs: dialogs);

        vm.ShowUserProfileCommand.Execute(null);
        var changed = dialogs.ProfileChangePassword!();

        Assert.True(changed);
        Assert.Null(prefs.RememberedPasswordFor(Server, Port));
        Assert.Equal("", dialogs.ProfileDefaultWindow);   // výchozí okno není nastavené
    }

    [Fact]
    public async Task ShellChangelog_SnoozeChecked_IsSaved()
    {
        var source = Substitute.For<IChangelogSource>();
        source.LoadAsync("EDISONMAIN").Returns(new ChangelogContent("EDISONMAIN", "nové změny", "historie", "hash-1"));
        var userPrefs = Substitute.For<IUserPrefsService>();
        var dialogs = new FakeDialogs { SnoozeOnClose = true };
        var vm = CreateVM(changelogSource: source, userPrefs: userPrefs, dialogs: dialogs);

        await vm.ShowShellChangelogIfAnyAsync();

        Assert.Equal(new[] { ("EDISON", false) }, dialogs.ShownChangelogs);
        await userPrefs.Received(1).SaveAsync("EDISONMAIN2", "ChangelogSnooze",
            Arg.Is<JsonNode>(n => n["EDISONMAIN"]!.GetValue<string>() == "hash-1"));
    }

    [Fact]
    public async Task ShellChangelog_SnoozeNotChecked_SavesNothing()
    {
        var source = Substitute.For<IChangelogSource>();
        source.LoadAsync("EDISONMAIN").Returns(new ChangelogContent("EDISONMAIN", "nové změny", "historie", "hash-1"));
        var userPrefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(changelogSource: source, userPrefs: userPrefs, dialogs: new FakeDialogs());

        await vm.ShowShellChangelogIfAnyAsync();

        await userPrefs.DidNotReceive().SaveAsync("EDISONMAIN2", "ChangelogSnooze", Arg.Any<JsonNode>());
    }

    [Fact]
    public async Task ShellChangelogFromMenu_AfterSnooze_ShowsItChecked()
    {
        // Vynucené zobrazení z Nápovědy ukáže odložení, aby ho uživatel mohl zrušit.
        var source = Substitute.For<IChangelogSource>();
        source.LoadAsync("EDISONMAIN").Returns(new ChangelogContent("EDISONMAIN", "nové změny", "historie", "hash-1"));
        var dialogs = new FakeDialogs { SnoozeOnClose = true };
        var vm = CreateVM(changelogSource: source, userPrefs: Substitute.For<IUserPrefsService>(), dialogs: dialogs);
        await vm.ShowShellChangelogIfAnyAsync();

        vm.ShowShellChangelogCommand.Execute(null);

        Assert.Equal(new[] { ("EDISON", false), ("EDISON", true) }, dialogs.ShownChangelogs);
    }

    [Fact]
    public void ConnectToDatabase_PickedConnection_StartsInstanceAgainstIt()
    {
        var started = new List<ProcessStartInfo>();
        var dialogs = new FakeDialogs { ConnectionToPick = new ConnectionChoice("Test", "srv.example", 57772, false) };
        var vm = CreateVM(dialogs: dialogs, startProcess: started.Add);

        vm.ConnectToDatabaseCommand.Execute(null);

        var info = Assert.Single(started);
        Assert.Equal(new ConnectionOverride("srv.example", 57772).ToArguments(), info.ArgumentList);
    }

    [Fact]
    public void ConnectToDatabase_Cancelled_StartsNothing()
    {
        var started = new List<ProcessStartInfo>();
        var vm = CreateVM(dialogs: new FakeDialogs(), startProcess: started.Add);

        vm.ConnectToDatabaseCommand.Execute(null);

        Assert.Empty(started);
    }
}
