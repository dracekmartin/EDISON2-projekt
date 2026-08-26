using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Sdílený dávkovač legacy dashboardů: posbírá požadavky, které přijdou v téže dispatcher operaci,
/// a pošle je serveru jako **jeden** <c>ALVA.EDISON.MiniAplikaceAPI.LoadData</c> request s N parametry
/// <c>appType</c> (parita s legacy <c>clsMiniApplication.LoadData</c>). Tím se obnova uloženého
/// layoutu s N panely vejde do jednoho round-tripu místo N a nejede na UI vlákně.
/// Jedna instance na shell - drží ji <see cref="LegacyDashboardMiniAppProvider"/>.
/// </summary>
public sealed class LegacyDashboardLoader
{
    private const string CacheClass = "ALVA.EDISON.MiniAplikaceAPI";

    private readonly IRestCallRunner _runner;
    private readonly Action<Action> _schedule;

    // Dotýká se jich jen UI vlákno (LoadAsync z VM, flush z dispatcheru) - proto bez zámku.
    private readonly Dictionary<string, TaskCompletionSource<DashboardData>> _pending = [];
    private bool _refreshPending;
    private bool _flushScheduled;

    public LegacyDashboardLoader()
        : this(new RestCallRunner(() => EDServices.Current.RequireMLRest), DispatcherSchedule()) { }

    /// <param name="schedule">
    /// Odloží flush za konec probíhající dispatcher operace - v ní se stihnou přihlásit všechny
    /// panely (<c>MiniAppManager.RestoreLayout</c> i hromadná obnova mountují/obnovují v cyklu).
    /// Testy si sem dají ruční spouštěč.
    /// </param>
    public LegacyDashboardLoader(IRestCallRunner runner, Action<Action> schedule)
    {
        _runner = runner;
        _schedule = schedule;
    }

    /// <summary>
    /// Vrátí data miniaplikace; požadavek se cestou spojí s ostatními do jedné dávky.
    /// <paramref name="refresh"/> = server data nejdřív přepočítá (<c>PrepareData</c>) - dávka jede
    /// s jedním společným flagem, stačí tedy jeden žadatel, aby přepočet chtěl.
    /// </summary>
    public Task<DashboardData> LoadAsync(string appType, bool refresh)
    {
        // Záměrně bez RunContinuationsAsynchronously: SetResult padá v continuation flushe (UI
        // vlákno), takže se panely naplní rovnou v ní - ne odloženě z pool threadu.
        if (!_pending.TryGetValue(appType, out var pending))
            _pending[appType] = pending = new TaskCompletionSource<DashboardData>();
        _refreshPending |= refresh;

        if (!_flushScheduled)
        {
            _flushScheduled = true;
            _schedule(Flush);
        }
        return pending.Task;
    }

    private void Flush()
    {
        _flushScheduled = false;
        if (_pending.Count == 0) return;

        var batch = _pending.ToList();
        bool refresh = _refreshPending;
        _pending.Clear();
        _refreshPending = false;
        _ = RunBatchAsync(batch, refresh);
    }

    private async Task RunBatchAsync(
        List<KeyValuePair<string, TaskCompletionSource<DashboardData>>> batch, bool refresh)
    {
        try
        {
            var appTypes = batch.Select(b => b.Key).ToList();
            var (output, delimiter) = await _runner.RunAsync(rest =>
            {
                // Legacy delimited API: object[] vstup i výstup, oddělovač polí = MLRest.Delimiter.
                char d = rest.Delimiter;
                var input = new object[appTypes.Count + 1];
                input[0] = $"refresh{d}{(refresh ? 1 : 0)}";
                for (int i = 0; i < appTypes.Count; i++) input[i + 1] = $"appType{d}{appTypes[i]}";
                return (Output: rest.QueryOrThrow(CacheClass, "LoadData", input), Delimiter: d);
            });

            var parsed = DashboardData.ParseAll(output, delimiter);
            foreach (var (appType, pending) in batch)
                pending.TrySetResult(parsed.GetValueOrDefault(appType, DashboardData.Empty));
        }
        catch (Exception ex)
        {
            foreach (var (_, pending) in batch) pending.TrySetException(ex);
        }
    }

    // Dispatcher se zachytává při konstrukci (běží na UI vlákně); Background priorita pustí
    // napřed layout a vykreslení, takže dávka odejde až za prvním paintem okna.
    private static Action<Action> DispatcherSchedule()
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return action => dispatcher.InvokeAsync(action, DispatcherPriority.Background);
    }
}
