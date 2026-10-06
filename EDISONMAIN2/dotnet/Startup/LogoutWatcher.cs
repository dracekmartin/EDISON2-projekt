using System;
using System.Diagnostics;
using System.Windows.Threading;
using EDControls;

namespace EDISONMAIN2;

/// <summary>Výsledek pollu <c>maSeUkoncit</c>.</summary>
public record LogoutSignal(bool ShouldClose, int Seconds);

/// <summary>
/// Cooperative logout - periodicky se ptá serveru (<c>ALVA.SYSTEM.Main.maSeUkoncit</c>),
/// jestli má klient skončit (idle timeout / admin remote-logout). Při signálu zavolá
/// <c>onLogoutRequested(sekundy)</c> a přestane pollovat. Legacy <c>frmNoMDI.MainTimerProc</c>.
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

    internal void Tick()
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
    /// Jeden poll na klonu s <c>CacheLocks=2</c> - poll nesmí obnovit ACTIVITY historii, jinak by
    /// se server-side idle timer resetoval a logout by nikdy nenastal. Klon (ne mutace sdílené
    /// instance) → žádný race se souběžnými voláními.
    /// </summary>
    public static LogoutSignal Poll(IMLRest mlRest)
    {
        // object[]: legacy endpoint, výstup jsou číslované řádky (viz Parse).
        var output = mlRest.With(cacheLocks: 2).QueryOrThrow(PollClass, PollMethod, new object[] { });
        return Parse(output, mlRest.Delimiter);
    }

    /// <summary>
    /// Parse výstupu: řádek <c>ukoncit{del}{0|1}{del}{sekundy}</c>. Sekundy floor na 60
    /// (legacy invariant). Bez řádku / ukoncit=0 → <c>ShouldClose=false</c>.
    /// </summary>
    public static LogoutSignal Parse(object[] output, char delimiter)
    {
        var parts = LegacyOutput.Rows(output, delimiter).FirstOrDefault(r => r.Length >= 2 && r[0] == "ukoncit");
        if (parts is null) return new LogoutSignal(false, MinSeconds);
        bool shouldClose = parts[1] is "1";
        int seconds = parts.Length >= 3 && int.TryParse(parts[2], out int s) ? s : MinSeconds;
        return new LogoutSignal(shouldClose, Math.Max(MinSeconds, seconds));
    }
}
