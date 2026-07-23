using System.Windows;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.MiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy karuselu Podpora M-line. Časovač (DispatcherTimer) se netestuje — logika kroku je
/// v interním <c>Tick()</c>, který testy volají přímo (vzor PeriodicRefresh).
/// </summary>
public class SupportCarouselMiniAppViewModelTests
{
    private static EDServices MockServices() =>
        new(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static CarouselPage ThrowingPage(string title) =>
        new(title, () => throw new InvalidOperationException("factory se nemá volat"));

    // Null obsah — čtení VisiblePages nesmí vyžadovat STA (FrameworkElement se nevytváří).
    private static CarouselPage Page(string title) => new(title, () => null!);

    private static SupportCarouselMiniAppViewModel Build() =>
        new(MockServices(), [Page("Novinky"), Page("Wiki"), Page("Školení")]);

    private static string[] VisibleTitles(SupportCarouselMiniAppViewModel vm) =>
        [.. vm.VisiblePages.Select(p => p.Title)];

    [Fact]
    public void Ctor_EmptyPages_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SupportCarouselMiniAppViewModel(MockServices(), []));
    }

    [Fact]
    public void Ctor_DoesNotInvokeContentFactories()
    {
        // Kontrakt jako SimpleMiniApp: obsah se materializuje až přístupem na VisiblePages.
        var ex = Record.Exception(() => new SupportCarouselMiniAppViewModel(
            MockServices(), [ThrowingPage("Novinky"), ThrowingPage("Wiki")]));

        Assert.Null(ex);
    }

    [Fact]
    public void Next_CyclesForward_AndWraps()
    {
        var vm = Build();

        vm.NextCommand.Execute(null);
        Assert.Equal(["Wiki"], VisibleTitles(vm));
        Assert.Equal("2/3", vm.PageIndicator);

        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        Assert.Equal(["Novinky"], VisibleTitles(vm));   // wrap na začátek
        Assert.Equal("1/3", vm.PageIndicator);
    }

    [Fact]
    public void Prev_FromFirstPage_WrapsToLast()
    {
        var vm = Build();

        vm.PrevCommand.Execute(null);

        Assert.Equal(["Školení"], VisibleTitles(vm));
        Assert.Equal("3/3", vm.PageIndicator);
    }

    [Fact]
    public void Tick_AccumulatesProgress()
    {
        var vm = Build();

        vm.Tick();
        vm.Tick();

        Assert.Equal(2d / 60d, vm.Progress, precision: 10);
        Assert.Equal(0, vm.CurrentIndex);
    }

    [Fact]
    public void Tick_FullDuration_AdvancesPage_AndResetsProgress()
    {
        var vm = Build();

        for (int i = 0; i < 60; i++) vm.Tick();

        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(0, vm.Progress);
    }

    [Fact]
    public void ManualMove_ResetsProgress()
    {
        var vm = Build();
        vm.Tick();
        Assert.True(vm.Progress > 0);

        vm.NextCommand.Execute(null);

        Assert.Equal(0, vm.Progress);
    }

    [Fact]
    public void UpdateViewportWidth_ScalesVisibleCount()
    {
        var vm = Build();

        vm.UpdateViewportWidth(SupportCarouselMiniAppViewModel.MinPageWidth - 1);
        Assert.Equal(1, vm.VisibleCount);

        vm.UpdateViewportWidth(2 * SupportCarouselMiniAppViewModel.MinPageWidth);
        Assert.Equal(2, vm.VisibleCount);

        // Širší než 3× min → cap na počet stránek.
        vm.UpdateViewportWidth(10 * SupportCarouselMiniAppViewModel.MinPageWidth);
        Assert.Equal(3, vm.VisibleCount);
    }

    [Fact]
    public void VisiblePages_WindowFromCurrentIndex_WithWrap()
    {
        var vm = Build();
        vm.UpdateViewportWidth(2 * SupportCarouselMiniAppViewModel.MinPageWidth);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);   // CurrentIndex = 2 (Školení)

        Assert.Equal(["Školení", "Novinky"], VisibleTitles(vm));
    }

    [Fact]
    public void AllPagesVisible_HidesCycleControls_ResetsOrder_AndStopsTicking()
    {
        var vm = Build();
        vm.NextCommand.Execute(null);   // rozházené pořadí + rozjetá lišta
        vm.Tick();

        vm.UpdateViewportWidth(3 * SupportCarouselMiniAppViewModel.MinPageWidth);

        Assert.False(vm.ShowCycleControls);
        Assert.Equal(0, vm.CurrentIndex);   // přirozené pořadí jako staré 3 sloupce
        Assert.Equal(["Novinky", "Wiki", "Školení"], VisibleTitles(vm));
        Assert.Equal(0, vm.Progress);
        vm.Tick();
        Assert.Equal(0, vm.Progress);       // lišta stojí, stránka se neposouvá
        Assert.Equal(0, vm.CurrentIndex);
    }

    [Fact]
    public void NarrowingBack_ReenablesCycling()
    {
        var vm = Build();
        vm.UpdateViewportWidth(3 * SupportCarouselMiniAppViewModel.MinPageWidth);

        vm.UpdateViewportWidth(SupportCarouselMiniAppViewModel.MinPageWidth);

        Assert.True(vm.ShowCycleControls);
        Assert.Equal(1, vm.VisibleCount);
        vm.Tick();
        Assert.True(vm.Progress > 0);
    }

    [Fact]
    public void OnClosed_DisposesCreatedContents_LeavesUncreatedAlone()
    {
        // WPF elementy vyžadují STA — dedikované vlákno (vzor EDFilterComboBoxCommitTests).
        RunSta(() =>
        {
            var created = new DisposableElement();
            var vm = new SupportCarouselMiniAppViewModel(MockServices(),
                [new CarouselPage("A", () => created), ThrowingPage("B")]);
            _ = vm.VisiblePages;   // materializuje jen viditelnou stránku A

            vm.OnClosed();

            Assert.True(created.Disposed);
        });
    }

    private sealed class DisposableElement : FrameworkElement, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    // Spustí akci na dedikovaném STA vlákně a propaguje výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
