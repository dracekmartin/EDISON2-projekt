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
