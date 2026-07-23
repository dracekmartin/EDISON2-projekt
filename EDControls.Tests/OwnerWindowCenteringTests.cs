using System.Windows;
using EDControls;

namespace EDControls.Tests;

public class OwnerWindowCenteringTests
{
    [Fact]
    public void ComputeTopLeft_CentersInClientAreaAtOrigin()
    {
        var topLeft = OwnerWindowCentering.ComputeTopLeft(
            new Point(0, 0), new Size(1000, 800), new Size(200, 100));

        Assert.Equal(400, topLeft.X);
        Assert.Equal(350, topLeft.Y);
    }

    [Fact]
    public void ComputeTopLeft_RespectsClientOriginOffset()
    {
        var topLeft = OwnerWindowCentering.ComputeTopLeft(
            new Point(1920, 100), new Size(1000, 800), new Size(200, 100));

        Assert.Equal(1920 + 400, topLeft.X);
        Assert.Equal(100 + 350, topLeft.Y);
    }

    [Fact]
    public void ComputeTopLeft_WindowLargerThanClient_YieldsNegativeOffset()
    {
        var topLeft = OwnerWindowCentering.ComputeTopLeft(
            new Point(0, 0), new Size(300, 200), new Size(400, 400));

        Assert.Equal(-50, topLeft.X);
        Assert.Equal(-100, topLeft.Y);
    }
}
