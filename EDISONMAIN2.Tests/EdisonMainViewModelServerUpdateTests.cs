using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Nástroje → Zkontrolovat aktualizace: položky vidí jen ten, kdo by se dostal k legacy tlačítku
/// v Správa systému > EDISON > EDISON (okno ve stromu + správce nebo právo „Aktualizovat EDISON"),
/// TEST verzi navíc jen stanice M-line.
/// </summary>
public class EdisonMainViewModelServerUpdateTests
{
    private sealed class StubManualUpdate(bool closeShell) : ManualUpdate(MockServices(), new ShellShutdown(() => { }))
    {
        public List<bool> Runs { get; } = [];

        public override Task<bool> RunAsync(bool testVersion, Func<IDisposable> progress)
        {
            Runs.Add(testVersion);
            return Task.FromResult(closeShell);
        }
    }

    private static IModuleTreeLoader TreeWithSettingsWindow(bool present)
    {
        var mock = Substitute.For<IModuleTreeLoader>();
        mock.GetModuleTree(Arg.Any<IMLRest>(), Arg.Any<string>()).Returns([]);
        mock.GetModuleTree(Arg.Any<IMLRest>(), "main").Returns(
        [
            new ExecutableModule { Name = "Uživatelé", ElementId = "5001", ExecutablePath = "USERS.exe" },
            .. present
                ? new[]
                {
                    new ExecutableModule
                    {
                        Name = "EDISON", ElementId = AccessLoader.FormEdisonmainSettingsShow, ExecutablePath = "EDISONMAIN.exe",
                        FormName = "ALVA.EDISONMAIN.formEDISONMAINSettingsShow",
                    },
                }
                : [],
        ]);
        return mock;
    }

    private static EdisonMainViewModel CreateUpdateVM(bool isAdmin = true, bool mayUpdate = false, bool settingsInTree = true,
                                                      bool mlineStation = false, Action? forceShutdown = null)
    {
        var services = MockServices();
        services.IsAdmin = isAdmin;
        services.MayUpdate = mayUpdate;
        return CreateVM(services: services, treeLoader: TreeWithSettingsWindow(settingsInTree),
                        station: NeutralStation with { IsMlineDomain = mlineStation }, forceShutdown: forceShutdown);
    }

    [Theory]
    [InlineData(true, false, true, false, true, false)]
    [InlineData(false, true, true, true, true, true)]
    [InlineData(false, false, true, true, false, false)]
    [InlineData(true, true, false, true, false, false)]
    public void UpdateItems_FollowLegacyButtonRights(bool isAdmin, bool mayUpdate, bool settingsInTree, bool mlineStation,
                                                     bool serverVisible, bool testVisible)
    {
        var vm = CreateUpdateVM(isAdmin, mayUpdate, settingsInTree, mlineStation);
        vm.ManualUpdate = new StubManualUpdate(closeShell: false);

        Assert.Equal(serverVisible, vm.IsServerUpdateVisible);
        Assert.Equal(serverVisible, vm.CheckUpdateCommand.CanExecute(null));
        Assert.Equal(testVisible, vm.IsTestUpdateVisible);
        Assert.Equal(testVisible, vm.CheckTestUpdateCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateItems_HiddenWithoutManualUpdate()
    {
        // E2E běh nesmí spustit serverový updater, bootstrapper mu službu nedá.
        var vm = CreateUpdateVM(mlineStation: true);

        Assert.False(vm.IsServerUpdateVisible);
        Assert.False(vm.IsTestUpdateVisible);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckUpdate_ClosesShellOnlyWhenAsked(bool closeShell)
    {
        int shutdowns = 0;
        var vm = CreateUpdateVM(forceShutdown: () => shutdowns++);
        var manual = new StubManualUpdate(closeShell);
        vm.ManualUpdate = manual;

        vm.CheckUpdateCommand.Execute(null);

        Assert.Equal([false], manual.Runs);
        Assert.Equal(closeShell ? 1 : 0, shutdowns);
    }

    [Fact]
    public void CheckTestUpdate_RunsTestVersion()
    {
        var vm = CreateUpdateVM(mlineStation: true);
        var manual = new StubManualUpdate(closeShell: false);
        vm.ManualUpdate = manual;

        vm.CheckTestUpdateCommand.Execute(null);

        Assert.Equal([true], manual.Runs);
    }

    [Fact]
    public async Task LoginAsAnotherUser_Success_NotifiesUpdateVisibility()
    {
        // Práva i strom se mění s uživatelem - bez notifikace by položky zůstaly podle předchozího.
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var services = MockServices(popup: popup);
        var vm = CreateVM(services: services, treeLoader: TreeWithSettingsWindow(present: true), switchUser: () =>
        {
            services.MayUpdate = true;
            return new(SwitchUserResult.Success);
        });
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        await vm.LoginAsAnotherUserAsync();

        Assert.Contains(nameof(EdisonMainViewModel.IsServerUpdateVisible), notified);
        Assert.Contains(nameof(EdisonMainViewModel.IsTestUpdateVisible), notified);
    }
}
