using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Licence v hlavním okně z pohledu VM: příkaz „Aktualizovat licenci", stažení po startu a po přihlášení
/// jiného uživatele a souběh s přepnutím firmy. Logiku chipu a stažení testuje <see cref="SessionLicenceCoordinatorTests"/>.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainViewModelLicenceTests
{
    private static EDServices Services(List<string> calls, bool isAdmin = false, bool mayUpdate = false,
                                       IPopupDisplayer? popup = null)
    {
        var svc = MockServices(popup: popup);
        svc.IsAdmin = isAdmin;
        svc.MayUpdate = mayUpdate;
        svc.LicenceLoader = () => { calls.Add("load"); return new Lic(); };
        return svc;
    }

    private static Func<IMLRest, LicenceService.MlineSyncResult> SyncSpy(List<string> calls, bool changed = false,
                                                                         string error = "") =>
        _ => { calls.Add("sync"); return new(changed, error); };

    /// <summary>Inline runner, jen stažení z M-line čeká na <see cref="Release"/> - pomalá M-line.</summary>
    private sealed class GatedSyncRunner(IMLRest rest) : IRestCallRunner
    {
        private readonly TaskCompletionSource _gate = new();
        public void Release() => _gate.TrySetResult();

        public Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            if (typeof(T) == typeof(LicenceService.MlineSyncResult))
                return _gate.Task.ContinueWith(_ => call(rest), CancellationToken.None,
                                               TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            try { return Task.FromResult(call(rest)); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }
    }

    // Strom napoprvé projde, podruhé (rebuild) spadne.
    private static IModuleTreeLoader TreeFailingOnSecondLoad()
    {
        var mockTree = Substitute.For<IModuleTreeLoader>();
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        int mainCalls = 0;
        mockTree.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(_ =>
            ++mainCalls == 1 ? [] : throw new InvalidOperationException("server je mimo"));
        return mockTree;
    }

    [Fact]
    public void RefreshLicence_DownloadsFromMlineThenRebuilds()
    {
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(services: Services(calls, popup: popup), licenceSync: SyncSpy(calls));

        vm.RefreshLicenceCommand.Execute(null);

        Assert.Equal(["sync", "load"], calls);
        popup.Received(1).Info("Licence byla aktualizována.");
    }

    [Fact]
    public void RefreshLicence_SyncThrows_ErrorPopup_NoRebuild()
    {
        // Selhal už dotaz na seznam licencí - nic se nestáhlo, session zůstane.
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(services: Services(calls, popup: popup),
                          licenceSync: _ => throw new InvalidOperationException("server nedostupný"));

        vm.RefreshLicenceCommand.Execute(null);

        Assert.Empty(calls);
        popup.Received(1).Error(Arg.Is<InvalidOperationException>(e => e.Message == "server nedostupný"));
        popup.DidNotReceive().Info(Arg.Any<string>(), Arg.Any<System.Windows.Window?>());
    }

    [Fact]
    public void RefreshLicence_EdUsersError_RebuildsThenShowsError()
    {
        // EDUsers licenci uloží dřív, než selže feedback (EDUsers.vb:324-327) - přestavět a pak ukázat chybu.
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(services: Services(calls, popup: popup),
                          licenceSync: SyncSpy(calls, changed: true, error: "feedback selhal"));

        vm.RefreshLicenceCommand.Execute(null);

        Assert.Equal(["sync", "load"], calls);
        popup.Received(1).Error(Arg.Is<InvalidOperationException>(e => e.Message == "feedback selhal"));
        popup.DidNotReceive().Info(Arg.Any<string>(), Arg.Any<System.Windows.Window?>());
    }

    [Fact]
    public void RefreshLicence_RegularUser_StillDownloads()
    {
        // Legacy volá UpdateLicFromMlineServer(allUsers:=True) - „Aktualizovat licenci" smí každý.
        var calls = new List<string>();
        var vm = CreateVM(services: Services(calls), licenceSync: SyncSpy(calls));

        vm.RefreshLicenceCommand.Execute(null);

        Assert.Contains("sync", calls);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Initialize_AdminOrMayUpdate_DownloadsAfterLogin(bool isAdmin, bool mayUpdate)
    {
        var calls = new List<string>();

        CreateVM(services: Services(calls, isAdmin, mayUpdate), licenceSync: SyncSpy(calls));

        // Nic nového z M-line → session se nepřestavuje (licenci dal bootstrapper, loader nevolán).
        Assert.Equal(["sync"], calls);
    }

    [Fact]
    public void Initialize_NewLicenceFromMline_RebuildsSession()
    {
        var calls = new List<string>();
        var svc = Services(calls, isAdmin: true);

        CreateVM(services: svc, licenceSync: SyncSpy(calls, changed: true));

        Assert.Equal(["sync", "load"], calls);
    }

    [Fact]
    public async Task SlowSync_FinishingDuringCompanySwitchDialog_DoesNotRebuild()
    {
        // Dialog licenčního retry uvnitř synchronního switche pumpuje dispatcher; MLRest je už na cílové firmě.
        // Doběhlý sync nesmí načíst licenci cizí firmy ani přestavět stav, který switch pak vrátí.
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>()).Returns(MLRestMocks.Output("-1"));
        var runner = new GatedSyncRunner(ml);
        popup.Confirm(Arg.Any<string>(), Arg.Any<System.Windows.Window?>()).Returns(_ => { runner.Release(); return false; });
        var svc = Services(calls, popup: popup);
        var vm = CreateVM(services: svc, mlRest: ml, restRunner: runner, initialLicence: new Lic(),
                          licenceSync: SyncSpy(calls, changed: true));
        svc.IsAdmin = true;
        var sync = vm.SyncMlineLicenceAfterLoginAsync();

        await vm.SwitchCompanyAsync(1);
        await sync;

        // "load" jen z VerifyLic přepnutí; po syncu už žádný.
        Assert.Equal(["load", "sync"], calls);
        Assert.Equal("CB_DP", vm.Idfa);
    }

    [Fact]
    public async Task SlowSync_AfterCancelledUserSwitch_DoesNotRebuild()
    {
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>(), Arg.Any<System.Windows.Window?>()).Returns(true);
        var ml = MockMLRest();
        var runner = new GatedSyncRunner(ml);
        var svc = Services(calls, popup: popup);
        var vm = CreateVM(services: svc, mlRest: ml, restRunner: runner, licenceSync: SyncSpy(calls, changed: true),
                          switchUser: () => new(SwitchUserResult.Cancelled));
        svc.IsAdmin = true;
        var sync = vm.SyncMlineLicenceAfterLoginAsync();

        await vm.LoginAsAnotherUserAsync();
        runner.Release();
        await sync;

        Assert.Equal(["sync"], calls);
    }

    [Fact]
    public void Notice_RebuildLoadFails_ChipFollowsNewLicence()
    {
        // Upozornění patří licenci session, ne stromům: po selhaném loadu nesmí zůstat chip staré licence.
        var calls = new List<string>();
        var ending = new Lic { ValidityTo = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") };
        var vm = CreateVM(services: Services(calls), treeLoader: TreeFailingOnSecondLoad(), initialLicence: ending);
        Assert.Equal("Licence skončí za 3 dny", vm.LicenceNoticeText);   // předpoklad

        vm.RefreshLicenceCommand.Execute(null);

        Assert.Equal("", vm.LicenceNoticeText);
    }

    // Licenci cílové firmy zapisuje LicenceVerifier při Accepted, ne až přestavba shellu: legacy
    // moduly se zabíjejí hned po Accepted a launch argumenty agend musí sedět s MLRest.Idfa.
    [Fact]
    public async Task SwitchCompany_Accepted_TargetLicenceBeforeRebuild()
    {
        var ml = MockMLRest();
        ml.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>()).Returns(MLRestMocks.Output("1"));
        var ending = new Lic { ValidityTo = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") };
        var target = new Lic { ValidityTo = DateTime.Today.AddDays(365).ToString("yyyy-MM-dd") };
        var svc = MockServices();
        svc.LicenceLoader = () => target;
        Lic? atAccepted = null;
        var vm = CreateVM(services: svc, mlRest: ml, initialLicence: ending,
                          closeLegacyModules: () => atAccepted = svc.ActiveLicence);
        Assert.Equal("Licence skončí za 3 dny", vm.LicenceNoticeText);   // předpoklad

        await vm.SwitchCompanyAsync(1);

        Assert.Same(target, atAccepted);
        Assert.Equal("", vm.LicenceNoticeText);   // přestavba čte licenci ze služeb
    }

    [Fact]
    public async Task LoginAsAnotherUser_Admin_DownloadsFromMline()
    {
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = Services(calls, popup: popup);
        var vm = CreateVM(services: svc, licenceSync: SyncSpy(calls),
                          switchUser: () => { svc.IsAdmin = true; svc.ActiveLicence = new Lic(); return new(SwitchUserResult.Success); });
        Assert.Empty(calls);   // předpoklad: původní uživatel nestahoval

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal(["sync"], calls);
    }

    [Fact]
    public async Task Notice_SetByInitialize_NotifiesBinding()
    {
        // Binding chipu čte hodnotu už při nastavení DataContextu, licence ale přijde až z InitializeAsync.
        var licence = new Lic { ValidityTo = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") };
        var vm = CreateVM(initialLicence: licence, initialize: false);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        await vm.InitializeAsync();

        Assert.Contains(nameof(vm.LicenceNoticeText), notified);
        Assert.Contains(nameof(vm.LicenceNoticeTooltip), notified);
    }

    [Fact]
    public void Notice_LicenceEndingSoon_ShowsChip()
    {
        var licence = new Lic { ValidityTo = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") };

        var vm = CreateVM(initialLicence: licence);

        Assert.Equal("Licence skončí za 3 dny", vm.LicenceNoticeText);
        Assert.Equal("Licence EDISON skončí za 3 dny.", vm.LicenceNoticeTooltip);
    }

}
