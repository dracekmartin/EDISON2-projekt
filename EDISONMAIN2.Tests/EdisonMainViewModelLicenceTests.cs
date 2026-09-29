using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDTesting;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Licence v hlavním okně: „Aktualizovat licenci" a login admina stahují z M-line (legacy
/// <c>EDUsers.UpdateLicFromMlineServer</c>), chip upozornění nahrazuje nástěnku starého shellu.
/// </summary>
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
    public void Initialize_RegularUser_NoDownload()
    {
        var calls = new List<string>();

        CreateVM(services: Services(calls), licenceSync: SyncSpy(calls));

        Assert.Empty(calls);
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
    public void Initialize_NewLicenceDespiteFeedbackError_RebuildsSession()
    {
        var calls = new List<string>();

        CreateVM(services: Services(calls, isAdmin: true), licenceSync: SyncSpy(calls, changed: true, error: "feedback selhal"));

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
                          switchUser: () => new(SwitchUserResult.Cancelled, null));
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

    [Fact]
    public void Initialize_DownloadFails_Swallowed()
    {
        // Legacy EDUsers.Login výsledek stažení nekontroloval; start se kvůli M-line nezastaví.
        var popup = Substitute.For<IPopupDisplayer>();
        var svc = Services([], isAdmin: true, popup: popup);

        var vm = CreateVM(services: svc, licenceSync: _ => throw new InvalidOperationException("timeout"));

        Assert.NotNull(vm);
        popup.DidNotReceiveWithAnyArgs().Error(default!);
    }

    [Fact]
    public async Task LoginAsAnotherUser_Admin_DownloadsFromMline()
    {
        var calls = new List<string>();
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var svc = Services(calls, popup: popup);
        var vm = CreateVM(services: svc, licenceSync: SyncSpy(calls),
                          switchUser: () => { svc.IsAdmin = true; return new(SwitchUserResult.Success, new Lic()); });
        Assert.Empty(calls);   // předpoklad: původní uživatel nestahoval

        await vm.LoginAsAnotherUserAsync();

        Assert.Equal(["sync"], calls);
    }

    [Fact]
    public void Notice_LicenceEndingSoon_ShowsChip()
    {
        var licence = new Lic { ValidityTo = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd") };

        var vm = CreateVM(initialLicence: licence);

        Assert.Equal("Licence skončí za 3 dny", vm.LicenceNoticeText);
        Assert.Equal("Licence EDISON skončí za 3 dny.", vm.LicenceNoticeTooltip);
    }

    [Fact]
    public void Notice_VehiclesOverLicence_ShowsChipAfterCount()
    {
        var mlRest = MockMLRest();
        var d = mlRest.Delimiter;
        mlRest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel", Arg.Any<object[]>())
              .Returns(MLRestMocks.Output($"celkem{d}11"));
        var licence = new Lic { ValidityTo = DateTime.Today.AddYears(1).ToString("yyyy-MM-dd"), CountsRZ = 10 };

        var vm = CreateVM(mlRest: mlRest, initialLicence: licence);

        Assert.Equal("Překročen počet vozidel", vm.LicenceNoticeText);
        // CountsZAM=0: licence zaměstnance neomezuje, počet se ani nezjišťuje.
        mlRest.DidNotReceive().QueryOrThrow("ALVA.MZDY2.MLAPI", "gPocZam", Arg.Any<object[]>());
    }

    [Fact]
    public void Notice_ValidLicence_NoChip()
    {
        var vm = CreateVM(initialLicence: new Lic { ValidityTo = DateTime.Today.AddYears(1).ToString("yyyy-MM-dd") });

        Assert.Equal("", vm.LicenceNoticeText);
    }
}
