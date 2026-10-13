using System.Collections.Concurrent;

namespace EDControls.SessionState;

/// <summary>
/// Výchozí implementace <see cref="ISessionState"/> - slovník v paměti procesu. Není co
/// konfigurovat ani co selhat (žádný transport), proto na rozdíl od UserPrefs nemá no-op
/// variantu a je rovnou defaultem v <c>EDServices</c>.
/// </summary>
internal sealed class InMemorySessionState : ISessionState
{
    private readonly ConcurrentDictionary<(string Scope, string Key), string> _values = new();

    public string? Get(string scope, string key) =>
        _values.TryGetValue((scope, key), out var value) ? value : null;

    public void Set(string scope, string key, string? value)
    {
        if (value is null) _values.TryRemove((scope, key), out _);
        else _values[(scope, key)] = value;
    }
}
