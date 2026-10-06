using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Licence session bez ViewModelu: chip upozornění, stažení z M-line po přihlášení a „Aktualizovat
/// licenci". Přestavbu session tu zastupuje záznam do <see cref="_calls"/>; návaznost na VM testuje
/// <see cref="EdisonMainViewModelLicenceTests"/>.
/// </summary>
public class SessionLicenceCoordinatorTests
{
    private readonly IMLRest _rest = MLRestMocks.Create();
    private readonly List<string> _calls = [];
    private readonly SessionEpoch _session;
    private int _noticeChanges;

    public SessionLicenceCoordinatorTests() => _session = new SessionEpoch(() => _rest, () => "CB_DP");

    private SessionLicenceCoordinator Coordinator(Func<IMLRest, LicenceService.MlineSyncResult>? sync = null,
                                                  bool syncsAfterLogin = true, IRestCallRunner? runner = null) =>
        new(runner ?? new InlineRestCallRunner(_rest),
            sync ?? Sync(changed: false),
            _session,
            () => { _calls.Add("rebuild"); return Task.CompletedTask; },
            () => _calls.Add("load"),
            () => syncsAfterLogin,
            () => _noticeChanges++);

    private Func<IMLRest, LicenceService.MlineSyncResult> Sync(bool changed, string error = "") =>
        _ => { _calls.Add("sync"); return new(changed, error); };

    /// <summary>Runner, který každý request pustí až po <see cref="Gate"/> - pomalá M-line nebo agendový balík.</summary>
    private sealed class GatedRunner(IMLRest rest) : IRestCallRunner
    {
        public TaskCompletionSource Gate { get; } = new();

        public async Task<T> RunAsync<T>(Func<IMLRest, T> call)
        {
            await Gate.Task;
            return call(rest);
        }
    }

    private static Lic ValidFor(int days) => new() { ValidityTo = DateTime.Today.AddDays(days).ToString("yyyy-MM-dd") };

    [Fact]
    public async Task RefreshNotice_VehiclesOverLicence_ShowsChipAfterCount()
    {
        var d = _rest.Delimiter;
        _rest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel", Arg.Any<object[]>())
             .Returns(MLRestMocks.Output($"celkem{d}11"));
        var licence = ValidFor(365);
        licence.CountsRZ = 10;
        var coordinator = Coordinator();

        await coordinator.RefreshNoticeAsync(licence);

        Assert.Equal("Překročen počet vozidel", coordinator.Notice?.Short);
        // Bez notifikace by binding chipu zůstal na hodnotě z doby před načtením počtů.
        Assert.Equal(1, _noticeChanges);
        // CountsZAM=0: licence zaměstnance neomezuje, počet se ani nezjišťuje.
        _rest.DidNotReceive().QueryOrThrow("ALVA.MZDY2.MLAPI", "gPocZam", Arg.Any<object[]>());
    }

    [Fact]
    public async Task RefreshNotice_ValidLicence_NoChip()
    {
        var coordinator = Coordinator();

        await coordinator.RefreshNoticeAsync(ValidFor(365));

        Assert.Null(coordinator.Notice);
        Assert.Equal(0, _noticeChanges);
    }

    [Fact]
    public async Task RefreshNotice_SessionChangedBeforeCounts_IgnoresStaleCounts()
    {
        // Počty patří licenci session, ve které se začaly počítat; po přestavbě by chip lhal.
        var d = _rest.Delimiter;
        _rest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel", Arg.Any<object[]>())
             .Returns(MLRestMocks.Output($"celkem{d}11"));
        var runner = new GatedRunner(_rest);
        var coordinator = Coordinator(runner: runner);
        var licence = ValidFor(365);
        licence.CountsRZ = 10;

        var refreshing = coordinator.RefreshNoticeAsync(licence);
        _session.Invalidate();
        runner.Gate.SetResult();
        await refreshing;

        Assert.Null(coordinator.Notice);
    }

    [Fact]
    public async Task SyncAfterLogin_NotAllowed_NoDownload()
    {
        await Coordinator(syncsAfterLogin: false).SyncAfterLoginAsync();

        Assert.Empty(_calls);
    }

    [Fact]
    public async Task SyncAfterLogin_NothingNew_NoRebuild()
    {
        await Coordinator().SyncAfterLoginAsync();

        Assert.Equal(["sync"], _calls);
    }

    [Fact]
    public async Task SyncAfterLogin_NewLicenceDespiteFeedbackError_Rebuilds()
    {
        // EDUsers licenci uloží dřív, než selže feedback - nová licence se promítnout musí.
        await Coordinator(Sync(changed: true, error: "feedback selhal")).SyncAfterLoginAsync();

        Assert.Equal(["sync", "load", "rebuild"], _calls);
    }

    [Fact]
    public async Task SyncAfterLogin_DownloadFails_Swallowed()
    {
        // Legacy EDUsers.Login výsledek stažení nekontroloval; start se kvůli M-line nezastaví.
        var ex = await Record.ExceptionAsync(() =>
            Coordinator(_ => throw new InvalidOperationException("timeout")).SyncAfterLoginAsync());

        Assert.Null(ex);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task SyncAfterLogin_SessionChangedDuringDownload_DoesNotRebuild()
    {
        var runner = new GatedRunner(_rest);
        var coordinator = Coordinator(Sync(changed: true), runner: runner);

        var syncing = coordinator.SyncAfterLoginAsync();
        _session.Invalidate();   // přepnutí firmy nebo uživatele začalo
        runner.Gate.SetResult();
        await syncing;

        Assert.Equal(["sync"], _calls);
    }

    [Fact]
    public async Task Refresh_EdUsersError_RebuildsAndReturnsError()
    {
        // Chybu ukáže VM až po přestavbě - licence mohla být uložena a selhat až feedback.
        int busy = 0;
        var result = await Coordinator(Sync(changed: true, error: "feedback selhal"))
            .RefreshAsync(() => { busy++; return Substitute.For<IDisposable>(); });

        Assert.Equal("feedback selhal", result.Error);
        Assert.Equal(["sync", "load", "rebuild"], _calls);
        Assert.Equal(1, busy);
    }

    [Fact]
    public async Task Refresh_SyncThrows_NoRebuild()
    {
        // Selhal už dotaz na seznam licencí - nic se nestáhlo, session zůstane.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator(_ => throw new InvalidOperationException("server nedostupný"))
                .RefreshAsync(() => Substitute.For<IDisposable>()));

        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Refresh_InvalidatesBeforeDownload_AndReleasesBusyBeforeRebuild()
    {
        // Pomalý sync po přihlášení nesmí doběhnout do souběžné přestavby, a overlay „Akce probíhá"
        // nemá refcount - přestavba má vlastní, takže scope stažení musí skončit před ní.
        var stamp = _session.Capture();
        bool invalidatedBeforeDownload = false, busyReleasedBeforeRebuild = false, busyOpen = false;
        var coordinator = new SessionLicenceCoordinator(new InlineRestCallRunner(_rest),
            _ => { invalidatedBeforeDownload = !_session.IsCurrent(stamp); return new(false, ""); },
            _session,
            () => { busyReleasedBeforeRebuild = !busyOpen; return Task.CompletedTask; },
            () => { }, () => true, () => { });

        await coordinator.RefreshAsync(() => { busyOpen = true; return new Scope(() => busyOpen = false); });

        Assert.True(invalidatedBeforeDownload);
        Assert.True(busyReleasedBeforeRebuild);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
