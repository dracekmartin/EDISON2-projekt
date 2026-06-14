using System;
using System.Diagnostics;
using System.Windows.Threading;
using EDControls;

namespace EDISONMAIN2;

/// <summary>Výsledek pollu <c>maSeUkoncit</c>.</summary>
public record LogoutSignal(bool ShouldClose, int Seconds);

/// <summary>
/// Cooperative logout — periodicky se ptá serveru (<c>ALVA.SYSTEM.Main.maSeUkoncit</c>),
/// jestli má klient skončit (idle timeout / admin remote-logout). Při signálu zavolá
/// <c>onLogoutRequested(sekundy)</c> a přestane pollovat. Detaily mechanismu:
/// RESPONSIBILITIES §3.5/3.9, legacy <c>frmNoMDI.MainTimerProc</c>.
/// </summary>
public class LogoutWatcher
{
    private const string PollClass = "ALVA.SYSTEM.Main";
    private const string PollMethod = "maSeUkoncit";
    private const int MinSeconds = 60;

    private readonly Func<IMLRest?> _mlRestProvider;
    private readonly Action<int> _onLogoutRequested;
    private readonly DispatcherTimer _timer;

    public LogoutWatcher(Func<IMLRest?> mlRestProvider, Action<int> onLogoutRequested, TimeSpan? interval = null)
    {
        _mlRestProvider = mlRestProvider;
        _onLogoutRequested = onLogoutRequested;
        _timer = new DispatcherTimer { Interval = interval ?? TimeSpan.FromSeconds(60) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        var mlRest = _mlRestProvider();
        if (mlRest is null) return;
        LogoutSignal signal;
        try
        {
            signal = Poll(mlRest);
        }
        catch (Exception ex)
        {
            // Tvrdé selhání (zaniklý zámek) řeší event-driven EdisonActivityFailed → shutdown;
            // transientní výpadek jen přeskočíme, příští tick zkusí znovu.
            Debug.WriteLine($"LogoutWatcher poll failed: {ex.Message}");
            return;
        }
        if (!signal.ShouldClose) return;
        Stop();
        _onLogoutRequested(signal.Seconds);
    }

    /// <summary>
    /// Jeden poll. <c>CacheLocks=2</c> po dobu volání — poll nesmí obnovit ACTIVITY historii,
    /// jinak by se server-side idle timer resetoval a logout by nikdy nenastal.
    /// </summary>
    /// <remarks>
    /// <c>CacheLocks</c> je per-instance mutable stav sdílené MLRest instance. Background volání
    /// (např. <c>CacheUserPrefsService</c> přes <c>Task.Run</c>) se může s pollem překrýt a přečíst
    /// <c>2</c> → to volání neobnoví ACTIVITY historii. Benigní: MLRest dělá HTTP per-call (žádná
    /// korupce, vlastní <c>^||ML</c> per request), efekt = jedno vynechané obnovení, self-correcting
    /// (idle = setrvalá nečinnost). Bulletproof varianta by chtěla dedikované spojení jen pro poll.
    /// </remarks>
    public static LogoutSignal Poll(IMLRest mlRest)
    {
        int previous = mlRest.CacheLocks;
        mlRest.CacheLocks = 2;
        try
        {
            var output = mlRest.QueryOrThrow(PollClass, PollMethod, new object[] { });
            return Parse(output, mlRest.Delimiter);
        }
        finally
        {
            mlRest.CacheLocks = previous;
        }
    }

    /// <summary>
    /// Parse výstupu: řádek <c>ukoncit{del}{0|1}{del}{sekundy}</c>. Sekundy floor na 60
    /// (legacy invariant). Bez řádku / ukoncit=0 → <c>ShouldClose=false</c>.
    /// </summary>
    public static LogoutSignal Parse(object[] output, char delimiter)
    {
        for (int i = 0; i < output.Length; i++)
        {
            var parts = (output[i]?.ToString() ?? "").Split(delimiter);
            if (parts.Length < 2 || parts[0] != "ukoncit") continue;
            bool shouldClose = parts[1] is "1";
            int seconds = parts.Length >= 3 && int.TryParse(parts[2], out int s) ? s : MinSeconds;
            return new LogoutSignal(shouldClose, Math.Max(MinSeconds, seconds));
        }
        return new LogoutSignal(false, MinSeconds);
    }
}
