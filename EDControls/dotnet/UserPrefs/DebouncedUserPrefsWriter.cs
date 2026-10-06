using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EDControls.UserPrefs;

/// <summary>
/// Odložený best-effort zápis preference controlu: série změn = jeden Save po 500 ms klidu. Volat z UI
/// vlákna - odpočet pokračuje na jeho kontextu, snapshot proto smí číst WPF stav. Prázdný klíč = nic.
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

    /// <summary>Restartuje odpočet; uloží se až snapshot po poslední změně.</summary>
    public void Schedule()
    {
        if (string.IsNullOrEmpty(key())) return;
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
        return SaveAsync();
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cts)
    {
        try { await _delay(cts.Token); }
        catch (OperationCanceledException) { return; }
        if (cts.IsCancellationRequested || !ReferenceEquals(_pending, cts)) return;
        _pending = null;
        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        var k = key();
        if (string.IsNullOrEmpty(k)) return;
        try { await prefs().SaveAsync(scope, k, snapshot()); }
        catch { /* best-effort */ }
    }
}
