using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserFonts;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias — class i namespace se jmenují stejně, plné qualifikování by bylo ukecané.
using EDCompTestWnd = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
using LogoutCountdownWnd = EDISONMAIN2.Windows.LogoutCountdownWindow.LogoutCountdownWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Name, string Database)
    {
        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Name} [{Id}]";
    }

    class EdisonMainViewModel : EDViewModel
    {
        private readonly IModuleTreeGetter _treeGetter;
        private readonly CompanySwitcher _switcher;
        private readonly Func<SwitchUserOutcome> _switchUser;
        private readonly ILocalPrefs _prefs;
        private readonly Func<IReadOnlyList<RunningModuleInfo>> _getActiveModules;
        private readonly MiniAppCatalog _miniAppCatalog;
        // Manager žije až po AttachMiniAppHost (potřebuje DockingManager z view) — do té doby null.
        private MiniAppManager? _miniAppManager;

        public ICommand OpenModuleCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand OpenLogoutCountdownTestCommand { get; }
        public ICommand TriggerErrorCommand { get; }
        public ICommand OpenTemplateDesignerCommand { get; }
        public ICommand CloseAllCommand { get; }
        public ICommand OpenMiniAppCommand { get; }

        // Hlavní menu (replikuje legacy frmNoMDI.mnuMain).
        // EDISON menu
        public ICommand ConnectToDatabaseCommand { get; }
        public ICommand RunEdisonCopyCommand { get; }
        public ICommand ExitCommand { get; }
        // Uživatel menu
        public ICommand LoginAsAnotherUserCommand { get; }
        public ICommand ShowUserEditCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand ShowBgTasksCommand { get; }
        public ICommand ResetFormsCommand { get; }
        // Nástroje menu
        public ICommand RefreshLicenseCommand { get; }
        public ICommand ShowUsersLocksCommand { get; }
        public ICommand ShowUsersActivityCommand { get; }
        public ICommand ShowLanguageSettingsCommand { get; }
        public ICommand SendSystemLogCommand { get; }
        // Nápověda menu
        public ICommand ShowInfoCommand { get; }
        public ICommand OpenHelpdeskWebCommand { get; }
        // Styl menu — SelectFont je akce, FontApplied je toggle (níže jako bool property).
        public ICommand SelectFontCommand { get; }
        // Spuštěné moduly menu
        public ICommand RefreshRunningModulesCommand { get; }
        public ICommand ActivateModuleCommand { get; }

        /// <summary>Dostupné mini-apps pro launcher (menu „Miniaplikace").</summary>
        public IReadOnlyCollection<MiniAppDescriptor> MiniApps => _miniAppCatalog.All;

        /// <summary>
        /// Napojí docking host (z view vrstvy) → vytvoří <see cref="MiniAppManager"/> a obnoví
        /// uložený layout aktuální firmy. Volá code-behind shellu po načtení okna.
        /// </summary>
        public void AttachMiniAppHost(IMiniAppHost host)
        {
            _miniAppManager = new MiniAppManager(host, _miniAppCatalog);
            RestoreMiniAppLayout(Idfa);
        }

        private void PersistMiniAppLayout(string idfa)
        {
            if (_miniAppManager is null) return;
            // Best-effort: serializace layoutu nesmí zablokovat zavření shellu / přepnutí firmy.
            try
            {
                (_prefs.MiniAppLayouts ??= new())[idfa] = _miniAppManager.SaveLayout();
                _prefs.Save();
            }
            catch { }
        }

        private void RestoreMiniAppLayout(string idfa)
        {
            if (_miniAppManager is null) return;
            if (_prefs.MiniAppLayouts?.TryGetValue(idfa, out var xml) != true || string.IsNullOrWhiteSpace(xml))
                return;
            try
            {
                _miniAppManager.RestoreLayout(xml);
            }
            catch
            {
                // Poškozený / nekompatibilní uložený layout nesmí shodit start shellu (letí z Loaded).
                // Zahodíme ho; dock zůstane prázdný, další uložení (close/switch) zapíše čistý.
                _prefs.MiniAppLayouts?.Remove(idfa);
                _prefs.Save();
            }
        }

        // Přepnutí session (firma/uživatel): ulož layout staré IDFa, zavři vše, obnov layout nové.
        private void ReplaceMiniAppSession(string oldIdfa)
        {
            if (_miniAppManager is null) return;
            PersistMiniAppLayout(oldIdfa);
            _miniAppManager.CloseAll();
            RestoreMiniAppLayout(Idfa);
        }

        public EdisonMainViewModel(
            IReadOnlyList<CompanyStruct> companies,
            IMLRest mlRest,
            IModuleTreeGetter treeService,
            Lic? initialLicence,
            CompanySwitcher switcher,
            Func<SwitchUserOutcome> switchUser,
            ILocalPrefs prefs,
            Func<IReadOnlyList<RunningModuleInfo>> getActiveModules,
            Action closeAllWindows,
            MiniAppCatalog miniAppCatalog)
        {
            _treeGetter = treeService;
            _switcher = switcher;
            _switchUser = switchUser;
            _prefs = prefs;
            _getActiveModules = getActiveModules;
            _miniAppCatalog = miniAppCatalog;

            OpenModuleCommand        = CommandAsync<IModule>(OpenModule);
            ToggleFavoriteCommand    = Command<IModule>(ToggleFavorite);
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show(new EDInputFormTestWindow()));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show(new EDListTestWindow()));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog(new EDCompTestWnd()));
            // Test trigger pro cooperative logout — countdown se po vypršení sám zavře (onExpired no-op).
            OpenLogoutCountdownTestCommand = Command(() => WindowNavigator.Show(new LogoutCountdownWnd(15, () => { })));
            // Test trigger pro error popup — výjimka projde RelayCommand handlerem do EDErrorPopup.
            TriggerErrorCommand = Command(() => throw new InvalidOperationException("Testovací výjimka z toolbaru."));
            OpenTemplateDesignerCommand = Command(OpenTemplateDesigner);
            CloseAllCommand          = Command(() => { PersistMiniAppLayout(Idfa); closeAllWindows(); _miniAppManager?.CloseAll(); });
            OpenMiniAppCommand       = Command<MiniAppDescriptor>(d => _miniAppManager?.Open(d));

            // EDISON menu
            ConnectToDatabaseCommand      = Command(NotYetImplemented);
            RunEdisonCopyCommand          = Command(RunEdisonCopy);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = Command(LoginAsAnotherUser);
            ShowUserEditCommand           = Command(() => WindowNavigator.ShowLegacyAsDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINUserEdit"));
            ChangePasswordCommand         = Command(() =>
            {
                bool? changed = WindowNavigator.ShowDialog(
                    new EDISONMAIN2.Windows.LoginWindow.ChangePasswordWindow(MLRest!, UserName));
                // Změna hesla za běhu zneplatní zapamatované (uložené při loginu se starým heslem).
                if (changed == true && _prefs.RememberedPassword != null)
                {
                    _prefs.RememberedPassword = null;
                    _prefs.Save();
                }
            });
            ShowBgTasksCommand            = Command(() => WindowNavigator.ShowLegacyAsDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList"));
            ResetFormsCommand             = Command(ResetForms);

            // Nástroje menu — Správa zámků a Aktivita uživatelů jsou v USERS.exe (retrofitted).
            RefreshLicenseCommand         = Command(RefreshLicense);
            // Admin-only — legacy frmNoMDI.vb:1101-1102 gatuje obě menu items přes UserIsAdmin.
            ShowUsersLocksCommand         = Command(() => WindowNavigator.ShowLegacy("USERS.exe", "ALVA.USERS.formUSERSLocks"), () => _services.IsAdmin);
            ShowUsersActivityCommand      = Command(() => WindowNavigator.ShowLegacy("USERS.exe", "ALVA.USERS.formUSERSActivity"), () => _services.IsAdmin);
            ShowLanguageSettingsCommand   = Command(() => WindowNavigator.ShowLegacyAsDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLanguageList"));
            SendSystemLogCommand          = Command(() => WindowNavigator.ShowLegacyAsDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLogList"));

            // Nápověda menu
            ShowInfoCommand               = Command(ShowAppInfo);
            OpenHelpdeskWebCommand        = Command(() => OpenWebUrl("http://podpora.m-line.cz"));

            // Styl menu
            SelectFontCommand             = Command(SelectFont);

            // Spuštěné moduly menu — dynamický seznam, refresh při SubmenuOpened.
            RefreshRunningModulesCommand  = Command(RefreshRunningModules);
            ActivateModuleCommand         = Command<RunningModuleInfo>(m => m.Activate());

            MainMenuTree = [];
            SystemAdminTree = [];
            PartnerSolutionsTree = [];
            FavoritesTree = [];
            UsedModules = [];
            _sourceTrees = [("main", MainMenuTree), ("system", SystemAdminTree), ("partners", PartnerSolutionsTree)];

            // GetCompanyList už vrací řazené podle Name & Id (SortedDictionary v ALVA.EDISONAPI.Database).
            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Name ?? "", c.Database))];
            // Index musí odpovídat MLRest.Idfa — tam má AppBootstrapper vytvořený ACTIVITY zámek.
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == mlRest.Idfa).i;

            _services.MLRest = mlRest;

            RebuildSessionState(initialLicence);
        }

        /// <summary>
        /// Po změně session state (initial load v ctor, switch firmy, manual refresh licence)
        /// přebuduje VEŠKERÝ session-scoped derived state. Jediný bod rozšíření pro budoucí
        /// session-scoped data (ECL, user settings, font scheme per firma, …) — přidá se sem,
        /// callsity nepotřebují vědět.
        /// </summary>
        private void RebuildSessionState(Lic? licence)
        {
            _services.ActiveLicence = licence;
            _services.AccessTable = AccessLoader.Load(MLRest!, licence, _services.IsAdmin);
            LoadAndFilterTrees();
            LoadPersonalization();
        }

        // Oblíbené + používanost jsou per-user × IDFa → načítají se po (pře)stavbě stromů.
        // _modulePrefs se vytváří tady (ne v ctoru), aby reflektoval případnou výměnu UserPrefs po switch-user.
        private void LoadPersonalization()
        {
            _modulePrefs = new ModulePreferencesService(UserPrefs);
            _favoriteIds = [.. _modulePrefs.LoadFavorites()];
            _usage = _modulePrefs.LoadUsage();
            _expansion = _modulePrefs.LoadExpansion();

            foreach (var exe in AllExecutables())
                exe.IsFavorite = _favoriteIds.Contains(exe.ElementId);

            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
            RebuildUsedList();

            // Obnoví uložené rozbalení a napojí persistenci na VŠECH stromech (vč. Oblíbených).
            // Suppress brání zápisu během programového apply.
            _suppressExpansionSave = true;
            ApplyTreeExpansion();
            _suppressExpansionSave = false;

            // Poslední otevřená záložka (per-user × IDFa); bez uložené volby výchozí dle oblíbených.
            _suppressTabSave = true;
            SelectedTreeTabIndex = _modulePrefs.LoadSelectedTab() ?? DefaultTreeTab();
            _suppressTabSave = false;
        }

        private void RebuildFavoritesTree()
        {
            FavoritesTree.Clear();
            foreach (var root in BuildFavoritesTree(_sourceTrees.Select(t => t.Tree), _favoriteIds))
                FavoritesTree.Add(root);

            // Po přestavbě (i mimo load — po toggle) znovu aplikuj/napoj rozbalení Oblíbených.
            bool prev = _suppressExpansionSave;
            _suppressExpansionSave = true;
            ApplyExpansion(FavoritesTree, "favorites");
            _suppressExpansionSave = prev;

            OnPropertyChanged(nameof(HasNoFavorites));
        }

        private void RebuildUsedList()
        {
            UsedModules.Clear();
            foreach (var module in BuildUsedList(_sourceTrees.Select(t => t.Tree), _usage, UsedModulesCap))
                UsedModules.Add(module);
        }

        // ---- persistence rozbalení stromu ------------------------------------
        // Klíč uzlu = "<tree>:<index-cesta>" (např. "main:5.2") — stabilní vůči přejmenování i suffixům
        // z ModuleLaunchabilityCheck, na rozdíl od cesty přes DisplayedName. Save běží na každou změnu
        // rozbalení (IDFa je tehdy stabilní), proto switch/close nepotřebují zvláštní obsluhu.

        private void ApplyTreeExpansion()
        {
            foreach (var (name, tree) in ExpandableTrees)
                ApplyExpansion(tree, name);
        }

        private void ApplyExpansion(IList<IModule> tree, string treeName)
        {
            for (int i = 0; i < tree.Count; i++)
                ApplyExpansionNode(tree[i], treeName, [i]);
        }

        private void ApplyExpansionNode(IModule module, string treeName, List<int> path)
        {
            if (module is not ModuleFamily family) return;

            // _expansion == null → první spuštění, ponech serverové výchozí rozbalení.
            if (_expansion is { } saved)
                family.IsExpanded = saved.Contains(ExpansionPathKey(treeName, path));

            family.PropertyChanged -= OnFamilyPropertyChanged;
            family.PropertyChanged += OnFamilyPropertyChanged;

            for (int i = 0; i < family.NestedModules.Count; i++)
                ApplyExpansionNode(family.NestedModules[i], treeName, [.. path, i]);
        }

        private void OnFamilyPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModuleFamily.IsExpanded) && !_suppressExpansionSave)
                _ = _modulePrefs.SaveExpansionAsync(CollectExpandedKeys());
        }

        private List<string> CollectExpandedKeys()
        {
            var keys = new List<string>();
            foreach (var (name, tree) in ExpandableTrees)
                CollectExpanded(tree, name, keys);
            return keys;
        }

        private static void CollectExpanded(IList<IModule> tree, string treeName, List<string> keys)
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

        private static string ExpansionPathKey(string treeName, IReadOnlyList<int> path) =>
            treeName + ":" + string.Join(".", path);

        // Filtruje VŠECHNY stromy (vč. Oblíbených) dle TreeSearchText. Rozbalení vynucené hledáním
        // se NEpersistuje (suppress flag); po vymazání textu se obnoví persistované rozbalení.
        private void ApplySearch()
        {
            var text = _treeSearchText.Trim();
            _suppressExpansionSave = true;
            try
            {
                foreach (var (_, tree) in ExpandableTrees)
                    foreach (var root in tree)
                        ApplySearchToModule(root, text);
            }
            finally { _suppressExpansionSave = false; }

            if (text.Length == 0) ApplyTreeExpansion();
        }

        /// <summary>
        /// Nastaví <see cref="IModule.IsVisibleBySearch"/> rekurzivně. Uzel je viditelný, když jeho
        /// název obsahuje <paramref name="text"/> (case-insensitive) NEBO má viditelného potomka
        /// (prázdný text = vše viditelné). Rodina s nalezeným potomkem se při aktivním hledání rozbalí.
        /// Vrací, zda je modul viditelný.
        /// </summary>
        internal static bool ApplySearchToModule(IModule module, string text)
        {
            bool match = text.Length == 0
                || module.DisplayedName.Contains(text, StringComparison.OrdinalIgnoreCase);

            if (module is ModuleFamily family)
            {
                bool anyChildVisible = false;
                foreach (var child in family.NestedModules)
                    if (ApplySearchToModule(child, text)) anyChildVisible = true;
                match = match || anyChildVisible;
                if (text.Length > 0 && anyChildVisible) family.IsExpanded = true;
            }

            module.IsVisibleBySearch = match;
            return match;
        }

        // Stromy i licenční gating jsou per-IdFa, takže po přepnutí firmy refresh všech tří.
        private void LoadAndFilterTrees()
        {
            foreach (var (name, tree) in _sourceTrees)
                LoadAndFilterTree(tree, name);
        }

        private void LoadAndFilterTree(ObservableCollection<IModule> target, string treeName)
        {
            target.Clear();
            var tree = _treeGetter.GetModuleTree(MLRest!, treeName);
            foreach (var module in tree)
            {
                if (TryFilter(module, _services.ActiveLicence, _services.AccessTable) is { } filtered)
                {
                    // Obarvit moduly dle launchovatelnosti (stub / chybí exe).
                    filtered = ModuleLaunchabilityCheck.Mark(filtered);
                    target.Add(filtered);
                }
            }
        }

        /// <summary>
        /// 4-vrstvá filtrace stromu (viz RESPONSIBILITIES § 5.2):
        /// 1) licence — <see cref="ExecutableModule.IsLicensed"/> uzel musí být v <see cref="Lic.Elements"/>;
        /// 2) hidden — <see cref="ExecutableModule.Hidden"/> uzly se vždy odfiltrují (legacy `hidden="1"`);
        /// 3) aplikační přístup — <c>AccessTable.Contains(ElementId)</c> pro každý EXE uzel;
        /// 4) admin fallback — řešen v <see cref="AccessLoader"/> už při loadu (přidá konkrétní IDs).
        /// FAMILY se auto-skryje, pokud všechny děti odfiltrované.
        /// </summary>
        internal static IModule? TryFilter(IModule module, Lic? licence, AccessTable? access = null) => module switch
        {
            ExecutableModule exe => IsAllowed(exe, licence, access) ? exe : null,
            ModuleFamily family  => FilterFamily(family, licence, access),
            _                    => module,
        };

        internal static bool IsAllowed(ExecutableModule exe, Lic? licence, AccessTable? access = null)
        {
            if (exe.Hidden) return false;
            if (exe.IsLicensed && !(licence?.Elements?.Contains(exe.ElementId) ?? false))
                return false;
            // AccessTable null = load nebyl proveden (typicky testy) → pass-through (graceful degradation).
            if (access != null && !access.Contains(exe.ElementId))
                return false;
            return true;
        }

        internal static ModuleFamily? FilterFamily(ModuleFamily family, Lic? licence, AccessTable? access = null)
        {
            var visibleChildren = family.NestedModules
                .Select(child => TryFilter(child, licence, access))
                .Where(child => child != null)
                .ToList();

            if (visibleChildren.Count == 0) return null;

            var filtered = new ModuleFamily
            {
                DisplayedName = family.DisplayedName,
                IsExpanded    = family.IsExpanded,
            };
            foreach (var child in visibleChildren) filtered.NestedModules.Add(child!);
            return filtered;
        }

        // StatusBar — Identity props z MLRest (per session konstantní).
        public string Server    => MLRest!.Server;
        public int    RestPort  => MLRest!.Port;
        public string UserName  => MLRest!.LoginName;
        public string FullName  => MLRest!.FullName;
        public string Mlp       => MLRest!.Mlp;
        public string Version   => VerzeInf.Current;

        public ObservableCollection<IModule> MainMenuTree { get; }
        public ObservableCollection<IModule> SystemAdminTree { get; }
        public ObservableCollection<IModule> PartnerSolutionsTree { get; }

        /// <summary>Ořezaný strom jen s oblíbenými (ruční ★) — zachovává hierarchii hlavních stromů.</summary>
        public ObservableCollection<IModule> FavoritesTree { get; }

        /// <summary>Žebříček nejpoužívanějších modulů (automaticky dle počtu spuštění × recency).</summary>
        public ObservableCollection<ExecutableModule> UsedModules { get; }

        /// <summary>Pro empty-state coaching v záložce Oblíbené.</summary>
        public bool HasNoFavorites => FavoritesTree.Count == 0;

        private string _treeSearchText = "";

        /// <summary>Hledaný text pro filtrování stromů (napříč všemi záložkami). Prázdný = bez filtru.</summary>
        public string TreeSearchText
        {
            get => _treeSearchText;
            set { if (Set(ref _treeSearchText, value)) ApplySearch(); }
        }

        // Pořadí záložek: 0 Hlavní nabídka, 1 Správa systému, 2 Partnerská řešení, 3 Oblíbené, 4 Používané.
        private int _selectedTreeTabIndex;
        private bool _suppressTabSave;

        /// <summary>Aktuální záložka stromu. TwoWay-bound; persistuje se per-user × IDFa (poslední otevřená).</summary>
        public int SelectedTreeTabIndex
        {
            get => _selectedTreeTabIndex;
            set
            {
                if (Set(ref _selectedTreeTabIndex, value) && !_suppressTabSave)
                    _ = _modulePrefs.SaveSelectedTabAsync(value);
            }
        }

        // Výchozí záložka při prvním spuštění (bez uložené volby): Oblíbené (3) když nějaké jsou, jinak Hlavní nabídka (0).
        private int DefaultTreeTab() => HasNoFavorites ? 0 : 3;

        // Per-user × IDFa stav (oblíbené + používanost). _modulePrefs se re-vytváří v RebuildSessionState
        // (UserPrefs se může po switch-user vyměnit). Stromové leaf instance se sdílí napříč kolekcemi.
        private ModulePreferencesService _modulePrefs = null!;
        private HashSet<string> _favoriteIds = [];
        private Dictionary<string, UsageEntry> _usage = [];
        // Klíče rozbalených uzlů (index-cesta). null = nikdy neuloženo → ponech serverové výchozí rozbalení.
        private List<string>? _expansion;
        private bool _suppressExpansionSave;
        private const int UsedModulesCap = 20;

        // Zdrojové stromy plněné ze serveru (per-IdFa). Jedna kolekce pro společné operace (load, filtrace,
        // oblíbené/používané, family flagy) místo opakování literálů. Inicializuje ctor po vytvoření kolekcí.
        private readonly (string Name, ObservableCollection<IModule> Tree)[] _sourceTrees;

        // Všechny rozbalovatelné stromy = zdrojové + odvozený Oblíbené (persistence rozbalení i nad Oblíbenými).
        private IEnumerable<(string Name, ObservableCollection<IModule> Tree)> ExpandableTrees =>
            [.. _sourceTrees, ("favorites", FavoritesTree)];

        public ObservableCollection<CompanyItem> VisibleIdfas { get; }

        private int _selectedCompanyIndex;
        public int SelectedCompanyIndex
        {
            get => _selectedCompanyIndex;
            set
            {
                if (_selectedCompanyIndex == value) return;

                if (!PopupDisplayer.Confirm($"Přepnout na firmu {VisibleIdfas[value].DisplayName}? Zavřou se všechna otevřená okna."))
                    return;

                int originalIndex = _selectedCompanyIndex;
                var oldIdfa = Idfa;

                Set(ref _selectedCompanyIndex, value);
                OnPropertyChanged(nameof(Namespace));
                OnPropertyChanged(nameof(Idfa));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(SelectedCompanyId));

                var result = _switcher.Switch(Idfa, Namespace, _services.ActiveLicence);

                if (result.Outcome == LicenceAcquireOutcome.Accepted)
                {
                    RebuildSessionState(result.Licence);
                    ReplaceMiniAppSession(oldIdfa);
                }
                else
                {
                    Set(ref _selectedCompanyIndex, originalIndex);
                    OnPropertyChanged(nameof(Namespace));
                    OnPropertyChanged(nameof(Idfa));
                    OnPropertyChanged(nameof(WindowTitle));
                    if (result.Outcome == LicenceAcquireOutcome.Refused)
                        PopupDisplayer.Info("Pro tuto firmu byl překročen počet licencí. Firma nebyla přepnuta.");
                }
            }
        }

        public CompanyItem SelectedCompany => VisibleIdfas[_selectedCompanyIndex];

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        public string WindowTitle => $"M-line EDISON [{Idfa}]";

        /// <summary>
        /// IDFa aktuální firmy pro filtrovatelný přepínač (SuggestComboBox.SelectedKey, TwoWay).
        /// Setter najde firmu a deleguje na <see cref="SelectedCompanyIndex"/> (potvrzení + switch + rollback);
        /// závěrečné OnPropertyChanged vrátí combobox na původní firmu, když se index nezměnil (cancel/reject).
        /// </summary>
        public string SelectedCompanyId
        {
            get => SelectedCompany.Id;
            set
            {
                int index = VisibleIdfas.ToList().FindIndex(c => c.Id == value);
                if (index >= 0) SelectedCompanyIndex = index;
                OnPropertyChanged(nameof(SelectedCompanyId));
            }
        }

        /// <summary>Firmy jako nápověda pro filtrovatelný přepínač (Key = IDFa, Label = název).</summary>
        public IEnumerable<ListItem> CompanySuggestions =>
            VisibleIdfas.Select(c => new ListItem(c.Id, c.Name));

        private async Task OpenModule(IModule module)
        {
            if (module is not ExecutableModule { ExecutablePath: { Length: > 0 } path } em) return;

            RecordUsage(em);
            // Overlay „akce probíhá" po dobu náběhu modulu (samostatný proces — čekáme na jeho okno).
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(path, em.FormName, inputParam: em.InputParam);
        }

        // Přepnutí oblíbeného. EXE = toggle jednoho ElementId; FAMILY = hromadně všechny moduly v ní
        // (přidá vše, pokud nejsou všechny oblíbené; jinak vše odebere). Hlavní stromy se nepřestavují
        // (jen flip IsFavorite) → rozbalení/výběr zůstávají; přestaví se jen záložka Oblíbené.
        private void ToggleFavorite(IModule module)
        {
            switch (module)
            {
                case ExecutableModule { ElementId: { Length: > 0 } id }:
                    SetFavorites([id], add: !_favoriteIds.Contains(id));
                    break;

                case ModuleFamily family:
                    var ids = Flatten(family).Select(e => e.ElementId).Where(x => x.Length > 0).Distinct().ToList();
                    if (ids.Count == 0) return;
                    SetFavorites(ids, add: !ids.All(_favoriteIds.Contains));
                    break;
            }
        }

        private void SetFavorites(IReadOnlyCollection<string> ids, bool add)
        {
            foreach (var id in ids)
                if (add) _favoriteIds.Add(id);
                else _favoriteIds.Remove(id);

            var changed = ids.ToHashSet();
            foreach (var exe in AllExecutables().Where(e => changed.Contains(e.ElementId)))
                exe.IsFavorite = add;

            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
            _ = _modulePrefs.SaveFavoritesAsync(_favoriteIds);
        }

        // Nastaví ModuleFamily.IsFavorite = "všichni potomci oblíbení" napříč hlavními stromy (řídí text
        // kontextového menu rodiny). Vrací, zda je uzel plně oblíbený (rekurzivní agregace).
        private void RefreshFamilyFavoriteFlags()
        {
            foreach (var (_, tree) in _sourceTrees)
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

        private void RecordUsage(ExecutableModule module)
        {
            if (module.ElementId.Length == 0) return;

            var entry = _usage.TryGetValue(module.ElementId, out var prev) ? prev : new UsageEntry();
            _usage[module.ElementId] = entry with { Count = entry.Count + 1, LastUsedUtc = DateTime.UtcNow };

            RebuildUsedList();
            _ = _modulePrefs.SaveUsageAsync(_usage);
        }

        // Všechny EXE instance napříč hlavními stromy. FavoritesTree i UsedModules sdílí tytéž instance,
        // takže stačí projít hlavní tři stromy (flip IsFavorite se projeví všude).
        private IEnumerable<ExecutableModule> AllExecutables() =>
            _sourceTrees.SelectMany(t => t.Tree).SelectMany(Flatten);

        private static IEnumerable<ExecutableModule> Flatten(IModule module) => module switch
        {
            ExecutableModule exe => [exe],
            ModuleFamily family  => family.NestedModules.SelectMany(Flatten),
            _                    => [],
        };

        /// <summary>
        /// Ořezané stromy jen s oblíbenými listy (predikát <c>favoriteIds.Contains(ElementId)</c>).
        /// Zachovává hierarchii, prázdné rodiny se vynechají. Listy se SDÍLEJÍ (ne kopírují) s hlavními
        /// stromy — přepnutí IsFavorite se projeví v obou. Rodiny jsou nové (rozbalené pro přehled).
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
            ExecutableModule exe => favoriteIds.Contains(exe.ElementId) ? exe : null,
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
            var pruned = new ModuleFamily { DisplayedName = family.DisplayedName, IsExpanded = true, Color = family.Color, IsFavorite = true };
            foreach (var child in kept) pruned.NestedModules.Add(child!);
            return pruned;
        }

        /// <summary>
        /// Plochý žebříček nejpoužívanějších modulů: řazení dle počtu spuštění (desc), tie-break dle
        /// posledního použití (desc), cap na <paramref name="cap"/>. Mapuje na EXISTUJÍCÍ leaf instance
        /// z hlavních stromů — tím respektuje aktuální licenci/přístup (nezobrazený modul se přeskočí)
        /// a sdílí IsFavorite. Každý ElementId právě jednou.
        /// </summary>
        internal static List<ExecutableModule> BuildUsedList(
            IEnumerable<IEnumerable<IModule>> sourceTrees, IReadOnlyDictionary<string, UsageEntry> usage, int cap)
        {
            var byElementId = new Dictionary<string, ExecutableModule>();
            foreach (var tree in sourceTrees)
                foreach (var node in tree)
                    foreach (var exe in Flatten(node))
                        if (exe.ElementId.Length > 0)
                            byElementId.TryAdd(exe.ElementId, exe);

            return usage
                .Where(kv => byElementId.ContainsKey(kv.Key))
                .OrderByDescending(kv => kv.Value.Count)
                .ThenByDescending(kv => kv.Value.LastUsedUtc)
                .Take(cap)
                .Select(kv => byElementId[kv.Key])
                .ToList();
        }

        private void ExitApplication()
        {
            if (PopupDisplayer.Confirm("Opravdu chcete ukončit aplikaci?"))
                Application.Current.Shutdown();
        }

        private void LoginAsAnotherUser()
        {
            if (!PopupDisplayer.Confirm("Přihlásit jiného uživatele zavře všechna otevřená okna. Pokračovat?"))
                return;

            var outcome = _switchUser();
            switch (outcome.Outcome)
            {
                case SwitchUserResult.Cancelled:
                    return;
                case SwitchUserResult.LicenceRefused:
                    PopupDisplayer.Info("Pro nového uživatele byl překročen počet licencí. Aplikace bude ukončena.");
                    Application.Current.Shutdown();
                    return;
                case SwitchUserResult.LicenceCancelled:
                    Application.Current.Shutdown();
                    return;
                case SwitchUserResult.Success:
                    RebuildSessionState(outcome.Licence);
                    ReplaceMiniAppSession(Idfa);   // přihlášení jiného uživatele: stejná firma, jiná data
                    // StatusBar identity bindings refreshne na novém MLRest.
                    OnPropertyChanged(nameof(UserName));
                    OnPropertyChanged(nameof(FullName));
                    OnPropertyChanged(nameof(Mlp));
                    // Admin-gated commandy (ShowUsersLocks/Activity) re-vyhodnotí CanExecute hned,
                    // ne až na první input event po switch.
                    CommandManager.InvalidateRequerySuggested();
                    break;
            }
        }

        private void ResetForms()
        {
            if (!PopupDisplayer.Confirm("Obnovit výchozí pozice a velikosti všech oken?"))
                return;
            // Reset persistovaných pozic/velikostí oken (per MLP × IDFa) — legacy endpoint.
            MLRest!.QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", new CVar());
            PopupDisplayer.Info("Nastavení oken bylo obnoveno. Změna se projeví po jejich příštím otevření.");
        }

        private static void RunEdisonCopy()
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        // Spustí TemplateDesigner.exe standalone — používá vlastní MLRest z templatedesigner.appsettings.json,
        // nesdílí shell session. Pro plnou integraci by TD musel implementovat EDISONMAIN2API kontrakt.
        private static void OpenTemplateDesigner()
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "TemplateDesigner.exe");
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(EDControls.EDViewModel.WindowNavigator.EdisonDeployRoot, "TemplateDesigner.exe");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private void RefreshLicense()
        {
            RebuildSessionState(_services.LicenceLoader?.Invoke());
            PopupDisplayer.Info("Licence byla aktualizována.");
        }

        // Uživatel → Možnosti — toggles uložené do LocalPrefs. Setter persistuje a notifikuje.
        // ShowWindowsAsDialogs zatím bez behaviorálního efektu (dialog mode nikdo nečte) — flag
        // se ukládá pro budoucnost.
        public bool ShowWindowsAsDialogs
        {
            get => _prefs.ShowWindowsAsDialogs;
            set
            {
                if (_prefs.ShowWindowsAsDialogs == value) return;
                _prefs.ShowWindowsAsDialogs = value;
                _prefs.Save();
                OnPropertyChanged();
            }
        }


        // Styl → Použít — aplikuje uložený font (FontFamily + FontSize) na celou aplikaci.
        // Storage je shared font.json (EDControls.UserFonts.UserFontPreference), takže ostatní
        // m-line tooly (TemplateDesigner) přes FileSystemWatcher okamžitě reagují na změnu.
        public bool FontApplied
        {
            get => UserFontPreference.Current.FontApplied;
            set
            {
                if (UserFontPreference.Current.FontApplied == value) return;
                UserFontPreference.Current.FontApplied = value;
                UserFontPreference.Current.Save();
                OnPropertyChanged();
                FontService.Apply(UserFontPreference.Current);
            }
        }

        // Styl → Zvolený font — WinForms FontDialog (jediný standardní picker dostupný v .NET).
        private void SelectFont()
        {
            using var dialog = new System.Windows.Forms.FontDialog();
            if (!string.IsNullOrEmpty(UserFontPreference.Current.FontFamily))
            {
                try
                {
                    dialog.Font = new System.Drawing.Font(
                        UserFontPreference.Current.FontFamily,
                        (float)(UserFontPreference.Current.FontSize ?? 12));
                }
                catch { /* nepřežijeme nepoužitelný font — picker použije default */ }
            }
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            UserFontPreference.Current.FontFamily = dialog.Font.FontFamily.Name;
            UserFontPreference.Current.FontSize = dialog.Font.Size;
            UserFontPreference.Current.Save();
            if (FontApplied) FontService.Apply(UserFontPreference.Current);
        }

        private void OpenWebUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void ShowAppInfo()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly().GetName();
            var info = $"M-line EDISON 2\n\n" +
                       $"Verze: {asm.Version}\n" +
                       $"Server: {MLRest!.Server}:{MLRest!.Port}\n" +
                       $"Namespace: {MLRest!.DataNamespace}\n" +
                       $"Firma: {MLRest!.Idfa}\n" +
                       $"Uživatel: {MLRest!.FullName} ({MLRest!.LoginName})";
            PopupDisplayer.Info(info);
        }

        // INFO label (vpravo v menu). Stálý text bez timer refreshe — typicky se shell
        // znovu spustí denně, půlnoční rollover neřešíme. Pokud bude potřeba, přidá se
        // DispatcherTimer naplánovaný na další půlnoc.
        public string InformationText { get; } = BuildInformationText(DateOnly.FromDateTime(DateTime.Today));

        internal static string BuildInformationText(DateOnly today)
        {
            var text = $"Dnes je {today:dd.MM.yyyy}";
            var nameday = CzechNamedays.GetByDate(today);
            if (!string.IsNullOrEmpty(nameday)) text += $", svátek má {nameday}";
            return text;
        }

        // Initial = placeholder: WPF neotevře prázdný submenu, takže MouseEnter refresh by se nevyvolal.
        private IReadOnlyList<RunningModuleInfo> _runningModules = [_emptyPlaceholder];
        public IReadOnlyList<RunningModuleInfo> RunningModules
        {
            get => _runningModules;
            private set => Set(ref _runningModules, value);
        }

        private static readonly RunningModuleInfo _emptyPlaceholder =
            new("Žádné moduly nejsou spuštěné", () => { }, IsEnabled: false);

        private void RefreshRunningModules()
        {
            var modules = _getActiveModules();
            RunningModules = modules.Count == 0 ? [_emptyPlaceholder] : modules;
        }

        private void NotYetImplemented()
        {
            PopupDisplayer.Info("Tato funkce zatím není v EDISONu 2 dostupná.");
        }

        // Ctrl+W+W chord — skrytá zkratka pro M-line utility (RESPONSIBILITIES §15.1).
        // Stav perzistuje v UI threadu, není potřeba timeout — jakákoliv jiná klávesa chord resetuje.
        private bool _isChordWaiting;
        public bool IsChordWaiting
        {
            get => _isChordWaiting;
            private set
            {
                if (Set(ref _isChordWaiting, value))
                    OnPropertyChanged(nameof(ChordHintText));
            }
        }
        public string ChordHintText => IsChordWaiting ? "Stisknuto Ctrl+W — čekám na druhý stisk…" : "";

        public void HandleCtrlW()
        {
            if (IsChordWaiting)
            {
                IsChordWaiting = false;
                // UTILITIES.exe ukáže vlastní FrmPassword (legacy date-dependent vzorec) —
                // shell jen detekuje chord a spustí. Single source of truth pro formuli.
                WindowNavigator.ShowLegacy("UTILITIES.exe", "ALVA.UTILITIES.formUTILITIESMain");
            }
            else
            {
                IsChordWaiting = true;
            }
        }

        public void ResetChord()
        {
            if (IsChordWaiting) IsChordWaiting = false;
        }
    }
}
