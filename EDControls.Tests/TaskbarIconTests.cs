using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// Obnova ikony v liště (<see cref="TaskbarIcon"/>) visí na class handleru Window.Loaded
/// (EDAppearance.Initialize); testuje se rozhodnutí, ne event - okna se v testech nezobrazují.
/// </summary>
public class TaskbarIconTests
{
    [Fact]
    public void ShouldRefresh_WindowWithIcon_True()
    {
        EDTesting.Sta.Run(() => Assert.True(TaskbarIcon.ShouldRefresh(new Window { Icon = new DrawingImage() })));
    }

    [Fact]
    public void ShouldRefresh_NoIcon_False()
    {
        // Bez Icon není co znovu poslat - okno bere ikonu z exe.
        EDTesting.Sta.Run(() => Assert.False(TaskbarIcon.ShouldRefresh(new Window())));
    }

    [Fact]
    public void ShouldRefresh_NotInTaskbar_False()
    {
        EDTesting.Sta.Run(() => Assert.False(TaskbarIcon.ShouldRefresh(
            new Window { Icon = new DrawingImage(), ShowInTaskbar = false })));
    }

    [Fact]
    public void ShouldRefresh_IconBound_False()
    {
        // Lokální zápis Icon by binding zahodil.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window();
            window.SetBinding(Window.IconProperty, new Binding("Icon"));

            Assert.False(TaskbarIcon.ShouldRefresh(window));
        });
    }

    [Fact]
    public void ShouldRefresh_OverlayWindow_False()
    {
        // Overlay okna EDControls (panel Podpora, „Akce probíhá") v liště nejsou.
        EDTesting.Sta.Run(() => Assert.False(TaskbarIcon.ShouldRefresh(
            new FakeOverlayWindow { Icon = new DrawingImage() })));
    }

    [Fact]
    public void ShouldRefresh_DockWindow_False()
    {
        EDTesting.Sta.Run(() => Assert.False(TaskbarIcon.ShouldRefresh(
            new AvalonDock.Controls.FakeDockWindow { Icon = new DrawingImage() })));
    }

    private sealed class FakeOverlayWindow : Window, IOverlayWindow;
}
