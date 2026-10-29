using System.IO;
using System.Threading;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Nástroje → Zkontrolovat aktualizace: serverová instalace spouští legacy runner mimo job a shell zavře jen na jeho
/// signál, stanice projde touž kontrolou jako po přihlášení. Updater bez disku a sítě, navigator je mock.
/// </summary>
public class ManualUpdateTests
{
    private sealed class FakeUpdater : Updater
    {
        public UpdateSettings Settings { get; set; } = new("260825.1", @"\\server\Edison", "");
        public string LocalVersion { get; set; } = "260825.1";
        public string SourceVersion { get; set; } = "260825.1";
        public IMLRest? LoadedFrom { get; private set; }
        public readonly List<string> Calls = [];

        public override UpdateSettings LoadSettings(IMLRest mlRest)
        {
            LoadedFrom = mlRest;
            return Settings;
        }
        protected override string GetLocalVersion() => LocalVersion;
        protected override string GetSourceVersion(string sourceDir) => SourceVersion;
        protected override string GetForcedStampSeen() => "";
        public override void CopyUpdater(string sourceDir) => Calls.Add("copy");
        public override void LaunchUpdate(string sourceDir, string version, bool forced, string forcedStamp) =>
            Calls.Add($"launch:{version}");
    }

    private sealed class TestableManualUpdate(EDServices services, ShellShutdown shutdown, Updater updater)
        : ManualUpdate(services, shutdown)
    {
        protected override Updater CreateUpdater() => updater;
    }

    private sealed class Fixture
    {
        public FakeUpdater Updater { get; } = new();
        public IMLRest Rest { get; } = MLRestMocks.Create();
        public IWindowNavigator Navigator { get; } = Substitute.For<IWindowNavigator>();
        public IPopupDisplayer Popup { get; } = Substitute.For<IPopupDisplayer>();
        public ShellShutdown Shutdown { get; } = new(() => { });
        public int OpenOverlays { get; private set; }
        public string? RunnerInput { get; private set; }
        public ManualUpdate Sut { get; }

        public Fixture()
        {
            Sut = new TestableManualUpdate(new EDServices(Rest, Navigator, Popup), Shutdown, Updater);
        }

        public Task<bool> RunAsync(bool testVersion = false) => Sut.RunAsync(testVersion, () =>
        {
            OpenOverlays++;
            return new Overlay(() => OpenOverlays--);
        });

        /// <summary>Runner se spustí; <paramref name="signalClose"/> = nastaví událost jako formEDISONMAINServerUpdate.</summary>
        public TaskCompletionSource RunnerStarts(bool signalClose)
        {
            Updater.Settings = Updater.Settings with { ServerInstallation = true };
            var exited = new TaskCompletionSource();
            Navigator.StartLegacyDetachedDialog(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(ci =>
                {
                    RunnerInput = ci.ArgAt<string>(2);
                    if (signalClose)
                    {
                        using var close = EventWaitHandle.OpenExisting(InputValue(RunnerInput, "CloseEvent"));
                        close.Set();
                    }
                    return new LegacyDialogRun(Task.CompletedTask, exited.Task);
                });
            return exited;
        }
    }

    private sealed class Overlay(Action dispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            dispose();
        }
    }

    private static string InputValue(string input, string key) =>
        input.Split('|').Select(p => p.Split('=', 2)).Single(p => p[0] == key)[1];

    [Fact]
    public async Task Server_RunnerSignalsClose_ClosesShellWithoutLocks()
    {
        var f = new Fixture();
        f.RunnerStarts(signalClose: true);

        Assert.True(await f.RunAsync());

        f.Navigator.Received(1).StartLegacyDetachedDialog(ManualUpdate.RunnerExe, ManualUpdate.RunnerForm, Arg.Any<string>());
        // Session odhlásil runner, zavírací requesty nesmí narazit na chybějící zámek.
        Assert.Equal(0, f.Rest.CacheLocks);
        Assert.True(f.Shutdown.SessionEndExpected);
        Assert.Equal(0, f.OpenOverlays);
    }

    [Fact]
    public async Task Server_RunnerEndsWithoutSignal_ShellKeepsRunning()
    {
        // Nic nového, vypnuté aktualizace, XML patch bez restartu: runner skončí a shell běží dál.
        var f = new Fixture();
        var exited = f.RunnerStarts(signalClose: false);

        var run = f.RunAsync();
        Assert.False(run.IsCompleted);
        exited.SetResult();

        Assert.False(await run);
        Assert.False(f.Shutdown.SessionEndExpected);
        Assert.Equal(0, f.OpenOverlays);
    }

    [Theory]
    [InlineData(false, "0")]
    [InlineData(true, "1")]
    public async Task Server_RunnerInputCarriesTestFlagShellPidAndEvent(bool testVersion, string test)
    {
        var f = new Fixture();
        f.RunnerStarts(signalClose: true);

        await f.RunAsync(testVersion);

        Assert.Equal(test, InputValue(f.RunnerInput!, "Test"));
        Assert.Equal(Environment.ProcessId.ToString(), InputValue(f.RunnerInput!, "ShellPid"));
        Assert.StartsWith("EDISONMAIN2.ServerUpdate.", InputValue(f.RunnerInput!, "CloseEvent"));
    }

    [Fact]
    public async Task Server_RunnerNotStarted_ShellKeepsRunning()
    {
        var f = new Fixture();
        f.Updater.Settings = f.Updater.Settings with { ServerInstallation = true };
        f.Navigator.StartLegacyDetachedDialog(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((LegacyDialogRun?)null);

        Assert.False(await f.RunAsync());
        Assert.False(f.Shutdown.SessionEndExpected);
        Assert.Equal(0, f.OpenOverlays);
    }

    [Fact]
    public async Task LoadsSettingsOnNoLocksClone()
    {
        // Po FULL vydání by version gate request se zámkem odmítl dřív, než se kontrola spustí.
        var f = new Fixture();
        var clone = MLRestMocks.Create();
        f.Rest.With(cacheLocks: 0).Returns(clone);

        await f.RunAsync();

        Assert.Same(clone, f.Updater.LoadedFrom);
    }

    [LegacyRepoFact]
    public void LegacyRunner_IsBuiltAndReadsTheSameInputKeys()
    {
        // Runner žije v repu EDISON: přejmenování formy nebo klíče by z Nástrojů tiše nic nespustilo.
        var dir = Path.Combine(LegacyTreeSources.Root!, "EDISONMAIN");
        var source = File.ReadAllText(Path.Combine(dir, "formEDISONMAINServerUpdate.vb"));

        Assert.Equal("ALVA.EDISONMAIN.formEDISONMAINServerUpdate", ManualUpdate.RunnerForm);
        Assert.Contains("Public Class formEDISONMAINServerUpdate", source);
        Assert.Contains("<RootNamespace>ALVA.EDISONMAIN</RootNamespace>", File.ReadAllText(Path.Combine(dir, "EDISONMAIN.vbproj")));
        Assert.Contains("<Compile Include=\"formEDISONMAINServerUpdate.vb\">", File.ReadAllText(Path.Combine(dir, "EDISONMAIN.vbproj")));
        foreach (var key in new[] { "Test", "ShellPid", "CloseEvent" })
            Assert.Contains($"InputValue(\"{key}\")", source);
    }

    [Fact]
    public async Task Station_UpToDate_InformsAndKeepsRunning()
    {
        var f = new Fixture();

        Assert.False(await f.RunAsync());

        f.Popup.Received(1).Info(ManualUpdate.NoNewVersionMessage);
        f.Navigator.DidNotReceiveWithAnyArgs().StartLegacyDetachedDialog(default!, default!, default!);
        Assert.Empty(f.Updater.Calls);
    }

    [Fact]
    public async Task Station_NewVersion_LaunchesStationUpdaterAndCloses()
    {
        var f = new Fixture();
        f.Updater.SourceVersion = "260901.1";

        Assert.True(await f.RunAsync());

        Assert.Equal(["copy", "launch:260901.1"], f.Updater.Calls);
        f.Popup.DidNotReceive().Info(ManualUpdate.NoNewVersionMessage);
    }

    [Fact]
    public async Task Station_Broken_InformsAndCloses()
    {
        var f = new Fixture();
        f.Updater.LocalVersion = "";

        Assert.True(await f.RunAsync());

        f.Popup.Received(1).Info(Arg.Is<string>(m => m.Contains("VERZE.INF")));
        Assert.Empty(f.Updater.Calls);
    }
}
