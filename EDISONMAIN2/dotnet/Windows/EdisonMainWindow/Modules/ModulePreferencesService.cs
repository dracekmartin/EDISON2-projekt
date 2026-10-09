using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        // Fronta patří úložišti, ne instanci: VM staví instanci znovu při každé přestavbě session nad týmiž UserPrefs.
        private static readonly ConditionalWeakTable<IUserPrefsService, Dictionary<string, PendingWrite?>> Queues = new();

        private readonly IUserPrefsService _prefs;
        // Klíč = zápis v letu, hodnota = čekající zápis (null = žádný).
        private readonly Dictionary<string, PendingWrite?> _pending;

        public ModulePreferencesService(IUserPrefsService prefs)
        {
            _prefs = prefs;
            _pending = Queues.GetValue(prefs, _ => []);
        }

        // MLRest čeká na odpověď hodiny: visící zápis by bez limitu zablokoval klíč do konce session.
        internal TimeSpan InFlightLimit { get; init; } = TimeSpan.FromSeconds(30);

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

        /// <summary>Používanost per <see cref="ExecutableModule.VariantKey"/> (počet spuštění + čas posledního). Prázdná mapa při chybě.</summary>
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

        // Zápis jednoho klíče v letu: každý jde na klonu v jiném Caché procesu, souběžné by neměly pořadí.
        // Čekající hodnotu nahradí novější, task volajícího skončí zápisem jeho nebo novější hodnoty.
        private Task SaveBlobAsync<T>(string key, T value)
        {
            JsonNode? node;
            try
            {
                node = JsonSerializer.SerializeToNode(value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModulePrefs] Save failed ({key}): {ex.Message}");
                return Task.CompletedTask;
            }
            if (node is null) return Task.CompletedTask;

            PendingWrite write;
            lock (_pending)
            {
                if (_pending.TryGetValue(key, out var waiting))
                {
                    _pending[key] = write = new PendingWrite(node, waiting?.Done ?? NewDone());
                    return write.Done.Task;
                }
                _pending[key] = null;
            }
            write = new PendingWrite(node, NewDone());
            _ = WriteInOrderAsync(key, write);
            return write.Done.Task;
        }

        private async Task WriteInOrderAsync(string key, PendingWrite write)
        {
            while (true)
            {
                try
                {
                    // Bez UI vlákna: při zavření shellu už nepumpuje a čekající zápis by neodešel.
                    await _prefs.SaveAsync(Scope, key, write.Node).WaitAsync(InFlightLimit).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ModulePrefs] Save failed ({key}): {ex.Message}");
                }
                write.Done.TrySetResult();

                lock (_pending)
                {
                    if (_pending[key] is not { } next)
                    {
                        _pending.Remove(key);
                        return;
                    }
                    write = next;
                    _pending[key] = null;
                }
            }
        }

        private static TaskCompletionSource NewDone() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed record PendingWrite(JsonNode Node, TaskCompletionSource Done);
    }

    /// <summary>Záznam používanosti modulu - počet spuštění + čas posledního (UTC). Defaults pro backward-compat.</summary>
    public record UsageEntry(int Count = 0, DateTime LastUsedUtc = default);
}
