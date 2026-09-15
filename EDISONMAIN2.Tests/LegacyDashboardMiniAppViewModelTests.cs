using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy generické legacy dashboardové miniaplikace: otevření panelu čte předpočítaná data
/// (refresh=0, parita s legacy startem), „Načíst" si vynutí přepočet (refresh=1), chyba končí
/// v panelu (ErrorText), ne popupem. MLRest mockován přes IMLRest (object[] QueryOrThrow).
/// </summary>
public class LegacyDashboardMiniAppViewModelTests
{
    private const char D = '\0';
    private const string CacheClass = "ALVA.EDISON.MiniAplikaceAPI";

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

    private sealed record Harness(
        LegacyDashboardMiniAppViewModel Vm, IMLRest MLRest, IPopupDisplayer Popup, ManualFlushScheduler Scheduler);

    // Scheduler default „hned" = dávka odejde uvnitř LoadAsync, takže test běží synchronně.
    private static Harness Build(string appType, string title, ManualFlushScheduler? scheduler = null)
    {
        var mlRest = Substitute.For<IMLRest>();
        mlRest.Delimiter.Returns(D);
        mlRest.QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>()).Returns(Sample(appType));

        var popup = Substitute.For<IPopupDisplayer>();
        var services = new EDServices(mlRest, Substitute.For<IWindowNavigator>(), popup);
        var loader = new LegacyDashboardLoader(new InlineRestCallRunner(mlRest),
                                               scheduler is null ? action => action() : scheduler.Schedule);
        return new Harness(new LegacyDashboardMiniAppViewModel(services, loader, appType, title),
                           mlRest, popup, scheduler!);
    }

    [Fact]
    public void OnOpened_LoadsSectionsAndTimestamp()
    {
        var h = Build("52005", "Aktuální období");

        h.Vm.OnOpened();

        Assert.Equal("Aktuální období", h.Vm.Title);
        Assert.Equal("21.06.2026 09:00", h.Vm.LastUpdated);
        // Label nad tabulkou už není sekce - je z něj titulek pruhu EDListu.
        Assert.Equal("Celkem 3 vozidla", Assert.IsType<DashboardTable>(Assert.Single(h.Vm.Sections)).Title);
        Assert.False(h.Vm.HasNoData);
        Assert.False(h.Vm.IsLoading);
        Assert.Null(h.Vm.ErrorText);
    }

    [Fact]
    public void OnOpened_ReadsPrecomputedData_WithoutServerRecompute()
    {
        var h = Build("52005", "Aktuální období");

        h.Vm.OnOpened();

        h.MLRest.Received().QueryOrThrow(CacheClass, "LoadData",
            Arg.Is<object[]>(a => a.Length == 2
                && (string)a[0] == $"refresh{D}0"
                && (string)a[1] == $"appType{D}52005"));
    }

    [Fact]
    public void RefreshCommand_ForcesServerRecompute()
    {
        var h = Build("52005", "Aktuální období");

        h.Vm.RefreshCommand.Execute(null);

        h.MLRest.Received().QueryOrThrow(CacheClass, "LoadData",
            Arg.Is<object[]>(a => (string)a[0] == $"refresh{D}1"));
    }

    [Fact]
    public void Load_Twice_DoesNotDuplicateSections()
    {
        var h = Build("52005", "Aktuální období");

        h.Vm.OnOpened();
        h.Vm.OnOpened();

        Assert.Single(h.Vm.Sections);
    }

    [Fact]
    public void WhilePending_IsLoadingAndNoEmptyState()
    {
        var scheduler = new ManualFlushScheduler();
        var h = Build("52005", "Aktuální období", scheduler);

        h.Vm.OnOpened();

        Assert.True(h.Vm.IsLoading);
        // Bez tohohle by při startu problikl text „Žádná data k zobrazení".
        Assert.False(h.Vm.HasNoData);
        Assert.Empty(h.Vm.Sections);

        scheduler.Flush();

        Assert.False(h.Vm.IsLoading);
        Assert.Single(h.Vm.Sections);
    }

    [Fact]
    public void FailedLoad_ShowsErrorInPanel_NotPopup()
    {
        var mlRest = Substitute.For<IMLRest>();
        mlRest.Delimiter.Returns(D);
        mlRest.QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>())
            .Returns<object[]>(_ => throw new InvalidOperationException("server je mimo"));
        var popup = Substitute.For<IPopupDisplayer>();
        var services = new EDServices(mlRest, Substitute.For<IWindowNavigator>(), popup);
        var loader = new LegacyDashboardLoader(new InlineRestCallRunner(mlRest), action => action());
        var vm = new LegacyDashboardMiniAppViewModel(services, loader, "52005", "Aktuální období");

        vm.OnOpened();

        Assert.Equal("server je mimo", vm.ErrorText);
        Assert.False(vm.HasNoData);
        Assert.False(vm.IsLoading);
        // Dávka o N panelech by jinak zvedla N popupů přes startující shell.
        popup.DidNotReceive().Error(Arg.Any<Exception>());
    }
}
