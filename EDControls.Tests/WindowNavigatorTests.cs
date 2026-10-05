using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>Fasáda nad <see cref="LegacyModuleHost"/> (launch a lifecycle testuje <see cref="LegacyModuleHostTests"/>);
/// vlastnictví oken (IsInternalWindow / ShouldClose) → WindowOwnershipTests.</summary>
public class WindowNavigatorTests
{
    private static (WindowNavigator Nav, List<LegacyModuleHostTests.FakeProcess> Started) WithFakeHost()
    {
        var started = new List<LegacyModuleHostTests.FakeProcess>();
        var mlRest = Substitute.For<IMLRest>();
        var services = new EDServices(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        var host = new LegacyModuleHost(() => services,
            start: _ =>
            {
                var p = new LegacyModuleHostTests.FakeProcess();
                started.Add(p);
                return p;
            },
            fileExists: _ => true, reportExit: (_, _, _) => { }, trackUsage: (_, _) => { },
            postToUi: a => a(), dialogOwner: () => null, focus: _ => { });
        return (new WindowNavigator(host), started);
    }

    [Fact]
    public void ShowLegacy_DelegatesToHost_CloseLegacyModulesKillsIt()
    {
        var (nav, started) = WithFakeHost();
        var exited = 0;
        nav.LegacyModuleExited += () => exited++;

        nav.ShowLegacy("USERS.exe", "ALVA.USERS.formUSERSLocks");
        nav.CloseLegacyModules();

        Assert.Equal(1, Assert.Single(started).KillCount);
        Assert.Equal(1, exited);
    }

    [Fact]
    public void WpfOnly_LegacyLaunch_IsNotSupported()
    {
        var nav = WindowNavigator.WpfOnly();

        Assert.Throws<NotSupportedException>(() => nav.ShowLegacy("USERS.exe", "ALVA.USERS.formUSERSLocks"));
        Assert.Throws<NotSupportedException>(() => nav.ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSLocks"));
        Assert.Throws<NotSupportedException>(() => nav.ShowLegacyAsDialog("USERS.exe", "ALVA.USERS.formUSERSLocks"));
    }

    [Fact]
    public void WpfOnly_AsyncLegacyLaunch_FaultsTask()
    {
        var nav = WindowNavigator.WpfOnly();

        Assert.IsType<NotSupportedException>(nav.ShowLegacyAsync("USERS.exe", "F").Exception!.GetBaseException());
        Assert.IsType<NotSupportedException>(nav.ShowLegacyAsDialogAsync("USERS.exe", "F").Exception!.GetBaseException());
        Assert.IsType<NotSupportedException>(nav.ShowLegacyShortcutAsync("USERS.exe", "F", "X").Exception!.GetBaseException());
    }

    [Fact]
    public void WpfOnly_CloseLegacyModulesAndEvent_AreNoOps()
    {
        var nav = (WindowNavigator)WindowNavigator.WpfOnly();

        nav.LegacyModuleExited += () => { };
        nav.CloseLegacyModules();
    }

    [Fact]
    public void ResolveExecutablePath_FileExistsLocally_ReturnsLocalPath()
    {
        // Test dll je v AppContext.BaseDirectory - File.Exists hit, vrátí local path.
        var existing = Path.GetFileName(typeof(WindowNavigator).Assembly.Location);

        var result = WindowNavigator.ResolveExecutablePath(existing);

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, existing), result);
    }

    [Fact]
    public void ResolveExecutablePath_FileNotExists_FallsBackToEdisonDeployRoot()
    {
        var nonExistent = $"{Guid.NewGuid()}.exe";

        var result = WindowNavigator.ResolveExecutablePath(nonExistent);

        Assert.Equal(Path.Combine(WindowNavigator.EdisonDeployRoot, nonExistent), result);
    }
}
