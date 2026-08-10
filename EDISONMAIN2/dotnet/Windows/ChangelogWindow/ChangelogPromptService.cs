using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

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
        Action<string, ChangelogContent, Action<bool>> showWindow)
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

                showWindow(displayName, content, snoozed =>
                {
                    if (!snoozed) return;
                    _snooze[content.ChangeSetKey] = content.ChangeSetId;
                    // Fire-and-forget jako ostatní preference saves - persistence nesmí blokovat UI.
                    _ = prefs.SaveChangelogSnoozeAsync(_snooze);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Changelog] {moduleKey}: {ex.Message}");
            }
        }
    }
}
