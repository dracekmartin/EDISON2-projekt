using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EDControls.UserPrefs;
using EDISONMAIN2.Desktop;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Per-user × IDFa stav stromu modulů (oblíbené + používanost) přes <see cref="IUserPrefsService"/>.
    /// Scope userdata blobů = "EDISONMAIN2".
    /// IdFa izolace je automatická - server klíčuje z sys kontextu MLRest, instance se používá per session.
    /// Load i Save jsou async (nesmí blokovat UI thread - session rebuild jich dělá pět);
    /// chyby se logují a degradují na prázdno, persistence nesmí shodit UI.
    /// </summary>
    public class ModulePreferencesService
    {
        private const string Scope             = "EDISONMAIN2";
        private const string ShortcutsKey      = "Shortcuts";
        private const string UsageKey          = "ModuleUsage";
        private const string ExpansionKey      = "TreeExpansion";
        private const string SelectedTabKey    = "SelectedTab";
        private const string ChangelogSnoozeKey = "ChangelogSnooze";

        private readonly IUserPrefsService _prefs;

        public ModulePreferencesService(IUserPrefsService prefs) => _prefs = prefs;

        /// <summary>
        /// Položky plochy v pořadí připnutí (druh, identita, pozice). Chybějící nebo poškozený blob
        /// = prázdný list; <c>null</c> = load selhal. Rozdíl je podstatný: plocha se po selhání
        /// nesmí seedovat prázdnem, první další toggle by prázdno zapsal zpět a uživatel by o ni přišel.
        /// </summary>
        public async Task<List<DesktopShortcutDto>?> LoadShortcutsAsync()
        {
            try
            {
                var node = await _prefs.LoadAsync(Scope, ShortcutsKey);
                return node?.Deserialize<List<DesktopShortcutDto>>() ?? [];
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[ModulePrefs] Shortcuts blob corrupt: {ex.Message}");
                return [];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModulePrefs] Load failed ({ShortcutsKey}): {ex.Message}");
                return null;
            }
        }

        public Task SaveShortcutsAsync(IEnumerable<DesktopShortcutDto> shortcuts) =>
            SaveBlobAsync(ShortcutsKey, shortcuts.ToList());

        /// <summary>Používanost per ElementId (počet spuštění + čas posledního). Prázdná mapa při chybě.</summary>
        public async Task<Dictionary<string, UsageEntry>> LoadUsageAsync() =>
            await LoadBlobAsync<Dictionary<string, UsageEntry>>(UsageKey) ?? [];

        public Task SaveUsageAsync(IReadOnlyDictionary<string, UsageEntry> usage) =>
            SaveBlobAsync(UsageKey, usage);

        /// <summary>
        /// Klíče rozbalených uzlů stromu (index-cesta per strom, viz EdisonMainViewModel).
        /// Vrací <c>null</c> když nebylo nikdy uloženo → caller ponechá serverové výchozí rozbalení.
        /// </summary>
        public Task<List<string>?> LoadExpansionAsync() => LoadBlobAsync<List<string>>(ExpansionKey);

        public Task SaveExpansionAsync(IEnumerable<string> expandedKeys) =>
            SaveBlobAsync(ExpansionKey, expandedKeys.ToList());

        /// <summary>Odložené novinky: klíč INF souboru ("AD", "EDISONMAIN") → hash odloženého change-setu.</summary>
        public async Task<Dictionary<string, string>> LoadChangelogSnoozeAsync() =>
            await LoadBlobAsync<Dictionary<string, string>>(ChangelogSnoozeKey) ?? [];

        public Task SaveChangelogSnoozeAsync(IReadOnlyDictionary<string, string> snooze) =>
            SaveBlobAsync(ChangelogSnoozeKey, snooze);

        /// <summary>
        /// Klíč naposledy otevřené záložky stromu (<see cref="TreeTab.Key"/>), nebo <c>null</c>
        /// když nebylo uloženo. Dřív se ukládal index - starou číselnou hodnotu shodí
        /// <c>GetValue&lt;string&gt;</c> a catch ji degraduje na null (= výchozí záložka).
        /// </summary>
        public async Task<string?> LoadSelectedTabAsync()
        {
            try
            {
                var node = await _prefs.LoadAsync(Scope, SelectedTabKey);
                return node?.GetValue<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModulePrefs] LoadSelectedTab failed: {ex.Message}");
                return null;
            }
        }

        public Task SaveSelectedTabAsync(string tabKey) => SaveBlobAsync(SelectedTabKey, tabKey);

        private async Task<T?> LoadBlobAsync<T>(string key) where T : class
        {
            try
            {
                var node = await _prefs.LoadAsync(Scope, key);
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

    /// <summary>Záznam používanosti modulu - počet spuštění + čas posledního (UTC). Defaults pro backward-compat.</summary>
    public record UsageEntry(int Count = 0, DateTime LastUsedUtc = default);
}
