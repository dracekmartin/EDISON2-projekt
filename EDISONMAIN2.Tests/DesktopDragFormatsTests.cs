using System.Windows;
using EDISONMAIN2.Desktop;
using Point = System.Windows.Point;

namespace EDISONMAIN2.Tests;

public class DesktopDragFormatsTests
{
    [Fact]
    public void ThresholdExceeded_DistinguishesClickFromDrag()
    {
        var start = new Point(100, 100);
        double h = SystemParameters.MinimumHorizontalDragDistance;
        double v = SystemParameters.MinimumVerticalDragDistance;

        Assert.False(DesktopDragFormats.ThresholdExceeded(start, start));
        Assert.False(DesktopDragFormats.ThresholdExceeded(start, new Point(100 + h / 2, 100 + v / 2)));
        Assert.True(DesktopDragFormats.ThresholdExceeded(start, new Point(100 + h, 100)));
        Assert.True(DesktopDragFormats.ThresholdExceeded(start, new Point(100, 100 - v)));  // oba směry
    }
}
