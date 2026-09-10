using System.Windows.Threading;

namespace EDTesting;

/// <summary>
/// Spuštění akce na STA threadu - WPF controly jdou konstruovat jen tam, xUnit běží na MTA.
/// Výjimka z akce se přehodí volajícímu, aby assert selhal v testu, ne na cizím threadu.
/// </summary>
public static class Sta
{
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }

    private static readonly object Gate = new();
    private static Dispatcher? _appDispatcher;

    /// <summary>
    /// Akce na trvalém STA vlákně s pumpujícím dispatcherem. Pro testy, které potřebují
    /// sdílenou <c>Application</c>: její dispatcher nesmí zaniknout (druhá Application se
    /// v AppDomain vytvořit nedá) a vlákno z <see cref="Run"/> umírá s testem.
    /// </summary>
    public static void RunOnAppThread(Action action)
    {
        var dispatcher = EnsureAppDispatcher();
        Exception? captured = null;
        dispatcher.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        if (captured != null) throw captured;
    }

    private static Dispatcher EnsureAppDispatcher()
    {
        lock (Gate)
        {
            if (_appDispatcher is not null) return _appDispatcher;

            Dispatcher? created = null;
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                created = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "EDTesting.AppThread",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            _appDispatcher = created;
            return _appDispatcher!;
        }
    }
}
