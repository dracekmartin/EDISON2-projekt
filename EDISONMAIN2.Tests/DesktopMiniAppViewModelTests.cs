using EDControls.EDViewModel;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy miniaplikace Plocha - VM je tenká vrstva nad <see cref="ModuleShortcutsService"/>
/// (remove, novinky, empty-state). Sdílení se stromem pokrývá <c>ModuleShortcutsServiceTests</c>.
/// </summary>
public class DesktopMiniAppViewModelTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { Name = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static DesktopShortcutDto Menu(string elementId) =>
        new(nameof(ShortcutKind.MenuModule), elementId, elementId, "M");

    private static DesktopMiniAppViewModel Vm(ModuleShortcutsService svc) => new(MockServices(), svc);

    [Fact]
    public void Items_ExposesServiceCollection()
    {
        var svc = new ModuleShortcutsService();
        svc.Seed([Exe("100")], [Menu("100")]);
        var vm = Vm(svc);

        Assert.Same(svc.Items, vm.Items);
        Assert.True(vm.HasItems);
    }

    [Fact]
    public void HasItems_False_WhenEmpty_AndRaisesOnChange()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], []);
        var vm = Vm(svc);
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasItems)) raised = true; };

        Assert.False(vm.HasItems);
        svc.Add(a);

        Assert.True(vm.HasItems);
        Assert.True(raised);
    }

    [Fact]
    public void RemoveCommand_RemovesFromService()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], [Menu("100")]);
        var vm = Vm(svc);

        vm.RemoveCommand.Execute(svc.Items[0]);

        Assert.Empty(svc.Items);
        Assert.False(a.IsFavorite);
    }

    [Fact]
    public void ShowChangelogCommand_RequestsChangelogThroughService()
    {
        var svc = new ModuleShortcutsService();
        svc.Seed([Exe("100")], [Menu("100")]);
        var vm = Vm(svc);
        DesktopShortcut? requested = null;
        svc.ChangelogRequested += s => requested = s;

        vm.ShowChangelogCommand.Execute(svc.Items[0]);

        Assert.Same(svc.Items[0], requested);
    }

    // Dock vykreslí Plochu jen přes DataTemplate ze slovníku mergnutého v App.xaml (pack URI build
    // neověří); bez něj panel ukáže název typu VM.
    [Fact]
    public void TemplatesDictionary_HasTemplateForViewModel()
    {
        _ = System.Windows.Application.ResourceAssembly;   // bez WPF Application se pack:// jinak nezaregistruje
        EDTesting.Sta.Run(() =>
        {
            var templates = new System.Windows.ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/EDISONMAIN2;component/Desktop/DesktopMiniAppTemplates.xaml"),
            };

            Assert.IsType<System.Windows.DataTemplate>(
                templates[new System.Windows.DataTemplateKey(typeof(DesktopMiniAppViewModel))]);
        });
    }

    [Fact]
    public void AppXaml_MergesTemplatesDictionary()
    {
        var app = System.IO.File.ReadAllText(LegacyTreeSources.RepoPath(System.IO.Path.Combine("EDISONMAIN2", "dotnet", "App.xaml")));

        Assert.Contains("component/Desktop/DesktopMiniAppTemplates.xaml", app);
    }

    [Fact]
    public void OnClosed_UnsubscribesFromCollection()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], []);
        var vm = Vm(svc);
        int raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasItems)) raised++; };

        vm.OnClosed();
        svc.Add(a);   // po OnClosed už VM nereaguje

        Assert.Equal(0, raised);
    }
}
