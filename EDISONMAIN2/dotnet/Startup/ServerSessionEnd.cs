using System;
using System.Threading;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

/// <summary>
/// Konec session ze serveru (zámek ACTIVITY zmizel: timeout, výpadek, admin disconnect, změna IP stanice;
/// nebo nesedí verze): hláška a ukončení aplikace. Každý další request mrtvé session vyvolá tutéž
/// událost znovu, proto se konec obslouží jednou. Session-scoped objekt, bootstrapper tou dobou nežije.
/// </summary>
internal sealed class ServerSessionEnd(IStartupHost host, ShellShutdown shutdown, Func<IPopupDisplayer> popups)
{
    internal const string ActivityLostMessage = "Spojení se serverem bylo přerušeno. Aplikace bude ukončena.";
    internal const string VersionMismatchMessage = "Rozdílná verze Caché a verze Edison, aplikace bude ukončena.";

    private int _ended;

    /// <summary>Napojí konec na MLRest každé nové session (start i přihlášení jiného uživatele).</summary>
    internal void Wire(IMLRest session) => LegacyApi.OnSessionEnd(session,
        activityLost: () => End(session, ActivityLostMessage),
        versionMismatch: () => End(session, VersionMismatchMessage));

    internal void End(IMLRest session, string message)
    {
        if (Interlocked.Exchange(ref _ended, 1) == 1) return;
        // Zbývající requesty (zavření oken, flush telemetrie, logout) jdou bez kontroly zámku jako při běžném zavření.
        session.CacheLocks = 0;
        // Uživatel shell zavřel sám, nebo ukončení už běží: hláška by přišla po zavření.
        if (host.ShutdownRequested) return;
        host.Dispatch(() =>
        {
            if (host.ShutdownRequested) return;
            // Druhá šance už nepřijde (příznak, Locks=0): bez ukončení by shell běžel nad mrtvou session.
            try { popups().Info(message); }
            finally { shutdown.Force(); }
        });
    }
}
