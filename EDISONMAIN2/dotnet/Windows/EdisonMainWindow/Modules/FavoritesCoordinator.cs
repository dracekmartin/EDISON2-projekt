using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDControls.EDViewModel;
using EDISONMAIN2.Desktop;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Plocha a záložka Oblíbené nad sdílenou <see cref="ModuleShortcutsService"/>: seed po session
    /// loadu, přenačtení po exitu agendy, ★ ve stromu, přestavba Oblíbených a persistence plochy.
    /// Spouštění a novinky z dlaždice obsluhuje VM (vedou na jeho commandy).
    /// </summary>
    internal sealed class FavoritesCoordinator
    {
        private readonly ModuleShortcutsService _shortcuts;
        private readonly ModuleTreeState _trees;
        // Per-user × IDFa preference se re-vytváří v každém session rebuildu a do prvního jsou null.
        private readonly Func<ModulePreferencesService?> _prefs;
        private readonly IWindowNavigator _navigator;
        private readonly Action _favoritesRebuilt;

        internal FavoritesCoordinator(ModuleShortcutsService shortcuts, ModuleTreeState trees,
            Func<ModulePreferencesService?> prefs, IWindowNavigator navigator, Action favoritesRebuilt)
        {
            _shortcuts = shortcuts;
            _trees = trees;
            _prefs = prefs;
            _navigator = navigator;
            _favoritesRebuilt = favoritesRebuilt;

            // Změny plochy ze stromu i z miniaplikace (★, DnD add, remove) → přestav Oblíbené + persist.
            _shortcuts.MembershipChanged += SyncFavorites;
            // Přesun dlaždice (reorder v miniaplikaci) → persistuj pozice (bez změny členství).
            _shortcuts.PositionsChanged += PersistShortcuts;
            _navigator.LegacyModuleExited += OnLegacyModuleExited;
        }

        /// <summary>Odhlásí odběry - při zavření shellu se zabíjejí agendy a každá by jinak spustila reload plochy.</summary>
        internal void Detach()
        {
            _shortcuts.MembershipChanged -= SyncFavorites;
            _shortcuts.PositionsChanged -= PersistShortcuts;
            _navigator.LegacyModuleExited -= OnLegacyModuleExited;
        }

        /// <summary>
        /// Null = load plochy selhal: zobrazit prázdno (data předchozí session sem nepatří), ale
        /// nepersistovat, dokud se blob nenačte - první toggle by jinak přepsal uživatelovu plochu.
        /// </summary>
        internal void Apply(List<DesktopShortcutDto>? stored)
        {
            Seed(stored);
            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
        }

        // Agenda mohla přidat zástupce na plochu (píše přímo do Caché) - po jejím ukončení
        // si blob přečteme znovu, jinak by dlaždice naskočila až po restartu shellu.
        private void OnLegacyModuleExited() => _ = ReloadAsync();

        /// <summary>
        /// Přenačte plochu z Caché. Agendy zapisují zástupce přímo na server (nemají na shell
        /// spojení), takže po jejich ukončení může mít blob víc, než co drží tenhle shell.
        /// Ticho při chybě je záměr - kvůli nedostupnému serveru nemá smysl obtěžovat uživatele,
        /// který zrovna zavřel agendu.
        /// </summary>
        private async Task ReloadAsync()
        {
            if (_prefs() is not { } prefs) return;
            var stored = await prefs.LoadShortcutsAsync();
            // Selhání loadu: v paměti zůstává stav téže session (agenda mohla přidat, nikdy neubírá);
            // seed prázdnem by ho zahodil a další toggle prázdno zapsal do Caché.
            if (stored is null) return;
            Apply(stored);
        }

        private void Seed(List<DesktopShortcutDto>? stored)
        {
            _shortcuts.Seed(_trees.AllExecutables(), stored);
        }

        /// <summary>
        /// Přepnutí oblíbeného přes sdílenou službu (stejný zdroj jako miniaplikace Plocha). EXE =
        /// toggle jednoho; FAMILY = hromadně (přidá vše, pokud nejsou všechny oblíbené; jinak vše odebere).
        /// Služba ohlásí změnu členství (rodina jednou) a <see cref="SyncFavorites"/> přestaví strom + uloží.
        /// </summary>
        internal void Toggle(IModule module)
        {
            switch (module)
            {
                case ExecutableModule { ElementId.Length: > 0 } exe:
                    if (_shortcuts.Contains(exe)) _shortcuts.Remove(exe);
                    else _shortcuts.Add(exe);
                    break;

                case ModuleFamily family:
                    var exes = ModuleTreeState.Flatten(family).Where(e => e.ElementId.Length > 0).ToList();
                    if (exes.Count == 0) return;
                    bool allFav = exes.All(_shortcuts.Contains);
                    _shortcuts.Batch(() =>
                    {
                        foreach (var e in exes)
                            if (allFav) _shortcuts.Remove(e);
                            else _shortcuts.Add(e);
                    });
                    break;
            }
        }

        // Přestaví záložku Oblíbené + agregační flagy rodin a uloží plochu. Volá se po každé změně
        // členství (tree toggle i změny z miniaplikace).
        private void SyncFavorites()
        {
            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
            PersistShortcuts();
        }

        // Uloží plochu (druh, identita, pozice). Volá se při změně členství i při přesunu dlaždice.
        private void PersistShortcuts()
        {
            if (!_shortcuts.IsLoaded) return;
            _ = _prefs()?.SaveShortcutsAsync(_shortcuts.Persistable());
        }

        private void RebuildFavoritesTree()
        {
            _trees.ReplaceFavorites(BuildFavoritesTree(_trees.SourceTrees.Select(t => t.Tree), FavoriteIdSet()));
            _favoritesRebuilt();
        }

        // Oblíbené = položky plochy, které jsou modulem hlavní nabídky (★ ve stromu jiné druhy nezná).
        private HashSet<string> FavoriteIdSet() =>
            [.. _shortcuts.Items.Where(i => i.Kind == ShortcutKind.MenuModule).Select(i => i.VariantKey)];

        // Nastaví ModuleFamily.IsFavorite = "všichni potomci oblíbení" napříč hlavními stromy (řídí text
        // kontextového menu rodiny). Vrací, zda je uzel plně oblíbený (rekurzivní agregace).
        private void RefreshFamilyFavoriteFlags()
        {
            foreach (var (_, tree) in _trees.SourceTrees)
                foreach (var node in tree)
                    UpdateFamilyFavorite(node);
        }

        private static bool UpdateFamilyFavorite(IModule module)
        {
            switch (module)
            {
                case ExecutableModule exe:
                    return exe.IsFavorite;
                case ModuleFamily family:
                    bool any = false, all = true;
                    foreach (var child in family.NestedModules)
                    {
                        any = true;
                        if (!UpdateFamilyFavorite(child)) all = false;
                    }
                    family.IsFavorite = any && all;
                    return family.IsFavorite;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Ořezané stromy jen s oblíbenými listy (predikát <c>favoriteIds.Contains(VariantKey)</c>).
        /// Zachovává hierarchii, prázdné rodiny se vynechají. Listy se SDÍLEJÍ (ne kopírují) s hlavními
        /// stromy - přepnutí IsFavorite se projeví v obou. Rodiny jsou nové (rozbalené pro přehled).
        /// </summary>
        internal static List<IModule> BuildFavoritesTree(
            IEnumerable<IEnumerable<IModule>> sourceTrees, ISet<string> favoriteIds)
        {
            var roots = new List<IModule>();
            foreach (var tree in sourceTrees)
                foreach (var node in tree)
                    if (PruneToFavorites(node, favoriteIds) is { } kept)
                        roots.Add(kept);
            return roots;
        }

        private static IModule? PruneToFavorites(IModule module, ISet<string> favoriteIds) => module switch
        {
            ExecutableModule exe => favoriteIds.Contains(exe.VariantKey) ? exe : null,
            ModuleFamily family  => PruneFamily(family, favoriteIds),
            _                    => null,
        };

        private static ModuleFamily? PruneFamily(ModuleFamily family, ISet<string> favoriteIds)
        {
            var kept = family.NestedModules
                .Select(child => PruneToFavorites(child, favoriteIds))
                .Where(child => child != null)
                .ToList();

            if (kept.Count == 0) return null;

            // V záložce Oblíbené rodina zobrazuje jen oblíbené potomky → je „plně oblíbená" (řídí text menu).
            var pruned = new ModuleFamily
            {
                Name = family.Name, DisplaySuffix = family.DisplaySuffix, IsExpanded = true, Color = family.Color, IsFavorite = true,
            };
            foreach (var child in kept) pruned.NestedModules.Add(child!);
            return pruned;
        }
    }
}
