namespace EDControls;

/// <summary>Spustí blokující MLRest volání mimo UI thread. Implementace určuje, zda smí běžet souběžně.</summary>
public interface IRestCallRunner
{
    Task<T> RunAsync<T>(Func<IMLRest, T> call);

    /// <summary>
    /// Runner vázaný na session z okamžiku volání, pro odložený zápis (debounce, fronta): přepnutí firmy mezi
    /// změnou a odesláním by ho jinak poslalo pod novou firmou. Dokud se session nezmění, vrací týž objekt.
    /// Výchozí implementace session nedrží a vrací sama sebe (testovací runner nad pevnou instancí).
    /// </summary>
    IRestCallRunner Capture() => this;
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
    private CapturedRunner? _captured;

    public RestCallRunner(Func<IMLRest> rest) : this(rest, TaskScheduler.Default) { }

    internal RestCallRunner(Func<IMLRest> rest, TaskScheduler scheduler)
    {
        _rest = rest;
        _scheduler = scheduler;
    }

    public Task<T> RunAsync<T>(Func<IMLRest, T> call) => Start(Snapshot(_rest()), call, _scheduler);

    public IRestCallRunner Capture()
    {
        var current = _rest();
        if (_captured is { } captured && captured.Matches(current)) return captured;
        return _captured = new CapturedRunner(current, Snapshot(current), _scheduler);
    }

    private static Task<T> Start<T>(Func<IMLRest> clone, Func<IMLRest, T> call, TaskScheduler scheduler) =>
        Task.Factory.StartNew(() => call(clone()), CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);

    // Jiná implementace než adaptér je testovací fake: hodnoty session od stavby klonu neoddělí, klon proto vzniká hned.
    private static Func<IMLRest> Snapshot(IMLRest current)
    {
        if (current is MLRestAdapter adapter) return adapter.SnapshotSession();
        var clone = current.With();
        return () => clone;
    }

    /// <summary>Session sdílené instance <paramref name="instance"/> ve chvíli <see cref="Capture"/>.</summary>
    private sealed class CapturedRunner(IMLRest instance, Func<IMLRest> snapshot, TaskScheduler scheduler) : IRestCallRunner
    {
        private readonly string _idfa = instance.Idfa;
        private readonly string _dataNamespace = instance.DataNamespace;
        private readonly int _cacheLocks = instance.CacheLocks;

        internal bool Matches(IMLRest current) =>
            ReferenceEquals(current, instance) && current.Idfa == _idfa
            && current.DataNamespace == _dataNamespace && current.CacheLocks == _cacheLocks;

        // Sdílená instance už je jinde (přepnutí firmy, odhlášení): zámek zachycené firmy mohl zaniknout
        // a s CacheLocks=1 by server zápis odmítl (LockExist).
        public Task<T> RunAsync<T>(Func<IMLRest, T> call) => Start(
            Matches(instance) ? snapshot : () => instance.With(cacheLocks: 0, idfa: _idfa, dataNamespace: _dataNamespace),
            call, scheduler);

        public IRestCallRunner Capture() => this;
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
