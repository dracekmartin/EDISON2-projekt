using System.Text.Json.Nodes;

namespace EDControls.UserPrefs;

/// <summary>
/// No-op fallback impl - používá se když Caché není dostupné (standalone testy, design-time,
/// chybějící MLRest). Load vrátí null (žádný state), Save / Delete tiše ignorují.
///
/// Konzumenti by neměli rozlišovat - používají IUserPrefsService stejně bez ohledu na konkrétní impl.
/// </summary>
public sealed class NullUserPrefsService : IUserPrefsService
{
    public Task<JsonNode?> LoadAsync(string scope, string key) => Task.FromResult<JsonNode?>(null);
    public Task SaveAsync(string scope, string key, JsonNode data) => Task.CompletedTask;
    public Task DeleteAsync(string scope, string key) => Task.CompletedTask;
}
