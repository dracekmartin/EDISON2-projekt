using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EDControls.UserPrefs;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Per-user × IDFa stav stromu modulů (oblíbené + používanost) přes <see cref="IUserPrefsService"/>.
    /// Schémata blobů a registr scope viz <c>EDControls/DOCS/05-userdata-scopes.md</c> (scope "EDISONMAIN2").
    /// IdFa izolace je automatická — server klíčuje z sys kontextu MLRest, instance se používá per session.
    /// Load je sync (blokuje krátký HTTP call — konzistentní s ModuleTreeGetter v témže rebuildu);
    /// Save je fire-and-forget, chyby se logují (persistence nesmí shodit UI).
    /// </summary>
    public class ModulePreferencesService
    {
        private const string Scope        = "EDISONMAIN2";
        private const string FavoritesKey = "Favorites";
        private const string UsageKey     = "ModuleUsage";

        private readonly IUserPrefsService _prefs;

        public ModulePreferencesService(IUserPrefsService prefs) => _prefs = prefs;

        /// <summary>ElementId oblíbených modulů v pořadí připnutí. Prázdný list při chybě / chybějícím blobu.</summary>
        public List<string> LoadFavorites() => LoadBlob<List<string>>(FavoritesKey) ?? [];

        /// <summary>Používanost per ElementId (počet spuštění + čas posledního). Prázdná mapa při chybě.</summary>
        public Dictionary<string, UsageEntry> LoadUsage() =>
            LoadBlob<Dictionary<string, UsageEntry>>(UsageKey) ?? [];

        public Task SaveFavoritesAsync(IEnumerable<string> elementIds) =>
            SaveBlobAsync(FavoritesKey, elementIds.ToList());

        public Task SaveUsageAsync(IReadOnlyDictionary<string, UsageEntry> usage) =>
            SaveBlobAsync(UsageKey, usage);

        private T? LoadBlob<T>(string key) where T : class
        {
            try
            {
                var node = _prefs.LoadAsync(Scope, key).GetAwaiter().GetResult();
                return node?.Deserialize<T>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModulePrefs] Load failed ({key}): {ex.Message}");
                return null;
            }
        }

        private async Task SaveBlobAsync<T>(string key, T value)
        {
            try
            {
                var node = JsonSerializer.SerializeToNode(value);
                if (node is null) return;
                await _prefs.SaveAsync(Scope, key, node);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModulePrefs] Save failed ({key}): {ex.Message}");
            }
        }
    }

    /// <summary>Záznam používanosti modulu — počet spuštění + čas posledního (UTC). Defaults pro backward-compat.</summary>
    public record UsageEntry(int Count = 0, DateTime LastUsedUtc = default);
}
