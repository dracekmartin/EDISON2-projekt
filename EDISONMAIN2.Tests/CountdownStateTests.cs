using System.Windows;
using EDISONMAIN2.Windows.LogoutCountdownWindow;

namespace EDISONMAIN2.Tests;

public class CountdownStateTests
{
    [Fact]
    public void Tick_CountsDown_SignalsExpiryExactlyOnce()
    {
        var state = new CountdownState(3);

        Assert.False(state.Tick());  // 2
        Assert.False(state.Tick());  // 1
        Assert.True(state.Tick());   // 0 → vypršení, právě teď
        Assert.False(state.Tick());  // po vypršení už nikdy (onExpired se nesmí volat dvakrát)
        Assert.Equal(0, state.SecondsLeft);
    }

    [Fact]
    public void Text_ReportsRemainingSeconds()
    {
        var state = new CountdownState(15);

        Assert.Equal("Aplikace bude ukončena za 15 s.", state.Text);
        state.Tick();
        Assert.Equal("Aplikace bude ukončena za 14 s.", state.Text);
    }

    [Fact]
    public void BottomRight_SitsInCornerOfShellMonitorWorkArea()
    {
        // Monitor shellu vlevo od hlavního (záporné souřadnice), odpočet nesmí skončit na hlavním.
        var topLeft = LogoutCountdownWindow.BottomRight(new Rect(-1920, 0, 1920, 1040), new Size(300, 80));

        Assert.Equal(new Point(-1920 + 1920 - 300 - 16, 1040 - 80 - 16), topLeft);
    }
}
