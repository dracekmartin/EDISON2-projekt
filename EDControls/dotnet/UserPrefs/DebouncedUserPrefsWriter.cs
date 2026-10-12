using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EDControls.UserPrefs;

/// <summary>
/// Odložený best-effort zápis preference controlu: série změn = jeden Save po 500 ms klidu. Volat z UI
/// vlákna - odpočet pokračuje na jeho kontextu, snapshot proto smí číst WPF stav. Prázdný klíč = nic.
/// Session (firmu) zápisu určuje první čekající změna, ne okamžik odeslání: flush v <c>Unloaded</c> přijde
/// až po přepnutí firmy. Změna pod jinou session nejdřív odešle čekající zápis té staré, a to se stavem controlu
/// už po nové změně (stav se čte až při odeslání).
/// </summary>
internal sealed class DebouncedUserPrefsWriter(
    Func<IUserPrefsService> prefs,
    string scope,
    Func<string?> key,
    Func<JsonNode> snapshot,
    Func<CancellationToken, Task>? delay = null)
{
    internal static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);

    private readonly Func<CancellationToken, Task> _delay = delay ?? (ct => Task.Delay(Delay, ct));
    private CancellationTokenSource? _pending;
    private IUserPrefsService? _session;

    /// <summary>Restartuje odpočet; uloží se až snapshot po poslední změně.</summary>
    public void Schedule()
    {
        if (string.IsNullOrEmpty(key())) return;
        var session = CaptureSession();
        if (_pending is not null && !ReferenceEquals(session, _session)) _ = FlushAsync();
        if (_pending is null) _session = session;
        _pending?.Cancel();
        var cts = _pending = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(cts);
    }

    /// <summary>Uloží hned, ale jen čekající změnu - bezpodmínečný save by poslal request při každém zavření.</summary>
    public Task FlushAsync()
    {
        if (_pending is not { } cts) return Task.CompletedTask;
        cts.Cancel();
        _pending = null;
        return SaveAsync(_session);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cts)
    {
        try { await _delay(cts.Token); }
        catch (OperationCanceledException) { return; }
        if (cts.IsCancellationRequested || !ReferenceEquals(_pending, cts)) return;
        _pending = null;
        await SaveAsync(_session);
    }

    // Null = snímek selhal (výjimka), zápis pak jde pod session z okamžiku odeslání jako dřív.
    private IUserPrefsService? CaptureSession()
    {
        try { return prefs().Capture(); }
        catch { return null; }
    }

    private async Task SaveAsync(IUserPrefsService? session)
    {
        var k = key();
        if (string.IsNullOrEmpty(k)) return;
        try { await (session ?? prefs()).SaveAsync(scope, k, snapshot()); }
        catch { /* best-effort */ }
    }
}
