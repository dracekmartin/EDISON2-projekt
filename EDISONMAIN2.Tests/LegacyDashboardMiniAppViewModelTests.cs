using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy generické legacy dashboardové miniaplikace: OnOpened zavolá legacy LoadData (object[])
/// a naplní Sections + LastUpdated. MLRest mockován přes IMLRest (object[] QueryOrThrow).
/// </summary>
public class LegacyDashboardMiniAppViewModelTests
{
    private const char D = '\0';

    private static object[] Sample(string appType) =>
    [
        "",
        $"appType{D}{appType}",
        $"param{D}1{D}21.06.2026 09:00",
        $"data{D}1{D}C1{D}1{D}Celkem 3 vozidla",
        $"data{D}2{D}C2{D}2{D}text:text",
        $"data{D}2{D}C2{D}3{D}Linka~Řidič",
        $"data{D}2{D}C2{D}4{D}1~Novák",
        $"data{D}2{D}C2{D}4{D}2~Svoboda",
    ];

    private static (LegacyDashboardMiniAppViewModel vm, IMLRest mlRest) Build(string appType, string title)
    {
        var mlRest = Substitute.For<IMLRest>();
        mlRest.Delimiter.Returns(D);
        mlRest.QueryOrThrow("ALVA.EDISON.MiniAplikaceAPI", "LoadData", Arg.Any<object[]>())
            .Returns(Sample(appType));

        var services = new EDServices(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        return (new LegacyDashboardMiniAppViewModel(services, appType, title), mlRest);
    }

    [Fact]
    public void OnOpened_LoadsSectionsAndTimestamp()
    {
        var (vm, _) = Build("52005", "Aktuální období");

        vm.OnOpened();

        Assert.Equal("Aktuální období", vm.Title);
        Assert.Equal("21.06.2026 09:00", vm.LastUpdated);
        Assert.Equal(2, vm.Sections.Count);
        Assert.IsType<DashboardLabel>(vm.Sections[0]);
        Assert.IsType<DashboardTable>(vm.Sections[1]);
        Assert.False(vm.HasNoData);
    }

    [Fact]
    public void OnOpened_PassesRefreshAndAppType()
    {
        var (vm, mlRest) = Build("52005", "Aktuální období");

        vm.OnOpened();

        mlRest.Received().QueryOrThrow("ALVA.EDISON.MiniAplikaceAPI", "LoadData",
            Arg.Is<object[]>(a => a.Length == 2
                && (string)a[0] == $"refresh{D}1"
                && (string)a[1] == $"appType{D}52005"));
    }

    [Fact]
    public void Load_Twice_DoesNotDuplicateSections()
    {
        var (vm, _) = Build("52005", "Aktuální období");

        vm.OnOpened();
        vm.OnOpened();

        Assert.Equal(2, vm.Sections.Count);
    }
}
