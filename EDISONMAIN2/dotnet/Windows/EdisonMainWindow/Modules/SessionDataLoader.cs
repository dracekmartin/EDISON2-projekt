using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.UserProfileWindow;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Session-scoped data ze serveru, ještě nepromítnutá do VM: surové (nefiltrované) stromy
    /// per jméno + aplikační přístup + per-user × IDFa preference + výchozí okno.
    /// </summary>
    public record SessionData(
        AccessTable AccessTable,
        IReadOnlyDictionary<string, IReadOnlyList<IModule>> Trees,
        List<DesktopShortcutDto> Shortcuts,
        Dictionary<string, UsageEntry> Usage,
        List<string>? Expansion,
        string? SelectedTab,
        Dictionary<string, string> ChangelogSnooze,
        int DefaultWindowId);

    public interface ISessionDataLoader
    {
        Task<SessionData> LoadAsync(Lic? licence, bool isAdmin, ModulePreferencesService prefs);
    }

    /// <summary>
    /// Jediné místo, kde session rebuild sahá na síť. Devět round-tripů jede na jeden zátah
    /// (~4 sekvenční latence), protože na hotových stromech závisí až *aplikace* dat ve VM,
    /// ne jejich načtení. Nic tady nemutuje stav VM — selhání kterékoli větve tedy nechá
    /// předchozí stav shellu nedotčený.
    /// </summary>
    public sealed class SessionDataLoader(
        IRestCallRunner runner,
        IModuleTreeGetter treeGetter,
        IReadOnlyList<string> treeNames) : ISessionDataLoader
    {
        public async Task<SessionData> LoadAsync(Lic? licence, bool isAdmin, ModulePreferencesService prefs)
        {
            var access = runner.RunAsync(rest => AccessLoader.Load(rest, licence, isAdmin));
            var trees = treeNames
                .Select(name => (Name: name, Task: runner.RunAsync(rest => treeGetter.GetModuleTree(rest, name))))
                .ToList();
            var defaultWindow = runner.RunAsync(UserProfileService.LoadDefaultWindow);
            var preferences = LoadPreferencesAsync(prefs);

            // WhenAll (ne postupné awaity) — dokončí všechny větve a označí jejich výjimky za
            // observované, takže selhání jedné nezůstane jako UnobservedTaskException.
            await Task.WhenAll([access, defaultWindow, preferences, .. trees.Select(t => (Task)t.Task)]);

            var (shortcuts, usage, expansion, selectedTab, changelogSnooze) = preferences.Result;
            return new SessionData(
                access.Result,
                trees.ToDictionary(t => t.Name, t => t.Task.Result),
                shortcuts, usage, expansion, selectedTab, changelogSnooze,
                defaultWindow.Result);
        }

        // Preference jdou sekvenčně v jedné větvi: IUserPrefsService drží sdílenou instanci MLRest
        // z composition rootu, takže jim runner nemá kam podstrčit klon a souběžně by si praly
        // RestStatus. Paralelizace by chtěla aditivní přetížení v EDControls (kandidát na vlnu 4).
        private static async Task<(List<DesktopShortcutDto>, Dictionary<string, UsageEntry>, List<string>?, string?, Dictionary<string, string>)>
            LoadPreferencesAsync(ModulePreferencesService prefs)
        {
            var shortcuts = await prefs.LoadShortcutsAsync();
            var usage = await prefs.LoadUsageAsync();
            var expansion = await prefs.LoadExpansionAsync();
            var selectedTab = await prefs.LoadSelectedTabAsync();
            var changelogSnooze = await prefs.LoadChangelogSnoozeAsync();
            return (shortcuts, usage, expansion, selectedTab, changelogSnooze);
        }
    }
}
