using System.IO;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Reálné tělo <c>SessionOpener.CheckForUpdate</c> (mapování výsledku Check na popup +
/// copy/launch + exit) - FakeUpdater řídí verze přes seamy Updateru, opener přepisuje
/// jen CreateUpdater.
/// </summary>
public class SessionOpenerUpdateTests
{
    private sealed class FakeUpdater : Updater
    {
        public UpdateSettings Settings { get; set; } = new("260825.1", @"\\server\Edison", "");
        public string LocalVersion { get; set; } = "260825.1";
        public string SourceVersion { get; set; } = "260825.1";
        public string StampSeen { get; set; } = "";
        public bool CopyThrows { get; set; }
        public EDControls.IMLRest? LoadedFrom { get; private set; }
        public readonly List<string> Calls = [];

        public override UpdateSettings LoadSettings(EDControls.IMLRest mlRest)
        {
            LoadedFrom = mlRest;
            return Settings;
        }
        protected override string GetLocalVersion() => LocalVersion;
        protected override string GetSourceVersion(string sourceDir) => SourceVersion;
        protected override string GetForcedStampSeen() => StampSeen;

        public override void CopyUpdater(string sourceDir)
        {
            Calls.Add("copy");
            if (CopyThrows) throw new IOException("EDUPDATE.exe je zamčený");
        }

        public override void LaunchUpdate(string sourceDir, string version, bool forced, string forcedStamp) =>
            Calls.Add($"launch:{version}|forced={forced}|stamp={forcedStamp}");
    }

    private sealed class UpdateCheckOpener(EDServices services, Updater updater, List<string>? reportLog = null)
        : SessionOpener(services, new StartupOptions(), _ => { })
    {
        protected override Updater CreateUpdater() => updater;

        public int Reports { get; private set; }

        protected override void ReportUpdaterFailure()
        {
            Reports++;
            reportLog?.Add("report");
        }

        public (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) Call(EDControls.IMLRest? sessionRest = null,
                                                                         bool startNotice = true) =>
            CheckForUpdate(
                new SessionContext(sessionRest ?? MLRestMocks.Create(),
                    new LoginResult(7, "bob", "Bob", false, "", false, true, "")),
                startNotice);
    }

    private static (UpdateCheckOpener Opener, IPopupDisplayer Popup) Build(FakeUpdater updater)
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), popup);
        return (new UpdateCheckOpener(services, updater), popup);
    }

    [Fact]
    public void UpdateRequired_InformsCopiesLaunchesAndExits()
    {
        var updater = new FakeUpdater { SourceVersion = "260901.1" };
        var (opener, popup) = Build(updater);

        var (shouldExit, notice) = opener.Call();

        Assert.True(shouldExit);
        Assert.Null(notice);
        popup.Received(1).InfoSystem(Arg.Is<string>(s => s.Contains("260901.1")));
        Assert.Equal(["copy", "launch:260901.1|forced=False|stamp="], updater.Calls);
    }

    [Fact]
    public void UpdateRequired_ReportsUpdaterFailureBeforeCopyAndLaunch()
    {
        // Spuštěný updater log předchozího běhu přepíše, hlášení musí být první.
        var updater = new FakeUpdater { SourceVersion = "260901.1" };
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        var opener = new UpdateCheckOpener(services, updater, updater.Calls);

        opener.Call();

        Assert.Equal(["report", "copy", "launch:260901.1|forced=False|stamp="], updater.Calls);
    }

    [Fact]
    public void ServerInstallation_DoesNotReportUpdaterFailure()
    {
        var updater = new FakeUpdater { Settings = new UpdateSettings("260825.1", @"\\server\Edison", "") { ServerInstallation = true } };
        var (opener, _) = Build(updater);

        opener.Call();

        Assert.Equal(0, opener.Reports);
    }

    [Fact]
    public void UpdateCheck_LoadsSettingsOnLocksZeroClone()
    {
        // Session po licenci má Locks=1 a verzi stanice - po FULL vydání by LoadSettings odmítl
        // server version gate a shell by skončil dřív, než spustí updater.
        var updater = new FakeUpdater { SourceVersion = "260901.1" };
        var (opener, _) = Build(updater);
        var sessionRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        sessionRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);

        opener.Call(sessionRest);

        Assert.Same(clone, updater.LoadedFrom);
    }

    [Fact]
    public void UpdateRequired_CopyFails_ExplainsAndStillExits()
    {
        // Bez copy/launch by se shell točil v detekci - exit platí, uživatel dostane hlášku.
        var updater = new FakeUpdater { SourceVersion = "260901.1", CopyThrows = true };
        var (opener, popup) = Build(updater);

        var (shouldExit, _) = opener.Call();

        Assert.True(shouldExit);
        popup.Received(1).InfoSystem(Arg.Is<string>(s => s.Contains("Aktualizaci nelze spustit")));
        Assert.Equal(["copy"], updater.Calls); // launch po selhané kopii neběží
    }

    [Fact]
    public void ForcedWave_LaunchesForcedWithStamp()
    {
        var updater = new FakeUpdater
        {
            Settings = new UpdateSettings("260825.1", @"\\server\Edison", "", ForcedStamp: "66234,2"),
            StampSeen = "66230,1", // lokální stopa nesedí s razítkem z Caché
        };
        var (opener, popup) = Build(updater);

        var (shouldExit, _) = opener.Call();

        Assert.True(shouldExit);
        Assert.Equal(["copy", "launch:260825.1|forced=True|stamp=66234,2"], updater.Calls);
    }

    [Fact]
    public void Broken_InformsAndExitsWithoutLaunching()
    {
        var updater = new FakeUpdater { SourceVersion = "" }; // zdrojová verze nečitelná
        var (opener, popup) = Build(updater);

        var (shouldExit, notice) = opener.Call();

        Assert.True(shouldExit);
        Assert.Null(notice);
        popup.Received(1).InfoSystem(Arg.Is<string>(s => s.Contains("serverovou verzi")));
        Assert.Empty(updater.Calls);
    }

    [Fact]
    public async Task NoUpdate_StartsNoticeOnlyCheckAndContinues()
    {
        var updater = new FakeUpdater(); // verze shodné → None
        var (opener, popup) = Build(updater);

        var (shouldExit, notice) = opener.Call();

        Assert.False(shouldExit);
        Assert.NotNull(notice);
        // DEBUG lokální verze je dev → notice bez sítě vrátí Unknown; podstatné je, že běží.
        Assert.Equal(ReleaseNoticeOutcome.Unknown, (await notice).Outcome);
        popup.DidNotReceiveWithAnyArgs().InfoSystem(default!);
        Assert.Empty(updater.Calls);
    }

    [Fact]
    public void NoUpdate_WithoutNoticeRequest_StartsNoNoticeCheck()
    {
        // Přihlášení jiného uživatele status bar nepřestaví, dotaz na release server by byl zbytečný.
        var (opener, _) = Build(new FakeUpdater());

        var (shouldExit, notice) = opener.Call(startNotice: false);

        Assert.False(shouldExit);
        Assert.Null(notice);
    }
}
