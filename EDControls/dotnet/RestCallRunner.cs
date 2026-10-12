namespace EDControls;

/// <summary>Spustí blokující MLRest volání mimo UI thread. Implementace určuje, zda smí běžet souběžně.</summary>
public interface IRestCallRunner
{
    Task<T> RunAsync<T>(Func<IMLRest, T> call);
}

/// <summary>
/// Produkční runner: volání na pool threadu nad **klonem** sdílené instance. Klon je nutný, ne kosmetika -
/// <c>MLRestAdapter.QueryOrThrow</c> čte <c>RestStatus</c> až po <c>Query</c>, takže dvě souběžná
/// volání na téže instanci si stavy prohodí (spolknutá chyba / cizí výjimka). Klon má stejný GUID,
/// pro server je to tatáž session; ověřeno, že každý souběžný request obsluhuje jiný Caché proces,
/// takže si nešlapou ani po process-private <c>^||ML</c>.
/// Instanci i její Idfa, DataNamespace a CacheLocks čte na volajícím threadu: přepnutí firmy je
/// přepisuje na sdílené instanci na místě a zápis by jinak odešel pod firmou, na kterou uživatel přepnul.
/// </summary>
public sealed class RestCallRunner : IRestCallRunner
{
    private readonly Func<IMLRest> _rest;
    private readonly TaskScheduler _scheduler;

    public RestCallRunner(Func<IMLRest> rest) : this(rest, TaskScheduler.Default) { }

    internal RestCallRunner(Func<IMLRest> rest, TaskScheduler scheduler)
    {
        _rest = rest;
        _scheduler = scheduler;
    }

    public Task<T> RunAsync<T>(Func<IMLRest, T> call)
    {
        var clone = Snapshot(_rest());
        return Task.Factory.StartNew(() => call(clone()), CancellationToken.None,
            TaskCreationOptions.DenyChildAttach, _scheduler);
    }

    // Jiná implementace než adaptér je testovací fake: hodnoty session od stavby klonu neoddělí, klon proto vzniká hned.
    private static Func<IMLRest> Snapshot(IMLRest current)
    {
        if (current is MLRestAdapter adapter) return adapter.SnapshotSession();
        var clone = current.With();
        return () => clone;
    }
}

/// <summary>
/// Zápisy na pozadí, na jejichž výsledek nikdo nečeká (geometrie okna, telemetrie, preference). Pool thread konec
/// procesu nepřežije, proto se na ně čeká při ukončení procesu a v synchronním <c>EDUsageTelemetry.Flush</c>, který
/// shell volá při ukončení a přepnutí uživatele před odhlášením session (po něm by klon s <c>CacheLocks=1</c> server odmítl).
/// </summary>
internal static class BackgroundWrites
{
    /// <summary>Při výpadku spojení proces po zavření oken čeká nejvýš tak dlouho, pak zápis zahodí.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly object Sync = new();
    private static readonly HashSet<Task> Pending = [];

    // TD žádný Exit hook nemá, ProcessExit pokryje každou aplikaci nad EDControls.
    static BackgroundWrites() => AppDomain.CurrentDomain.ProcessExit += (_, _) => WaitPending(Timeout);

    /// <summary>Zápis se drží, dokud nedoběhne. Selhání se zahodí: zápis je best-effort a nikdo ho nečeká.</summary>
    internal static Task<T> Register<T>(Task<T> write)
    {
        lock (Sync) Pending.Add(write);
        write.ContinueWith(t =>
        {
            _ = t.Exception;
            lock (Sync) Pending.Remove(t);
        }, TaskScheduler.Default);
        return write;
    }

    /// <summary>False = limit vypršel a některý zápis ještě běží.</summary>
    internal static bool WaitPending(TimeSpan timeout)
    {
        Task[] pending;
        lock (Sync) pending = [.. Pending];
        try { return Task.WaitAll(pending, timeout); }
        catch (AggregateException) { return true; }
    }
}
