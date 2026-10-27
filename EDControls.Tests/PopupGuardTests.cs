using System.Threading;
using System.Windows.Threading;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// Hláška po začátku ukončení aplikace okno nevytvoří: XAML by hodil „The Application object is being shut down."
/// Testuje se přes dispečera, ne přes Application.Current, který ostatní testy procesu mění.
/// </summary>
public class PopupGuardTests
{
    [Fact]
    public void CanOpen_WithoutApplication_False() => Assert.False(PopupGuard.CanOpen(null));

    [Fact]
    public void CanOpen_LiveDispatcher_TrueAfterShutdownFalse()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        Assert.True(PopupGuard.CanOpen(dispatcher));

        dispatcher!.InvokeShutdown();
        thread.Join(5000);

        Assert.False(PopupGuard.CanOpen(dispatcher));
    }
}
