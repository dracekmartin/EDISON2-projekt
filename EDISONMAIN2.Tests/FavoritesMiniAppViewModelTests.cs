using EDControls.EDViewModel;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy miniaplikace Oblíbené — VM je tenká vrstva nad <see cref="ModuleShortcutsService"/>
/// (launch conduit, remove, empty-state). Sdílení se stromem pokrývá <c>ModuleShortcutsServiceTests</c>.
/// </summary>
public class FavoritesMiniAppViewModelTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static ExecutableModule Exe(string id, string name = "M") =>
        new() { DisplayedName = name, ElementId = id, ExecutablePath = id + ".exe" };

    private static FavoritesMiniAppViewModel Vm(ModuleShortcutsService svc) => new(MockServices(), svc);

    [Fact]
    public void Favorites_ExposesServiceCollection()
    {
        var svc = new ModuleShortcutsService();
        svc.Seed([Exe("100")], ["100"]);
        var vm = Vm(svc);

        Assert.Same(svc.Favorites, vm.Favorites);
        Assert.True(vm.HasFavorites);
    }

    [Fact]
    public void HasFavorites_False_WhenEmpty_AndRaisesOnChange()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], []);
        var vm = Vm(svc);
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasFavorites)) raised = true; };

        Assert.False(vm.HasFavorites);
        svc.Add(a);

        Assert.True(vm.HasFavorites);
        Assert.True(raised);
    }

    [Fact]
    public void LaunchCommand_RequestsLaunchThroughService()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], ["100"]);
        var vm = Vm(svc);
        ExecutableModule? launched = null;
        svc.LaunchRequested += m => launched = m;

        vm.LaunchCommand.Execute(a);

        Assert.Same(a, launched);
    }

    [Fact]
    public void RemoveCommand_RemovesFromService()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], ["100"]);
        var vm = Vm(svc);

        vm.RemoveCommand.Execute(a);

        Assert.Empty(svc.Favorites);
        Assert.False(a.IsFavorite);
    }

    [Fact]
    public void OnClosed_UnsubscribesFromCollection()
    {
        var svc = new ModuleShortcutsService();
        var a = Exe("100");
        svc.Seed([a], []);
        var vm = Vm(svc);
        int raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasFavorites)) raised++; };

        vm.OnClosed();
        svc.Add(a);   // po OnClosed už VM nereaguje

        Assert.Equal(0, raised);
    }
}
