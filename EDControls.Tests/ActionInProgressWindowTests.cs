using System.Windows;
using System.Windows.Threading;
using EDControls.EDActionInProgress;
using EDTesting;

namespace EDControls.Tests;

/// <summary>Karta „Akce probíhá" v owned okně: reakce na změnu stavu ownera.</summary>
public class ActionInProgressWindowTests
{
    [WindowFact]
    public void OwnerMaximized_CubeAnimationKeepsRunning()
    {
        WithRunningCube((owner, overlay) =>
        {
            double before = overlay.CubeTime;

            owner.WindowState = WindowState.Maximized;
            PumpUntil(() => false, TimeSpan.FromMilliseconds(100));

            // Restart by vrátil čas skoro na nulu (prázdné plátno).
            Assert.True(overlay.CubeTime >= before, $"Krychle se skládá znovu: {overlay.CubeTime} < {before}.");
        });
    }

    [WindowFact]
    public void OwnerRestoredFromMinimized_CubeAnimationStartsAgain()
    {
        WithRunningCube((owner, overlay) =>
        {
            owner.WindowState = WindowState.Minimized;
            Assert.True(PumpUntil(() => !overlay.IsVisible), "Karta se s minimalizací neschovala.");
            Assert.Equal(0, overlay.CubeTime);

            owner.WindowState = WindowState.Normal;
            Assert.True(PumpUntil(() => overlay.CubeTime > 0), "Animace krychle se po obnovení nerozběhla.");
        });
    }

    private static void WithRunningCube(Action<Window, ActionInProgressWindow> test)
    {
        // Vypnuté animace ve Windows = statická krychle, není co měřit.
        if (!SystemParameters.ClientAreaAnimation) return;

        Sta.RunOnAppThread(() =>
        {
            var owner = new Window { ShowInTaskbar = false, ShowActivated = false, Width = 400, Height = 300 };
            owner.Show();
            var overlay = new ActionInProgressWindow(owner);
            try
            {
                overlay.ShowOverlay();
                Assert.True(PumpUntil(() => overlay.CubeTime > 300), "Animace krychle se nerozběhla.");
                test(owner, overlay);
            }
            finally
            {
                overlay.Close();
                owner.Close();
            }
        });
    }

    // Hodiny animace tikají jen při pumpování dispatcheru.
    private static bool PumpUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        return condition();
    }
}
