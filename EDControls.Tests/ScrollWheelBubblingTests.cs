using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDControls;

namespace EDControls.Tests;

/// <summary>
/// Kolečko nad vnořeným seznamem: co seznam nemá kam použít, dostane rodič - jinak vnější panel
/// (přehledy v miniaplikacích) rolovat jen nad svým posuvníkem.
/// </summary>
public class ScrollWheelBubblingTests
{
    [Theory]
    [InlineData(0, 100, -120, true)]     // na začátku, dolů: seznam má kam
    [InlineData(0, 100, 120, false)]     // na začátku, nahoru: předat rodiči
    [InlineData(100, 100, -120, false)]  // na konci, dolů: předat rodiči
    [InlineData(100, 100, 120, true)]    // na konci, nahoru: seznam má kam
    [InlineData(50, 100, -120, true)]
    [InlineData(50, 100, 120, true)]
    [InlineData(0, 0, -120, false)]      // nic k rolování (sekce přehledu na výšku obsahu): vždy rodiči
    [InlineData(0, 0, 120, false)]
    public void CanScroll_OnlyWhenInnerHasRoomInWheelDirection(double offset, double scrollable, int delta, bool expected)
    {
        Assert.Equal(expected, ScrollWheelBubbling.CanScroll(offset, scrollable, delta));
    }

    [Fact]
    public void WheelOverUnscrollableInner_ReachesParent()
    {
        EDTesting.Sta.Run(() =>
        {
            var inner = new ScrollViewer();           // bez obsahu: ScrollableHeight = 0
            ScrollWheelBubbling.SetIsEnabled(inner, true);
            var parent = new Grid();
            parent.Children.Add(inner);
            int parentDelta = 0;
            parent.MouseWheel += (_, e) => parentDelta = e.Delta;

            inner.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent });

            Assert.Equal(-120, parentDelta);
        });
    }

    [Fact]
    public void Disabled_DoesNotForward()
    {
        EDTesting.Sta.Run(() =>
        {
            var inner = new ScrollViewer();
            ScrollWheelBubbling.SetIsEnabled(inner, true);
            ScrollWheelBubbling.SetIsEnabled(inner, false);
            var parent = new Grid();
            parent.Children.Add(inner);
            bool reached = false;
            parent.MouseWheel += (_, _) => reached = true;

            inner.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent });

            Assert.False(reached);
        });
    }
}
