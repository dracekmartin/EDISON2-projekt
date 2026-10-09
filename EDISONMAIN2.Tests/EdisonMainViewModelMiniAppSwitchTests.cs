using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;
using NSubstitute;
using static ALVA.EDISONAPI.Database;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;
using static EDISONMAIN2.Tests.MiniAppTestDoubles;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Dashboardy při zavření ukládají layout EDListu přes sdílený MLRest, takže se dock musí zavřít ještě
/// nad starou session (jinak zápis odejde pod novou firmu nebo uživatele) a po zrušeném přepnutí vrátit.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainViewModelMiniAppSwitchTests
{
    private sealed class ClosingProbe(EDServices services, Action onClosed) : EDMiniAppViewModel(services)
    {
        public override string Title => "sonda";
        public override void OnClosed() => onClosed();
    }

    private sealed record ClosedUnder(IMLRest Session, string Idfa);

    // Připnutá sonda: výchozí rozložení ji otevře a při zavření zapíše session, která v tu chvíli platí.
    private static MiniAppCatalog ProbeCatalog(EDServices svc, List<ClosedUnder> closed) =>
        MiniAppCatalog.Build([new SingleProvider(new MiniAppDescriptor("sonda", "Sonda",
            () => new ClosingProbe(svc, () => closed.Add(new(svc.RequireMLRest, svc.RequireMLRest.Idfa))), Pinned: true))]);

    private static IPopupDisplayer ConfirmingPopup()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        return popup;
    }

    private static ILocalPrefs Prefs()
    {
        var prefs = Substitute.For<ILocalPrefs>();
        prefs.MiniAppLayouts.Returns(new Dictionary<string, string>());
        return prefs;
    }

    private static int OpenPanes(RecordingHost host) => host.Panes.Count(p => !p.IsClosed);

    // Mock MLRest vrací pro Server a LoginName "" a pro Port 0.
    private static string DockKey(string idfa) => new DockScope("", 0, idfa, "").Key;

    // Host s odlišeným layoutem při každém uložení: druhé uložení prozradí, pod čí session dock po zrušení patří.
    private static IMiniAppHost HostSaving(params string[] layouts)
    {
        var host = Substitute.For<IMiniAppHost>();
        host.SaveLayout().Returns(layouts[0], layouts[1..]);
        return host;
    }

    [Fact]
    public async Task SwitchCompany_ClosesMiniAppsBeforeIdfaChanges()
    {
        var svc = MockServices(popup: ConfirmingPopup());
        var closed = new List<ClosedUnder>();
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, closed));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await vm.SwitchCompanyAsync(1);

        Assert.Equal("JI_DP", vm.Idfa);   // předpoklad: přepnutí proběhlo
        Assert.Equal("CB_DP", Assert.Single(closed).Idfa);
        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task SwitchCompany_Refused_ReopensMiniApps()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("-1"));
        var licence = new Lic { CountsLOGIN = 5 };
        var svc = MockServices(popup: ConfirmingPopup());
        svc.ActiveLicence = licence;
        svc.LicenceLoader = () => licence;
        svc.IsAdmin = false;
        var closed = new List<ClosedUnder>();
        var vm = CreateVM(services: svc, mlRest: ml, initialLicence: licence, prefs: Prefs(),
                          miniAppCatalog: ProbeCatalog(svc, closed));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await vm.SwitchCompanyAsync(1);

        Assert.Equal("CB_DP", vm.Idfa);   // předpoklad: licence přepnutí odmítla
        Assert.Equal("CB_DP", Assert.Single(closed).Idfa);
        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task SwitchCompany_Refused_DockBelongsToOriginalCompanyAgain()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output("-1"));
        var licence = new Lic { CountsLOGIN = 5 };
        var svc = MockServices(popup: ConfirmingPopup());
        svc.ActiveLicence = licence;
        svc.LicenceLoader = () => licence;
        svc.IsAdmin = false;
        var prefs = Prefs();
        var vm = CreateVM(services: svc, mlRest: ml, initialLicence: licence, prefs: prefs);
        vm.AttachMiniAppHost(HostSaving("<pred-prepnutim/>", "<po-zruseni/>"));

        await vm.SwitchCompanyAsync(1);
        vm.OnShellClosed();

        Assert.Equal("<po-zruseni/>", prefs.MiniAppLayouts![DockKey("CB_DP")]);
        Assert.DoesNotContain(DockKey("JI_DP"), prefs.MiniAppLayouts.Keys);
    }

    [Fact]
    public async Task SwitchCompany_SwitchThrowsBeforeIdfaChanges_ReopensMiniApps()
    {
        // Okno shellu při zavření selže: Idfa se nezměnila a session staré firmy běží dál.
        var svc = MockServices(popup: ConfirmingPopup());
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, []),
                          closeAllWindows: () => throw new InvalidOperationException("okno"));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SwitchCompanyAsync(1));

        Assert.Equal("CB_DP", svc.RequireMLRest.Idfa);   // předpoklad
        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task SwitchCompany_SwitchThrowsAfterIdfaChanged_LeavesDockClosed()
    {
        // VerifyLic selže až po výměně Idfa a CompanySwitcher ji nevrací: dock staré firmy by mluvil s cizí.
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
            .Returns(_ => throw new InvalidOperationException("síť"));
        var licence = new Lic { CountsLOGIN = 5 };
        var svc = MockServices(popup: ConfirmingPopup());
        svc.ActiveLicence = licence;
        svc.LicenceLoader = () => licence;
        var vm = CreateVM(services: svc, mlRest: ml, initialLicence: licence, prefs: Prefs(),
                          miniAppCatalog: ProbeCatalog(svc, []));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SwitchCompanyAsync(1));

        Assert.Equal("JI_DP", ml.Idfa);   // předpoklad
        Assert.Equal(0, OpenPanes(host));
    }

    [Fact]
    public async Task LoginAsAnotherUser_ClosesMiniAppsBeforeSessionChanges()
    {
        var svc = MockServices(popup: ConfirmingPopup());
        var original = MockMLRest();
        var closed = new List<ClosedUnder>();
        var vm = CreateVM(services: svc, mlRest: original, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, closed),
                          switchUser: () =>
                          {
                              svc.MLRest = MockMLRest("JI_DP");   // SessionFactory vymění MLRest za session nového uživatele
                              return new(SwitchUserResult.Success, [new CompanyStruct { Id = "JI_DP", Database = "JI_DP" }]);
                          });
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal("JI_DP", vm.Idfa);   // předpoklad: přepnutí proběhlo
        var (session, idfa) = Assert.Single(closed);
        Assert.Same(original, session);
        Assert.Equal("CB_DP", idfa);
        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task LoginAsAnotherUser_Cancelled_ReopensMiniApps()
    {
        var svc = MockServices(popup: ConfirmingPopup());
        var host = new RecordingHost();
        int? openDuringLogin = null;
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, []),
                          switchUser: () =>
                          {
                              openDuringLogin = OpenPanes(host);
                              return new(SwitchUserResult.Cancelled);
                          });
        vm.AttachMiniAppHost(host);

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal(0, openDuringLogin);
        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task LoginAsAnotherUser_Cancelled_DockBelongsToOriginalSessionAgain()
    {
        var svc = MockServices(popup: ConfirmingPopup());
        var prefs = Prefs();
        var vm = CreateVM(services: svc, prefs: prefs, switchUser: () => new(SwitchUserResult.Cancelled));
        vm.AttachMiniAppHost(HostSaving("<pred-prepnutim/>", "<po-zruseni/>"));

        await vm.LoginAsAnotherUserAsync();
        vm.OnShellClosed();

        Assert.Equal("<po-zruseni/>", prefs.MiniAppLayouts![DockKey("CB_DP")]);
    }

    [Fact]
    public async Task LoginAsAnotherUser_SwitchThrowsBeforeSessionChanges_ReopensMiniApps()
    {
        // Seznam firem nového uživatele selže ještě před výměnou session, stará běží dál.
        var svc = MockServices(popup: ConfirmingPopup());
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, []),
                          switchUser: () => throw new InvalidOperationException("síť"));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoginAsAnotherUserAsync());

        Assert.Equal(1, OpenPanes(host));
    }

    [Fact]
    public async Task LoginAsAnotherUser_ShellClosedDuringLogin_DoesNotReopenMiniApps()
    {
        // Force logout zavře shell i login dialog, který pak vrátí Cancelled.
        var svc = MockServices(popup: ConfirmingPopup());
        var host = new RecordingHost();
        EdisonMainViewModel? vmRef = null;
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, []),
                          switchUser: () =>
                          {
                              vmRef!.OnShellClosed();
                              return new(SwitchUserResult.Cancelled);
                          });
        vmRef = vm;
        vm.AttachMiniAppHost(host);

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal(0, OpenPanes(host));
    }

    [Theory]
    [InlineData(SwitchUserResult.LicenceRefused)]
    [InlineData(SwitchUserResult.LicenceCancelled)]
    public async Task LoginAsAnotherUser_LicenceFailure_LeavesDockClosed(SwitchUserResult result)
    {
        // MLRest už patří novému uživateli a shell končí: obnovený dock by mluvil s jeho session.
        var svc = MockServices(popup: ConfirmingPopup());
        var shutdown = Substitute.For<Action>();
        var vm = CreateVM(services: svc, prefs: Prefs(), miniAppCatalog: ProbeCatalog(svc, []),
                          forceShutdown: shutdown, switchUser: () => new(result));
        var host = new RecordingHost();
        vm.AttachMiniAppHost(host);

        await vm.LoginAsAnotherUserAsync();

        shutdown.Received(1).Invoke();
        Assert.Equal(0, OpenPanes(host));
    }
}
