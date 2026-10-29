using System.Windows;
using System.Windows.Threading;
using EDControls.EDLogo;
using EDTesting;
using Logo = EDControls.EDLogo.EDLogo;

namespace EDControls.Tests;

/// <summary>
/// Logo EDISON: co ukazuje stojící logo a kdy běží hodiny. Regrese, které to hlídá: logo čekající na
/// zobrazení probleskne složené, skryté logo drží tikající clock, Once bez <c>AnimationCompleted</c>.
/// </summary>
public class EDLogoTests
{
    [Fact]
    public void None_ShowsAssembledLogo()
    {
        Sta.Run(() => AssertAssembled(new Logo()));
    }

    [Theory]
    [InlineData(EDLogoAnimationMode.Once)]
    [InlineData(EDLogoAnimationMode.Loop)]
    public void Animated_WhileHidden_WaitsWithEmptyCanvas(EDLogoAnimationMode mode)
    {
        Sta.Run(() =>
        {
            var logo = new Logo { AnimationMode = mode };

            Assert.Equal(0, logo.Time);
            Assert.All(logo.Cube.Atoms, a => Assert.Equal(0, a.Opacity));
            Assert.All(logo.Cube.Bonds, b => Assert.Equal(0, b.Opacity));
        });
    }

    [Fact]
    public void BackToNone_ShowsAssembledLogo()
    {
        Sta.Run(() =>
        {
            var logo = new Logo { AnimationMode = EDLogoAnimationMode.Loop };
            logo.AnimationMode = EDLogoAnimationMode.None;
            AssertAssembled(logo);
        });
    }

    [WindowFact]
    public void Once_InShownWindow_AssemblesAndCompletesOnce()
    {
        WithShownLogo(EDLogoAnimationMode.Once, (window, logo, completed) =>
        {
            Assert.True(PumpUntil(() => completed() > 0), "Once nedoběhlo.");
            PumpUntil(() => false, TimeSpan.FromMilliseconds(100));

            Assert.Equal(1, completed());
            AssertAssembled(logo);
        });
    }

    [WindowFact]
    public void Once_HiddenBeforeEnd_StopsAssembled()
    {
        WithShownLogo(EDLogoAnimationMode.Once, (window, logo, completed) =>
        {
            // Vypnuté animace ve Windows dokončí Once hned při zobrazení - hodiny tu nejsou.
            if (SystemParameters.ClientAreaAnimation)
                Assert.True(PumpUntil(() => logo.Time > 0), "Animace se nerozběhla.");

            window.Hide();

            Assert.Equal(1, completed());
            Assert.Equal(0, logo.Time);
            AssertAssembled(logo);
        });
    }

    private static void AssertAssembled(Logo logo)
    {
        Assert.All(logo.Cube.Atoms, a =>
        {
            Assert.Equal(1, a.Scale, 9);
            Assert.Equal(1, a.Opacity, 9);
        });
        Assert.All(logo.Cube.Bonds, b => Assert.Equal(1, b.Progress, 9));
    }

    private static void WithShownLogo(EDLogoAnimationMode mode, Action<Window, Logo, Func<int>> test)
    {
        Sta.RunOnAppThread(() =>
        {
            int completed = 0;
            var logo = new Logo { Width = 64, Height = 64, AnimationMode = mode };
            logo.AnimationCompleted += (_, _) => completed++;
            var window = new Window { ShowInTaskbar = false, ShowActivated = false, Content = logo, SizeToContent = SizeToContent.WidthAndHeight };
            try
            {
                window.Show();
                test(window, logo, () => completed);
            }
            finally
            {
                window.Close();
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
