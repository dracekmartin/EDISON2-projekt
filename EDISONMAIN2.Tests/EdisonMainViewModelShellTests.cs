using System.Text.Json.Nodes;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Shell chování VM mimo strom samotný: Ctrl+W dvojhmat, titulek, toolbar gating,
/// výchozí okno po loginu, spuštění zástupce přes hosta a hromadné rozbalení.
/// </summary>
public class EdisonMainViewModelShellTests
{
    private static ExecutableModule Exe(string id, string exe = "a.exe", string form = "ALVA.A.frm") =>
        new() { Name = "A", ExecutablePath = exe, FormName = form, ElementId = id };

    private static ModuleFamily Fam(string name, params IModule[] kids)
    {
        var f = new ModuleFamily { Name = name };
        foreach (var k in kids) f.NestedModules.Add(k);
        return f;
    }

    [Fact]
    public void HandleCtrlW_SecondPress_OpensUtilitiesAndResets()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        vm.HandleCtrlW();
        Assert.True(vm.IsWaitingForSecondKey);
        Assert.NotEqual("", vm.SecondKeyHintText);
        nav.DidNotReceive().ShowLegacy(Arg.Any<string>(), Arg.Any<string>());

        vm.HandleCtrlW();
        Assert.False(vm.IsWaitingForSecondKey);
        Assert.Equal("", vm.SecondKeyHintText);
        nav.Received(1).ShowLegacy("UTILITIES.exe", "ALVA.UTILITIES.formUTILITIESMain");
    }

    [Fact]
    public void CancelSecondKeyWait_AbortsChord()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        vm.HandleCtrlW();
        vm.CancelSecondKeyWait();  // jiná klávesa mezi stisky
        vm.HandleCtrlW();          // = zase první stisk, ne druhý

        Assert.True(vm.IsWaitingForSecondKey);
        nav.DidNotReceive().ShowLegacy(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void WindowTitle_CarriesIdfa()
    {
        var vm = CreateVM();

        Assert.Equal("M-line EDISON [CB_DP]", vm.WindowTitle);
    }

    [Fact]
    public void HasSelectionAndCanSetStartup_FollowSelectedModule()
    {
        var vm = CreateVM(treeLoader: TreeLoaderWith([Fam("Rodina", Exe("23"))]));

        Assert.False(vm.HasSelection);
        Assert.False(vm.CanSetStartup);

        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        vm.SelectedModule = family;
        Assert.True(vm.HasSelection);
        Assert.False(vm.CanSetStartup);   // rodina není spustitelná

        vm.SelectedModule = family.NestedModules.OfType<ExecutableModule>().Single();
        Assert.True(vm.CanSetStartup);

        vm.SelectedModule = new ExecutableModule { Name = "bez id", ExecutablePath = "x.exe" };
        Assert.False(vm.CanSetStartup);   // bez ElementId nejde uložit
    }

    [Fact]
    public void OpenStartupWindowIfAny_NoDefault_OpensNothing()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav), treeLoader: TreeLoaderWith([Exe("23")]));

        vm.OpenStartupWindowIfAny();

        nav.DidNotReceive().ShowLegacyAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public void OpenStartupWindowIfAny_OpensDefaultModule_OnlyOncePerRun()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav), treeLoader: TreeLoaderWith([Exe("23")]));
        vm.SetStartupWindowCommand.Execute(vm.MainMenuTree.OfType<ExecutableModule>().Single());

        vm.OpenStartupWindowIfAny();
        vm.OpenStartupWindowIfAny(); // idempotence - druhé vykreslení shellu nesmí otevřít znovu

        nav.Received(1).ShowLegacyAsync("a.exe", "ALVA.A.frm", inputParam: "");
    }

    [Fact]
    public void OpenStartupWindowIfAny_TestRun_OpensNothing()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav), treeLoader: TreeLoaderWith([Exe("23")]));
        vm.SetStartupWindowCommand.Execute(vm.MainMenuTree.OfType<ExecutableModule>().Single());
        vm.OpensStartupWindow = false;

        vm.OpenStartupWindowIfAny();

        nav.DidNotReceive().ShowLegacyAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public void LaunchShortcutCommand_ShortcutWithoutForm_ExplainsInsteadOfSilence()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var popup = Substitute.For<IPopupDisplayer>();
        var vm = CreateVM(services: MockServices(nav: nav, popup: popup));

        vm.LaunchShortcutCommand.Execute(new DesktopShortcut { Name = "Sestava" });

        popup.Received(1).Info(Arg.Is<string>(s => s.Contains("nelze ho spustit")));
        nav.DidNotReceive().ShowLegacyShortcutAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public void LaunchShortcutCommand_PassesPayloadAndInputToHost()
    {
        var nav = Substitute.For<IWindowNavigator>();
        var vm = CreateVM(services: MockServices(nav: nav));

        vm.LaunchShortcutCommand.Execute(new DesktopShortcut
        {
            Kind = ShortcutKind.PrintReport,
            Id = "42", ElementId = "660", Name = "Sestava",
            ExecutablePath = "r.exe", FormName = "ALVA.R.frm", InputParam = "vstup",
        });

        nav.Received(1).ShowLegacyShortcutAsync("r.exe", "ALVA.R.frm",
            Arg.Is<string>(p => p.Length > 0), inputParam: "vstup");
    }

    [Fact]
    public void ExpandAllCommand_ExpandsActiveTree_SavesOnce()
    {
        var userPrefs = Substitute.For<IUserPrefsService>();
        var vm = CreateVM(treeLoader: TreeLoaderWith([Fam("Rodina", Exe("23"))]), userPrefs: userPrefs);
        vm.SelectedTab = vm.TreeTabs.First(t => t.Expandable);
        var family = vm.MainMenuTree.OfType<ModuleFamily>().Single();
        family.IsExpanded = false;
        userPrefs.ClearReceivedCalls();

        vm.ExpandAllCommand.Execute(null);

        Assert.True(family.IsExpanded);
        userPrefs.Received(1).SaveAsync("EDISONMAIN2", "TreeExpansion", Arg.Any<JsonNode>());

        userPrefs.ClearReceivedCalls();
        vm.CollapseAllCommand.Execute(null);

        Assert.False(family.IsExpanded);
        userPrefs.Received(1).SaveAsync("EDISONMAIN2", "TreeExpansion", Arg.Any<JsonNode>());
    }

    [Fact]
    public void ActionOverlayVariants_DefaultFirst_LabelsUnique()
    {
        var vm = CreateVM();

        // Výchozí varianta = null klíč (dopravní prostředky); přidání sady "Vehicles" do mapy
        // AgendaSprites by popisek zdvojilo - hardcoded default v VM a SetLabels se musí sladit.
        Assert.Null(vm.ActionOverlayVariants[0].SpriteKey);
        var labels = vm.ActionOverlayVariants.Select(v => v.Label).ToList();
        Assert.Equal(labels.Distinct(), labels);
    }

    [Fact]
    public async Task ShowShellChangelogIfAnyAsync_WithoutSource_Completes()
    {
        var vm = CreateVM(); // changelogSource null → prompt neexistuje, nesmí spadnout

        await vm.ShowShellChangelogIfAnyAsync();
    }
}
