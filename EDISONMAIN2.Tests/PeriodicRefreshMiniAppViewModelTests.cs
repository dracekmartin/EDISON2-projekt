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
    public void Title_MatchesCatalogDescriptor()
    {
        // Název žije na dvou místech (descriptor v katalogu, Title VM ve výpisu „Poslední obnova")
        // a rozejít se smí jen omylem.
        var descriptor = new CoreMiniApps().GetMiniApps().Single(a => a.Id == "Core.AutoRefresh");

        Assert.Equal(descriptor.Title, Build().Title);
    }

    [Fact]
    public void RefreshNow_WithoutRefresher_DoesNotThrow()
    {
        var vm = Build();

        vm.RefreshNowCommand.Execute(null);

        Assert.Contains("nic k obnovení", vm.LastRefresh);
    }

    [Fact]
    public void OnOpened_StartsTimerWithDefaultFiveMinutes()
    {
        var vm = Build();

        vm.OnOpened();

        Assert.True(vm.TimerRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), vm.TimerInterval);
    }

    [Fact]
    public void IsEnabled_TogglesTimer()
    {
        var vm = Build();
        vm.OnOpened();

        vm.IsEnabled = false;
        Assert.False(vm.TimerRunning);

        vm.IsEnabled = true;
        Assert.True(vm.TimerRunning);
    }

    [Fact]
    public void SelectedInterval_Change_ReschedulesWithNewInterval()
    {
        var vm = Build();
        vm.OnOpened();

        vm.SelectedInterval = vm.Intervals.First(i => i.Duration == TimeSpan.FromSeconds(30));

        Assert.True(vm.TimerRunning);
        Assert.Equal(TimeSpan.FromSeconds(30), vm.TimerInterval);
    }

    [Fact]
    public void OnClosed_StopsTimer()
    {
        var vm = Build();
        vm.OnOpened();

        vm.OnClosed();

        Assert.False(vm.TimerRunning);
    }
}
