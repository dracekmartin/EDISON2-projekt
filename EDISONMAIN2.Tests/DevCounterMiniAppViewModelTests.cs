using EDControls.EDViewModel;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class DevCounterMiniAppViewModelTests
{
    private static DevCounterMiniAppViewModel NewVm() =>
        new(new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void Increment_IncreasesCount()
    {
        var vm = NewVm();

        vm.IncrementCommand.Execute(null);
        vm.IncrementCommand.Execute(null);

        Assert.Equal(2, vm.Count);
    }

    [Fact]
    public void Reset_ZeroesCount()
    {
        var vm = NewVm();
        vm.IncrementCommand.Execute(null);

        vm.ResetCommand.Execute(null);

        Assert.Equal(0, vm.Count);
    }

    [Fact]
    public void Title_IsCounter() => Assert.Equal("Počítadlo", NewVm().Title);
}
