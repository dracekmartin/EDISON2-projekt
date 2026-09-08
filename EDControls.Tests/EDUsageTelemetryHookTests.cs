using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Napojení usage telemetrie na okno (<c>EDUsageTelemetryHook</c>). Hook visí na class handleru
/// <c>Window.Loaded</c> z <c>EDAppearance.Initialize</c>; testuje se jeho rozhodnutí a efekt,
/// ne event - zobrazovat okna v testech není potřeba.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class EDUsageTelemetryHookTests
{
    public EDUsageTelemetryHookTests() => EDUsageTelemetry.ResetForTests();

    private static EDServices MockServices() =>
        new(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static Window WindowWithVm() => new() { DataContext = new EDViewModel.EDViewModel(MockServices()) };

    [Fact]
    public void ShouldTrack_WindowWithViewModel_True()
    {
        EDTesting.Sta.Run(() => Assert.True(EDUsageTelemetryHook.ShouldTrack(WindowWithVm())));
    }

    [Fact]
    public void ShouldTrack_WindowWithoutViewModel_False()
    {
        // Popupy EDControls, overlay okna a chrome AvalonDocku ViewModel nemají - šum se odfiltruje sám.
        EDTesting.Sta.Run(() => Assert.False(EDUsageTelemetryHook.ShouldTrack(new Window())));
    }

    [Fact]
    public void ShouldTrack_OptedOutViewModel_False()
    {
        // PersistedFormId = null je opt-out z persistence i z telemetrie.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { DataContext = new NoFormIdViewModel(MockServices()) };

            Assert.False(EDUsageTelemetryHook.ShouldTrack(window));
        });
    }

    [Fact]
    public void ShouldTrack_AlreadyHooked_False()
    {
        // Druhý Loaded téhož okna (re-parent) nesmí navěsit druhou sadu handlerů.
        EDTesting.Sta.Run(() =>
        {
            var window = WindowWithVm();
            EDUsageTelemetryHook.Hook(window, "TEST.Window");

            Assert.False(EDUsageTelemetryHook.ShouldTrack(window));
        });
    }

    [Fact]
    public void Hook_TracksOpen()
    {
        EDTesting.Sta.Run(() =>
        {
            EDUsageTelemetryHook.Hook(WindowWithVm(), "TEST.Window");

            Assert.Equal(1, EDUsageTelemetry.BufferCountForTests);
        });
    }

    [Fact]
    public void Hook_NamedButtonClick_IsTracked()
    {
        EDTesting.Sta.Run(() =>
        {
            var button = new Button { Name = "BtnSave" };
            var window = WindowWithVm();
            window.Content = button;
            EDUsageTelemetryHook.Hook(window, "TEST.Window");

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(2, EDUsageTelemetry.BufferCountForTests);   // OPEN + BtnSave
        });
    }

    [Fact]
    public void Hook_UnnamedButtonClick_IsIgnored()
    {
        EDTesting.Sta.Run(() =>
        {
            var button = new Button();
            var window = WindowWithVm();
            window.Content = button;
            EDUsageTelemetryHook.Hook(window, "TEST.Window");

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(1, EDUsageTelemetry.BufferCountForTests);   // jen OPEN
        });
    }

    [Fact]
    public void Hook_NamedMenuItemClick_IsTracked()
    {
        EDTesting.Sta.Run(() =>
        {
            var item = new MenuItem { Name = "MnuExport" };
            var window = WindowWithVm();
            window.Content = new Menu { Items = { item } };
            EDUsageTelemetryHook.Hook(window, "TEST.Window");

            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(2, EDUsageTelemetry.BufferCountForTests);   // OPEN + MnuExport
        });
    }

    [Fact]
    public void AttachToWindow_DoesNotTrack()
    {
        // Hook smí být jen na class handleru - na obou místech by okna shellu hlásila OPEN i kliky dvakrát.
        EDTesting.Sta.Run(() =>
        {
            var vm = new EDViewModel.EDViewModel(MockServices());
            var window = new Window { DataContext = vm };
            vm.AttachToWindow(window);

            Assert.Equal(0, EDUsageTelemetry.BufferCountForTests);
        });
    }

    private sealed class NoFormIdViewModel(EDServices services) : EDViewModel.EDViewModel(services)
    {
        protected override string? PersistedFormId => null;
    }

}
