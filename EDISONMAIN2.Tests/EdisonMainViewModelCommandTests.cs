using System.Windows.Input;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.ConnectionWindow;
using EDISONMAIN2.Windows.EdisonMainWindow;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Delegující commandy menu: každý musí volat správnou metodu navigátoru se správným exe
/// a plným názvem legacy formy - překlep v názvu by jinak prošel až do produkce.
/// </summary>
public class EdisonMainViewModelCommandTests
{
    private static ICommand Cmd(EdisonMainViewModel vm, string name) => name switch
    {
        "BgTasks"          => vm.ShowBgTasksCommand,
        "UsersLocks"       => vm.ShowUsersLocksCommand,
        "UsersActivity"    => vm.ShowUsersActivityCommand,
        "LanguageSettings" => vm.ShowLanguageSettingsCommand,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData("UsersLocks", "USERS.exe", "ALVA.USERS.formUSERSLocks")]
    [InlineData("UsersActivity", "USERS.exe", "ALVA.USERS.formUSERSActivity")]
    public void ModelessLegacyCommands_LaunchCorrectForm(string command, string exe, string form)
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        Cmd(vm, command).Execute(null);

        nav.Received(1).ShowLegacyAsync(exe, form);
    }

    [Theory]
    [InlineData("BgTasks", "EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList")]
    [InlineData("LanguageSettings", "EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLanguageList")]
    public void DialogLegacyCommands_LaunchCorrectForm(string command, string exe, string form)
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        Cmd(vm, command).Execute(null);

        nav.Received(1).ShowLegacyAsDialogAsync(exe, form);
    }

    [Fact]
    public void AdminOnlyCommands_GateOnIsAdmin()
    {
        // Parita s legacy frmNoMDI - Správa zámků a Aktivita uživatelů jen pro admina.
        var services = MockServices();
        var vm = CreateVM(services: services);

        services.IsAdmin = false;
        Assert.False(vm.ShowUsersLocksCommand.CanExecute(null));
        Assert.False(vm.ShowUsersActivityCommand.CanExecute(null));

        services.IsAdmin = true;
        Assert.True(vm.ShowUsersLocksCommand.CanExecute(null));
        Assert.True(vm.ShowUsersActivityCommand.CanExecute(null));
    }

    [Fact]
    public void OpenTemplateDesignerCommand_LaunchesTdWithoutForm()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        vm.OpenTemplateDesignerCommand.Execute(null);

        nav.Received(1).ShowLegacy("TemplateDesigner.exe", formFqn: "");
    }

    [Fact]
    public void ConnectionLaunchInfo_PassesServerAndRestPortAsArguments()
    {
        // Nová instance proti jinému serveru - argumenty musí přečíst ConnectionOverride.Parse při startu.
        var info = EdisonMainViewModel.ConnectionLaunchInfo(@"C:\Edison\EDISONMAIN2.exe",
            new ConnectionChoice("Test", "10.0.0.5", 57770, IsDefault: false));

        Assert.Equal(@"C:\Edison\EDISONMAIN2.exe", info.FileName);
        Assert.True(info.UseShellExecute);
        Assert.Equal(new ConnectionOverride("10.0.0.5", 57770), ConnectionOverride.Parse(info.ArgumentList));
    }

    [Fact]
    public void EdisonCopyStartInfo_CarriesLoaderMarker()
    {
        // Bez značky by App kopii přesměroval přes EDLOADER (při výpadku LoadSettings by naskočil starý shell).
        var info = EdisonMainViewModel.EdisonCopyStartInfo(@"C:\Edison\EDISONMAIN2.exe");

        Assert.Equal(@"C:\Edison\EDISONMAIN2.exe", info.FileName);
        Assert.False(App.ShouldRedirectToLoader(info.ArgumentList, loaderPresent: true, alreadyRedirected: false));
    }

    [Fact]
    public void ExitCommand_ConfirmGatesForceShutdown()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        int shutdowns = 0;
        var vm = CreateVM(services: MockServices(popup: popup), forceShutdown: () => shutdowns++);

        popup.Confirm(Arg.Any<string>()).Returns(false);
        vm.ExitCommand.Execute(null);
        Assert.Equal(0, shutdowns);

        popup.Confirm(Arg.Any<string>()).Returns(true);
        vm.ExitCommand.Execute(null);
        Assert.Equal(1, shutdowns);
    }

    [Fact]
    public void ResetFormsCommand_ConfirmGatesServerReset()
    {
        var popup = Substitute.For<IPopupDisplayer>();
        var ml = MockMLRest();
        var vm = CreateVM(services: MockServices(popup: popup), mlRest: ml);

        popup.Confirm(Arg.Any<string>()).Returns(false);
        vm.ResetFormsCommand.Execute(null);
        ml.DidNotReceive().QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", Arg.Any<CVar>());

        popup.Confirm(Arg.Any<string>()).Returns(true);
        vm.ResetFormsCommand.Execute(null);
        ml.Received(1).QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", Arg.Any<CVar>());
        popup.Received(1).Info(Arg.Is<string>(s => s.Contains("obnoveno")));
    }

    [Fact]
    public void ActivateModuleCommand_ActivatesGivenModule()
    {
        var vm = CreateVM();
        int activated = 0;
        var running = new RunningModuleInfo("Turnusy [CB_DP]", () => activated++);

        vm.ActivateModuleCommand.Execute(running);

        Assert.Equal(1, activated);
    }
}
