using System.IO;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Reálné tělo <c>AppBootstrapper.CheckForUpdate</c> (mapování výsledku Check na popup +
/// copy/launch + exit) - FakeUpdater řídí verze přes seamy Updateru, bootstrapper přepisuje
/// jen CreateUpdater.
/// </summary>
public class AppBootstrapperUpdateTests
{
    private sealed class FakeUpdater : Updater
    {
        public UpdateSettings Settings { get; set; } = new(true, "", @"\\server\Edison", "");
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
            if (CopyThrows) throw new IOException("EDUPDATE2.exe je zamčený");
        }

        public override void LaunchUpdate(string sourceDir, string version, bool forced, string forcedStamp) =>
            Calls.Add($"launch:{version}|forced={forced}|stamp={forcedStamp}");
    }

    private sealed class UpdateCheckBootstrapper(Updater updater) : AppBootstrapper
    {
        protected override Updater CreateUpdater() => updater;

        public (bool ShouldExit, Task<ReleaseNoticeResult>? Notice) Call(EDServices services,
            EDControls.IMLRest? sessionRest = null) =>
            CheckForUpdate(
                new SessionContext(sessionRest ?? MLRestMocks.Create(),
                    new LoginResult(7, "bob", "Bob", false, "", false, true, ""), () => null),
                services);
    }

    private static (UpdateCheckBootstrapper Bootstrapper, IPopupDisplayer Popup, EDServices Services) Build(
        FakeUpdater updater)
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), popup);
        return (new UpdateCheckBootstrapper(updater), popup, services);
    }

    [Fact]
    public void UpdateRequired_InformsCopiesLaunchesAndExits()
    {
        var updater = new FakeUpdater { SourceVersion = "260901.1" };
        var (bootstrapper, popup, services) = Build(updater);

        var (shouldExit, notice) = bootstrapper.Call(services);

        Assert.True(shouldExit);
        Assert.Null(notice);
        popup.Received(1).Info(Arg.Is<string>(s => s.Contains("260901.1")));
        Assert.Equal(["copy", "launch:260901.1|forced=False|stamp="], updater.Calls);
    }

    [Fact]
    public void UpdateCheck_LoadsSettingsOnLocksZeroClone()
    {
        // Session po licenci má Locks=1 a verzi stanice - po FULL vydání by LoadSettings odmítl
        // server version gate a shell by skončil dřív, než spustí updater.
        var updater = new FakeUpdater { SourceVersion = "260901.1" };
        var (bootstrapper, _, services) = Build(updater);
        var sessionRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        sessionRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);

        bootstrapper.Call(services, sessionRest);

        Assert.Same(clone, updater.LoadedFrom);
    }

    [Fact]
    public void UpdateRequired_CopyFails_ExplainsAndStillExits()
    {
        // Bez copy/launch by se shell točil v detekci - exit platí, uživatel dostane hlášku.
        var updater = new FakeUpdater { SourceVersion = "260901.1", CopyThrows = true };
        var (bootstrapper, popup, services) = Build(updater);

        var (shouldExit, _) = bootstrapper.Call(services);

        Assert.True(shouldExit);
        popup.Received(1).Info(Arg.Is<string>(s => s.Contains("Aktualizaci nelze spustit")));
        Assert.Equal(["copy"], updater.Calls); // launch po selhané kopii neběží
    }

    [Fact]
    public void ForcedWave_LaunchesForcedWithStamp()
    {
        var updater = new FakeUpdater
        {
            Settings = new UpdateSettings(true, "", @"\\server\Edison", "", ForcedStamp: "66234,2"),
            StampSeen = "66230,1", // lokální stopa nesedí s razítkem z Caché
        };
        var (bootstrapper, popup, services) = Build(updater);

        var (shouldExit, _) = bootstrapper.Call(services);

        Assert.True(shouldExit);
        Assert.Equal(["copy", "launch:260825.1|forced=True|stamp=66234,2"], updater.Calls);
    }

    [Fact]
    public void Broken_InformsAndExitsWithoutLaunching()
    {
        var updater = new FakeUpdater { SourceVersion = "" }; // zdrojová verze nečitelná
        var (bootstrapper, popup, services) = Build(updater);

        var (shouldExit, notice) = bootstrapper.Call(services);

        Assert.True(shouldExit);
        Assert.Null(notice);
        popup.Received(1).Info(Arg.Is<string>(s => s.Contains("serverovou verzi")));
        Assert.Empty(updater.Calls);
    }

    [Fact]
    public async Task NoUpdate_StartsNoticeOnlyCheckAndContinues()
    {
        var updater = new FakeUpdater(); // verze shodné → None
        var (bootstrapper, popup, services) = Build(updater);

        var (shouldExit, notice) = bootstrapper.Call(services);

        Assert.False(shouldExit);
        Assert.NotNull(notice);
        // DEBUG lokální verze je dev → notice bez sítě vrátí Unknown; podstatné je, že běží.
        Assert.Equal(ReleaseNoticeOutcome.Unknown, (await notice).Outcome);
        popup.DidNotReceiveWithAnyArgs().Info(default!);
        Assert.Empty(updater.Calls);
    }
}
