using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Stromy launcher panelu: záložky, plnění s licenční a přístupovou filtrací, hledání
    /// a persistence rozbalení. Statické členy jsou čisté algoritmy nad stromem bez stavu shellu.
    /// </summary>
    internal sealed class ModuleTreeState
    {
        /// <summary>Per stanice konstantní (DNS doména / registry override) - stačí vyhodnotit jednou.</summary>
        internal static readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();

        /// <summary>
        /// Filtrace stromu: licence (<see cref="ExecutableModule.IsLicensed"/> ∈ <see cref="Lic.Elements"/>),
        /// <see cref="ExecutableModule.Hidden"/>, mlineOnly a aplikační přístup (<c>AccessTable</c>);
        /// admin fallback řeší už <see cref="AccessLoader"/> při loadu.
        /// Gate platí i na FAMILY (package ID) - složka mimo licenci/AP/doménu skryje celou větev,
        /// parita s legacy <c>BuildTreeXML</c>; prázdná FAMILY se navíc auto-skryje.
        /// </summary>
        internal static IModule? TryFilter(IModule module, Lic? licence, AccessTable? access = null, bool isMlineDomain = false) => module switch
        {
            ExecutableModule exe => IsAllowed(exe, licence, access, isMlineDomain)
                ? (exe.MlineOnly ? new ExecutableModule(exe) { DisplaySuffix = exe.DisplaySuffix + MlineDomainDetector.Suffix } : exe)
                : null,
            ModuleFamily family  => FilterFamily(family, licence, access, isMlineDomain),
            _                    => module,
        };

        internal static bool IsAllowed(ExecutableModule exe, Lic? licence, AccessTable? access = null, bool isMlineDomain = false)
        {
            if (exe.Hidden) return false;
            if (exe.MlineOnly && !isMlineDomain) return false;
            if (exe.IsLicensed && !(licence?.Elements?.Contains(exe.ElementId) ?? false))
                return false;
            // AccessTable null = load nebyl proveden (typicky testy) → pass-through (graceful degradation).
            // ElementId "" = legacy 4-field řádek bez ID → bez AP gate (parita s FAMILY větví).
            if (access != null && exe.ElementId.Length > 0 && !access.Contains(exe.ElementId))
                return false;
            return true;
        }

        internal static ModuleFamily? FilterFamily(ModuleFamily family, Lic? licence, AccessTable? access = null, bool isMlineDomain = false)
        {
            // Gate složky PŘED rekurzí - mimo licenci/AP/doménu je skrytá celá větev včetně
            // jinak povolených dětí (např. admin fallback přidá formTMList, ale bez AP
            // na package Kmenová evidence se nezobrazí - parita s legacy BuildTreeXML).
            if (family.MlineOnly && !isMlineDomain) return null;
            if (family.IsLicensed && !(licence?.Elements?.Contains(family.ElementId) ?? false))
                return null;
            // ElementId "" = syntetická složka bez gate; access null = pass-through (testy).
            if (access != null && family.ElementId.Length > 0 && !access.Contains(family.ElementId))
                return null;

            var visibleChildren = family.NestedModules
                .Select(child => TryFilter(child, licence, access, isMlineDomain))
                .Where(child => child != null)
                .ToList();

            if (visibleChildren.Count == 0) return null;

            var filtered = new ModuleFamily
            {
                Name          = family.Name,
                DisplaySuffix = family.DisplaySuffix + (family.MlineOnly ? MlineDomainDetector.Suffix : ""),
                IsExpanded    = family.IsExpanded,
                ElementId     = family.ElementId,
                IsLicensed    = family.IsLicensed,
                MlineOnly     = family.MlineOnly,
            };
            foreach (var child in visibleChildren) filtered.NestedModules.Add(child!);
            return filtered;
        }

        /// <summary>
        /// Nastaví <see cref="IModule.IsVisibleBySearch"/> rekurzivně. Uzel je viditelný, když jeho
        /// název obsahuje <paramref name="text"/> (case + diakritika insensitive) NEBO má viditelného
        /// potomka (prázdný text = vše viditelné). Rodina nalezená podle vlastního názvu zpřístupní
        /// celý svůj obsah; rodina s nalezeným potomkem se při aktivním hledání rozbalí.
        /// Vrací, zda je modul viditelný.
        /// </summary>
        internal static bool ApplySearchToModule(IModule module, string text)
        {
            var needle = SearchNormalization.NormalizeForSearch(text);
            bool selfMatch = needle.Length == 0
                || SearchNormalization.NormalizeForSearch(module.Name).Contains(needle);

            if (module is ModuleFamily family)
            {
                // Shoda na názvu skupiny zpřístupní celý její obsah - potomci hledané slovo v názvu
                // nemají, takže by nález byl slepá ulička (skupina prázdná a spustit ji nejde).
                // Prázdný needle v rekurzi = "všechno viditelné", proto stačí předat "".
                bool wholeSubtree = selfMatch && needle.Length > 0;
                bool anyChildVisible = false;
                foreach (var child in family.NestedModules)
                    if (ApplySearchToModule(child, wholeSubtree ? "" : text)) anyChildVisible = true;
                selfMatch = selfMatch || anyChildVisible;
                if (text.Length > 0 && anyChildVisible)
                {
                    if (wholeSubtree) SetExpandedRecursive(family, true);
                    else family.IsExpanded = true;
                }
            }

            module.IsVisibleBySearch = selfMatch;
            return selfMatch;
        }

        /// <summary>
        /// První spustitelný modul v pořadí stromu, který přežil hledání (DFS). Do skryté rodiny
        /// se nesestupuje - její potomci mají <see cref="IModule.IsVisibleBySearch"/> false také.
        /// </summary>
        internal static ExecutableModule? FirstVisibleExecutable(IEnumerable<IModule> tree)
        {
            foreach (var module in tree)
            {
                if (!module.IsVisibleBySearch) continue;
                switch (module)
                {
                    case ExecutableModule exe:
                        return exe;
                    case ModuleFamily family when FirstVisibleExecutable(family.NestedModules) is { } hit:
                        return hit;
                }
            }
            return null;
        }

        // Klíč uzlu = "<tree>:<index-cesta>" (např. "main:5.2") - stabilní vůči přejmenování,
        // na rozdíl od cesty přes názvy.

        internal static void SetExpandedRecursive(IModule module, bool expanded)
        {
            if (module is not ModuleFamily family) return;
            family.IsExpanded = expanded;
            foreach (var child in family.NestedModules)
                SetExpandedRecursive(child, expanded);
        }

        internal static void CollectExpanded(IList<IModule> tree, string treeName, List<string> keys)
        {
            for (int i = 0; i < tree.Count; i++)
                CollectExpandedNode(tree[i], treeName, [i], keys);
        }

        private static void CollectExpandedNode(IModule module, string treeName, List<int> path, List<string> keys)
        {
            if (module is not ModuleFamily family) return;
            if (family.IsExpanded) keys.Add(ExpansionPathKey(treeName, path));
            for (int i = 0; i < family.NestedModules.Count; i++)
                CollectExpandedNode(family.NestedModules[i], treeName, [.. path, i], keys);
        }

        internal static string ExpansionPathKey(string treeName, IReadOnlyList<int> path) =>
            treeName + ":" + string.Join(".", path);

        /// <summary>
        /// Mapa <c>exe|forma|param</c> → popisky modulu. Klíč je záměrně tentýž, kterým
        /// <c>WindowNavigator</c> deduplikuje běžící procesy.
        /// </summary>
        internal static Dictionary<string, ModuleLabel> BuildModuleLabels(IEnumerable<IEnumerable<IModule>> trees)
        {
            var labels = new Dictionary<string, ModuleLabel>();
            // První nález vyhrává - týž modul může být ve stromě víckrát (např. i v Oblíbených).
            foreach (var exe in trees.SelectMany(tree => tree).SelectMany(Flatten))
                labels.TryAdd(ModuleKey(exe.ExecutablePath, exe.FormName, exe.InputParam),
                    new ModuleLabel(exe.TreePath, exe.LongName));
            return labels;
        }

        internal static string ModuleKey(string exe, string form, string inputParam) => $"{exe}|{form}|{inputParam}";

        /// <summary>
        /// Popisky modulu ve stromu: <c>Path</c> = cesta agendou včetně listu
        /// („Mzdy a personalistika › Zaměstnanci"), <c>LongName</c> = legacy <c>nameEx</c>
        /// (může být prázdný - má ho jen pětina modulů).
        /// </summary>
        internal record ModuleLabel(string Path, string LongName);

        internal static IEnumerable<ExecutableModule> Flatten(IModule module) => module switch
        {
            ExecutableModule exe => [exe],
            ModuleFamily family  => family.NestedModules.SelectMany(Flatten),
            _                    => [],
        };

        // Per-user × IDFa preference se re-vytváří v každém session rebuildu a do prvního jsou null.
        private readonly Func<ModulePreferencesService?> _prefs;
        private readonly ModuleTreeStation _station;

        internal ModuleTreeState(Func<ModulePreferencesService?> prefs, ModuleTreeStation? station = null)
        {
            _prefs = prefs;
            _station = station ?? ModuleTreeStation.Production;
            // Pořadí = pořadí záložek v UI. Přidání záložky je změna jen tady (+ případný control v XAML).
            TreeTabs =
            [
                new("main",      "Hlavní nabídka",     "EDIcons.Home",      MainMenuTree,         Expandable: true,  ServerLoaded: true),
                new("system",    "Správa systému",     "EDIcons.Gears",     SystemAdminTree,      Expandable: true,  ServerLoaded: true),
                new("partners",  "Partnerská řešení",  "EDIcons.Component", PartnerSolutionsTree, Expandable: true,  ServerLoaded: true),
                new("favorites", "Oblíbené",           "EDIcons.Star",      FavoritesTree,        Expandable: true,  ServerLoaded: false),
                new("used",      "Používané",          "EDIcons.History",   null,                 Expandable: false, ServerLoaded: false),
            ];
            SourceTrees = [.. TreeTabs.Where(t => t.ServerLoaded).Select(t => (t.Key, t.Tree!))];
        }

        internal ObservableCollection<IModule> MainMenuTree { get; } = [];
        internal ObservableCollection<IModule> SystemAdminTree { get; } = [];
        internal ObservableCollection<IModule> PartnerSolutionsTree { get; } = [];
        /// <summary>Ořezaný strom jen s oblíbenými - plní ho <see cref="ReplaceFavorites"/>.</summary>
        internal ObservableCollection<IModule> FavoritesTree { get; } = [];

        /// <summary>
        /// Pořadí i identita záložek launcher panelu - jediný seznam, ze kterého se odvozuje
        /// tab strip, viditelnost obsahu, cíl Rozbalit/Sbalit vše i uložená volba.
        /// </summary>
        internal IReadOnlyList<TreeTab> TreeTabs { get; }

        /// <summary>Stromy plněné ze serveru (per IDFa), na nich stojí oblíbené, používané i family flagy.</summary>
        internal IReadOnlyList<(string Name, ObservableCollection<IModule> Tree)> SourceTrees { get; }

        // Všechny rozbalovatelné stromy = zdrojové + odvozený Oblíbené (persistence rozbalení i nad Oblíbenými).
        private IEnumerable<(string Name, ObservableCollection<IModule> Tree)> ExpandableTrees =>
            TreeTabs.Where(t => t.Expandable).Select(t => (t.Key, t.Tree!));

        internal TreeTab TabByKey(string? key) =>
            TreeTabs.FirstOrDefault(t => t.Key == key) ?? TreeTabs[0];

        // Oblíbené se nenačítají ze serveru, ale rozbalení se jim persistuje jako ostatním.
        private TreeTab FavoritesTab => TabByKey("favorites");

        /// <summary>
        /// Všechny EXE instance napříč zdrojovými stromy. Oblíbené i Používané sdílí tytéž instance,
        /// takže stačí projít zdrojové stromy (flip IsFavorite se projeví všude).
        /// </summary>
        internal IEnumerable<ExecutableModule> AllExecutables() =>
            SourceTrees.SelectMany(t => t.Tree).SelectMany(Flatten);

        /// <summary>
        /// Naplní zdrojové stromy daty session a převezme jejich uložené rozbalení; aplikuje ho až
        /// <see cref="ApplySavedExpansion"/>, po přestavbě Oblíbených.
        /// </summary>
        internal void Load(IReadOnlyDictionary<string, IReadOnlyList<IModule>> trees, Lic? licence,
                           AccessTable? access, List<string>? expansion)
        {
            foreach (var (name, tree) in SourceTrees)
                FillFilteredTree(tree, trees[name], licence, access);
            _expansion = expansion;
            // Stromy jsou nové instance s persistovaným rozbalením; trvající hledání si vezme čerstvý snímek.
            _expansionBeforeSearch = null;
        }

        // Clear až tady (po úspěšném loadu, ne před requestem) - selhání jinak nechá záložku
        // trvale prázdnou a při přepnutí firmy zahodí i strom, který byl platný.
        private void FillFilteredTree(ObservableCollection<IModule> target, IReadOnlyList<IModule> tree,
                                      Lic? licence, AccessTable? access)
        {
            target.Clear();
            foreach (var module in tree)
            {
                if (TryFilter(module, licence, access, _station.IsMlineDomain) is { } filtered)
                    target.Add(_station.Decorate(filtered));
            }
        }

        /// <summary>
        /// Nové kořeny záložky Oblíbené. Rodinné wrappery jsou nové instance, takže si znovu
        /// vezmou uložené rozbalení i trvající hledání (jinak by naskočily s IsVisibleBySearch=true).
        /// </summary>
        internal void ReplaceFavorites(IEnumerable<IModule> roots)
        {
            FavoritesTree.Clear();
            foreach (var root in roots)
                FavoritesTree.Add(root);

            using (SuppressExpansionSave())
            {
                ApplyExpansion(FavoritesTree, FavoritesTab.Key, _expansion);
                var text = _searchText.Trim();
                if (text.Length > 0)
                    foreach (var root in FavoritesTree)
                        ApplySearchToModule(root, text);
            }
        }

        // Save rozbalení běží na každou jeho změnu (IDFa je tehdy stabilní), proto switch/close
        // nepotřebují zvláštní obsluhu.

        // Klíče rozbalených uzlů (index-cesta). null = nikdy neuloženo → ponech serverové výchozí rozbalení.
        private List<string>? _expansion;
        // Snímek rozbalení před hledáním; non-null = stromy nesou rozbalení vynucené hledáním.
        private List<string>? _expansionBeforeSearch;
        private int _expansionSaveSuppressed;

        /// <summary>Obnoví uložené rozbalení a napojí jeho persistenci na všech stromech (vč. Oblíbených).</summary>
        internal void ApplySavedExpansion()
        {
            using (SuppressExpansionSave())
                ApplyTreeExpansion(_expansion);
        }

        private void ApplyTreeExpansion(List<string>? keys)
        {
            foreach (var (name, tree) in ExpandableTrees)
                ApplyExpansion(tree, name, keys);
        }

        /// <summary>
        /// Potlačí persistenci rozbalení po dobu programové změny stromu. Refcount, ne bool:
        /// cesty se vnořují (přestavba Oblíbených uvnitř session loadu, re-filtrace při hledání)
        /// a plochý reset na false by vnitřní blok odemkl dřív, než skončí ten vnější.
        /// </summary>
        internal IDisposable SuppressExpansionSave()
        {
            _expansionSaveSuppressed++;
            return new ExpansionSaveScope(this);
        }

        private sealed class ExpansionSaveScope(ModuleTreeState state) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                state._expansionSaveSuppressed--;
            }
        }

        /// <summary>
        /// Hromadné rozbalení/sbalení celého stromu. Suppress + jeden save na konci (jinak by každá
        /// rodina zvlášť triggerovala SaveExpansionAsync - vzor jako ApplySearch).
        /// </summary>
        internal void SetTreeExpanded(IEnumerable<IModule> tree, bool expanded)
        {
            using (SuppressExpansionSave())
            {
                foreach (var module in tree)
                    SetExpandedRecursive(module, expanded);
            }
            PersistExpansion();
        }

        /// <summary>Rozbalit/sbalit rodinu i její potomky (kontextové menu „úroveň").</summary>
        internal void SetSubtreeExpanded(IModule module, bool expanded)
        {
            if (module is not ModuleFamily) return;
            using (SuppressExpansionSave())
                SetExpandedRecursive(module, expanded);
            PersistExpansion();
        }

        private void ApplyExpansion(IList<IModule> tree, string treeName, List<string>? keys)
        {
            for (int i = 0; i < tree.Count; i++)
                ApplyExpansionNode(tree[i], treeName, [i], keys);
        }

        private void ApplyExpansionNode(IModule module, string treeName, List<int> path, List<string>? keys)
        {
            if (module is not ModuleFamily family) return;

            // keys == null → nikdy neuloženo, ponech serverové výchozí rozbalení.
            if (keys is { } saved)
                family.IsExpanded = saved.Contains(ExpansionPathKey(treeName, path));

            family.PropertyChanged -= OnFamilyPropertyChanged;
            family.PropertyChanged += OnFamilyPropertyChanged;

            for (int i = 0; i < family.NestedModules.Count; i++)
                ApplyExpansionNode(family.NestedModules[i], treeName, [.. path, i], keys);
        }

        private void OnFamilyPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModuleFamily.IsExpanded) && _expansionSaveSuppressed == 0)
                PersistExpansion();
        }

        // Během hledání je rozbalení přechodné (vynucené nálezy); po vymazání textu se obnoví snímek.
        // _expansion drží poslední uložený stav, aby přestavba Oblíbených nevracela rozbalení z loginu.
        private void PersistExpansion()
        {
            if (_expansionBeforeSearch != null) return;
            _expansion = CollectExpandedKeys();
            _ = _prefs()?.SaveExpansionAsync(_expansion);
        }

        private List<string> CollectExpandedKeys()
        {
            var keys = new List<string>();
            foreach (var (name, tree) in ExpandableTrees)
                CollectExpanded(tree, name, keys);
            return keys;
        }

        private string _searchText = "";
        private DispatcherTimer? _searchDebouncer;

        /// <summary>
        /// Hledaný text napříč všemi stromy (prázdný = bez filtru). Filtrace se pouští ~150 ms po
        /// posledním stisku - synchronní průchod všemi stromy na každý znak jinak sekal UI.
        /// </summary>
        internal string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                _searchDebouncer ??= CreateSearchDebouncer();
                _searchDebouncer.Stop();
                _searchDebouncer.Start();
            }
        }

        private DispatcherTimer CreateSearchDebouncer()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => { timer.Stop(); ApplySearch(); };
            return timer;
        }

        /// <summary>Po přestavbě stromů: nové uzly mají IsVisibleBySearch=true, trvající hledání se musí aplikovat znovu.</summary>
        internal void ReapplySearch()
        {
            if (_searchText.Trim().Length > 0) ApplySearch();
        }

        // Filtruje VŠECHNY stromy (vč. Oblíbených). Rozbalení vynucené hledáním se NEpersistuje;
        // snímek ze začátku hledání se po vymazání textu obnoví.
        private void ApplySearch()
        {
            var text = _searchText.Trim();
            if (text.Length > 0) _expansionBeforeSearch ??= CollectExpandedKeys();

            using (SuppressExpansionSave())
            {
                foreach (var (_, tree) in ExpandableTrees)
                    foreach (var root in tree)
                        ApplySearchToModule(root, text);

                if (text.Length == 0 && _expansionBeforeSearch is { } before)
                {
                    _expansionBeforeSearch = null;
                    ApplyTreeExpansion(before);
                }
            }
        }

        /// <summary>Aplikuje čekající hledání hned (debounce v testech netiká, Enter nesmí číst starý stav).</summary>
        internal void FlushPendingSearch()
        {
            if (_searchDebouncer is not { IsEnabled: true }) return;
            _searchDebouncer.Stop();
            ApplySearch();
        }

        /// <summary>
        /// První modul odpovídající hledání v záložce, nebo <c>null</c> (prázdné hledání, záložka
        /// bez stromu, žádný nález).
        /// </summary>
        internal ExecutableModule? FirstSearchResult(TreeTab tab)
        {
            if (_searchText.Trim().Length == 0) return null;
            FlushPendingSearch();
            return tab.Tree is { } tree ? FirstVisibleExecutable(tree) : null;
        }
    }
}
