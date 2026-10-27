using System.Windows;

namespace EDControls.Tests;

/// <summary>Volba monitoru pro okno bez uložené pozice a bez vlastníka. Win32 hledání monitorů nahrazuje seznam jmen.</summary>
public class MonitorPlacementTests
{
    private const string Left = @"\\.\DISPLAY2";
    private const string Primary = @"\\.\DISPLAY1";

    private static readonly HashSet<string> Connected = [Left, Primary];

    private static string? Choose(string? owner = null, string? main = null, string? preferred = null,
                                  string?[]? others = null, string? cursor = null) =>
        MonitorPlacement.ChooseMonitor(
            MonitorPlacement.Candidates(() => owner, () => main, preferred, others ?? [], () => cursor),
            Connected.Contains);

    [Fact]
    public void Owner_WinsOverEverything()
    {
        Assert.Equal(Left, Choose(owner: Left, main: Primary, preferred: Primary, cursor: Primary));
    }

    [Fact]
    public void MainWindow_WinsOverShellHint()
    {
        // TD přesunutý na jiný monitor než shell: hláška bez vlastníka patří k TD.
        Assert.Equal(Primary, Choose(main: Primary, preferred: Left, cursor: Left));
    }

    [Fact]
    public void ShellHint_WinsOverOtherWindowsAndCursor()
    {
        Assert.Equal(Left, Choose(preferred: Left, others: [Primary], cursor: Primary));
    }

    [Fact]
    public void OtherVisibleWindow_WinsOverCursor()
    {
        // Hláška při startu shellu, než existuje hlavní okno: patří k login oknu.
        Assert.Equal(Left, Choose(others: [null, Left], cursor: Primary));
    }

    [Fact]
    public void Cursor_IsLastResort()
    {
        Assert.Equal(Primary, Choose(cursor: Primary));
    }

    [Fact]
    public void DisconnectedHint_IsSkipped()
    {
        // Monitor shellu odpojený mezi spuštěním a otevřením okna.
        Assert.Equal(Primary, Choose(preferred: @"\\.\DISPLAY9", cursor: Primary));
    }

    [Fact]
    public void EmptyCandidates_AreSkipped()
    {
        Assert.Equal(Left, Choose(owner: "", main: null, preferred: "", cursor: Left));
    }

    [Fact]
    public void NothingConnected_ReturnsNull()
    {
        Assert.Null(Choose(preferred: @"\\.\DISPLAY9"));
    }

    [Fact]
    public void MainWindow_IsNoCandidateForItself()
    {
        // PDF okno TD je první okno procesu, tedy MainWindow: jinak by nápovědu přebil jeho vlastní hlavní monitor.
        EDTesting.Sta.Run(() =>
        {
            var window = new Window();
            var main = new Window();

            Assert.Null(MonitorPlacement.MainWindowOtherThan(window, window));
            Assert.Same(main, MonitorPlacement.MainWindowOtherThan(main, window));
        });
    }

    [Fact]
    public void Owner_DecidesWithoutAskingForCursor()
    {
        var cursorAsked = false;

        MonitorPlacement.ChooseMonitor(
            MonitorPlacement.Candidates(() => Left, () => null, null, [], () => { cursorAsked = true; return Primary; }),
            Connected.Contains);

        Assert.False(cursorAsked);
    }

    [Fact]
    public void CenterIn_CentersOnSecondaryMonitorLeftOfPrimary()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 800, Height = 600 };

            MonitorPlacement.CenterIn(window, new Rect(-1920, 0, 1920, 1040));

            Assert.Equal(-1920 + 560, window.Left);
            Assert.Equal(220, window.Top);
        });
    }

    [Fact]
    public void CenterIn_WindowLargerThanArea_KeepsCaptionOnArea()
    {
        EDTesting.Sta.Run(() =>
        {
            var window = new Window { Width = 2500, Height = 1200 };

            MonitorPlacement.CenterIn(window, new Rect(-1920, 0, 1920, 1040));

            Assert.Equal(-1920, window.Left);
            Assert.Equal(0, window.Top);
        });
    }
}
