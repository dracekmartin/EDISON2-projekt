using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy řídicí mini-app pro periodický refresh. Časovač (DispatcherTimer) se netestuje;
/// podstatná je delegace refreshe na <c>RefreshAll</c> předaný managerem.
/// </summary>
public class PeriodicRefreshMiniAppViewModelTests
{
    private static PeriodicRefreshMiniAppViewModel Build() =>
        new(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void RefreshNow_InvokesAttachedRefresher_AndListsApps()
    {
        var vm = Build();
        vm.AttachRefresher(() => ["Dispečink", "Aktuální období"]);

        vm.RefreshNowCommand.Execute(null);

        Assert.Contains("Dispečink", vm.LastRefresh);
        Assert.Contains("Aktuální období", vm.LastRefresh);
    }

    [Fact]
    public void RefreshNow_WithoutRefresher_DoesNotThrow()
    {
        var vm = Build();

        vm.RefreshNowCommand.Execute(null);

        Assert.Contains("nic k obnovení", vm.LastRefresh);
    }
}
