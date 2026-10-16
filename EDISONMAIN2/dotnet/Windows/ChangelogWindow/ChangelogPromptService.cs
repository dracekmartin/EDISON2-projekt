using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    /// <summary>
    /// Rozhoduje o auto-popupu novinek: nové změny (existence _DIFF) × snooze (hash change-setu)
    /// × max jednou za session. Instance žije per session rebuild - reset dedupu při přepnutí
    /// firmy/uživatele je záměr (snooze je per-user × IDFa).
    /// </summary>
    public class ChangelogPromptService(
        IChangelogSource source,
        ModulePreferencesService prefs,
        Action<string, ChangelogContent, bool, Action<bool>> showWindow)
    {
        // Klíč = ChangeSetKey: dvě agendy téhož balíku (i re-focus přes ShowLegacyAsync dedup) = 1 popup.
        // Vědomá odchylka od legacy - ten neodložené novinky ukazoval při každém otevření agendy.
        private readonly HashSet<string> _shownThisSession = [];
        private Dictionary<string, string> _snooze = [];

        /// <summary>Načtený snooze stav ze session dat (ApplySessionData).</summary>
        public void SeedSnooze(Dictionary<string, string> snooze) => _snooze = snooze;

        /// <summary>
        /// Zobrazí novinky modulu, pokud existují a nejsou odložené. Nikdy nevyhazuje -
        /// changelog nesmí rozbít otevírání agend ani start shellu.
        /// </summary>
        public async Task ShowIfNewAsync(string moduleKey, string displayName)
        {
            try
            {
                var content = await source.LoadAsync(moduleKey);
                if (content?.ChangesText is not { Length: > 0 }) return;
                if (!_shownThisSession.Add(content.ChangeSetKey)) return;
                if (_snooze.TryGetValue(content.ChangeSetKey, out var snoozedId)
                    && snoozedId == content.ChangeSetId) return;

                showWindow(displayName, content, false, snoozed => ApplySnooze(content, snoozed));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Changelog] {moduleKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// Novinky na vyžádání (menu, kontextová menu): obchází snooze i session dedup - obsah
        /// leží na disku a uživatel si o něj řekl. Chybějící _DIFF není důvod nic neukázat,
        /// okno padne na celou historii. Vrací <c>false</c>, když modul žádný INF nemá
        /// (volající to musí uživateli říct, jinak klik nic neudělá).
        /// </summary>
        public async Task<bool> ShowAlwaysAsync(string moduleKey, string displayName)
        {
            try
            {
                var content = await source.LoadAsync(moduleKey);
                if (content is null) return false;

                var snoozed = content.ChangeSetId.Length > 0
                              && _snooze.TryGetValue(content.ChangeSetKey, out var id)
                              && id == content.ChangeSetId;
                showWindow(displayName, content, snoozed, s => ApplySnooze(content, s));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Changelog] {moduleKey}: {ex.Message}");
                return false;
            }
        }

        // Checkbox okna je dvoucestný: odškrtnutí ve vynuceném zobrazení odložení ruší. Okno bez
        // _DIFF (prázdný ChangeSetId) má checkbox zakázaný, takže do snooze nesmí sáhnout ani omylem.
        private void ApplySnooze(ChangelogContent content, bool snoozed)
        {
            if (content.ChangeSetId.Length == 0) return;
            if (snoozed) _snooze[content.ChangeSetKey] = content.ChangeSetId;
            else if (!_snooze.Remove(content.ChangeSetKey)) return;

            // Fire-and-forget jako ostatní preference saves - persistence nesmí blokovat UI.
            _ = prefs.SaveChangelogSnoozeAsync(_snooze);
        }
    }
}
