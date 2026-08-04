using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDSupportPanel;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDControls.UserFonts;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.UserProfileWindow;
// Alias — class i namespace se jmenují stejně, plné qualifikování by bylo ukecané.
using EDCompTestWnd = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
using UserProfileWnd = EDISONMAIN2.Windows.UserProfileWindow.UserProfileWindow;
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
        // Přístup k šířce stromu (ColumnDefinition ve view) — registruje code-behind přes AttachTreeLayout.
        private Func<double>? _getTreeWidth;
        private Action<double>? _applyTreeWidth;

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
        public ICommand OpenDefaultMiniAppsCommand { get; }
        public ICommand CloseMiniAppsCommand { get; }
        public ICommand ResetMiniAppsCommand { get; }
        public ICommand ShowDesktopMiniAppCommand { get; }

        // Hlavní menu (replikuje legacy frmNoMDI.mnuMain).
        // EDISON menu
        public ICommand ConnectToDatabaseCommand { get; }
        public ICommand RunEdisonCopyCommand { get; }
        public ICommand ExitCommand { get; }
        // Uživatel menu
        public ICommand LoginAsAnotherUserCommand { get; }
        public ICommand ShowUserEditCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        // Kontextové menu stromu — výchozí okno (auto-open po loginu)
        public ICommand SetStartupWindowCommand { get; }
        public ICommand ClearStartupWindowCommand { get; }
        // Toolbar stromu — rozbalit/sbalit celý aktivní strom + rozbalit/sbalit podstrom rodiny.
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandSubtreeCommand { get; }
        public ICommand CollapseSubtreeCommand { get; }
        // Nástroje menu
        public ICommand ShowBgTasksCommand { get; }
        public ICommand RefreshLicenseCommand { get; }
        public ICommand ShowUsersLocksCommand { get; }
        public ICommand ShowUsersActivityCommand { get; }
        // Nastavení menu (+ toggle ShowWindowsAsDialogs níže jako bool property)
        public ICommand FontSettingsCommand { get; }
        public ICommand ShowLanguageSettingsCommand { get; }
        public ICommand ResetFormsCommand { get; }
        // Nápověda menu
        public ICommand ShowInfoCommand { get; }
        public ICommand OpenHelpdeskWebCommand { get; }
        public ICommand SendSystemLogCommand { get; }
        // Spuštěné moduly menu
        public ICommand RefreshRunningModulesCommand { get; }
        public ICommand ActivateModuleCommand { get; }

        /// <summary>Mini-apps seskupené podle providerů pro launcher (menu „Miniaplikace").</summary>
        public IReadOnlyList<MiniAppGroup> MiniAppGroups => _miniAppCatalog.Groups;

        // Výchozí sada: pinned karusel Podpora M-line (cyklí Novinky/Wiki/Školení) — samostatné
        // widgety by vedle něj duplikovaly obsah, uživatel si je může otevřít z menu.
        private static readonly string[] DefaultMiniAppIds = ["Core.Podpora"];

        private IEnumerable<MiniAppDescriptor> DefaultMiniApps =>
            DefaultMiniAppIds.Select(_miniAppCatalog.Find).OfType<MiniAppDescriptor>();

        private bool _hasOpenMiniApps;
        /// <summary>Je v docku alespoň jedna mini-app? False → shell ukáže prázdný placeholder.</summary>
        public bool HasOpenMiniApps { get => _hasOpenMiniApps; private set => Set(ref _hasOpenMiniApps, value); }

        private bool _hasOpenDesktop;
        /// <summary>Je otevřená miniaplikace Plocha? False → proužek pod stromem s výzvou k zobrazení.</summary>
        public bool HasOpenDesktop { get => _hasOpenDesktop; private set => Set(ref _hasOpenDesktop, value); }

        /// <summary>
        /// Napojí docking host (z view vrstvy) → vytvoří <see cref="MiniAppManager"/> a obnoví
        /// uložený layout aktuální firmy. Volá code-behind shellu po načtení okna.
        /// </summary>
        public void AttachMiniAppHost(IMiniAppHost host)
        {
            _miniAppManager = new MiniAppManager(host, _miniAppCatalog);
            _miniAppManager.InstancesChanged += UpdateMiniAppFlags;
            RestoreMiniAppLayout(Idfa);
        }

        private void UpdateMiniAppFlags()
        {
            HasOpenMiniApps = _miniAppManager!.OpenInstances.Count > 0;
            HasOpenDesktop = _miniAppManager.OpenInstances.Any(i => i.DescriptorId == DesktopMiniAppProvider.DesktopId);
        }

        /// <summary>
        /// Napojí přístup k šířce stromu z view (ColumnDefinition). Šířka se persistuje per IDFa
        /// společně s layoutem miniaplikací. Volat **před** <see cref="AttachMiniAppHost"/> — ten
        /// spouští první restore, který už šířku aplikuje.
        /// </summary>
        public void AttachTreeLayout(Func<double> getWidth, Action<double> applyWidth)
        {
            _getTreeWidth = getWidth;
            _applyTreeWidth = applyWidth;
        }

        // Výchozí rozložení: web widgety ve sloupcích nahoře + Plocha přes celou šířku dole.
        private void OpenDefaultMiniApps()
        {
            _miniAppManager?.OpenDefaultLayout(DefaultMiniApps, _miniAppCatalog.Find(DesktopMiniAppProvider.DesktopId));
            // Pinned mimo výchozí sadu nesmí zmizet ani po resetu / prvním spuštění.
            _miniAppManager?.EnsurePinnedOpen();
        }

        private void PersistMiniAppLayout(string idfa)
        {
            if (_miniAppManager is null) return;
            // Best-effort: serializace layoutu nesmí zablokovat zavření shellu / přepnutí firmy.
            try
            {
                (_prefs.MiniAppLayouts ??= new())[idfa] = _miniAppManager.SaveLayout();
                if (_getTreeWidth is not null)
                    (_prefs.TreeWidths ??= new())[idfa] = _getTreeWidth();
                _prefs.Save();
            }
            catch { }
        }

        private void RestoreMiniAppLayout(string idfa)
        {
            if (_miniAppManager is null) return;
            // Šířka stromu (nezávislá na tom, zda je uložený layout miniaplikací) — bez uložené
            // hodnoty zůstane XAML default (*).
            if (_applyTreeWidth is not null && _prefs.TreeWidths?.TryGetValue(idfa, out var width) == true && width > 0)
                _applyTreeWidth(width);
            if (_prefs.MiniAppLayouts?.TryGetValue(idfa, out var xml) != true || string.IsNullOrWhiteSpace(xml))
            {
                // Žádný uložený layout (první spuštění / nová firma) → výchozí rozložení.
                OpenDefaultMiniApps();
                return;
            }
            try
            {
                _miniAppManager.RestoreLayout(xml);
                // Layout z doby před pinned sadou (nebo s dříve zavřeným pinned widgetem) ho neobsahuje.
                _miniAppManager.EnsurePinnedOpen();
            }
            catch
            {
                // Poškozený / nekompatibilní uložený layout nesmí shodit start shellu (letí z Loaded).
                // Zahodíme ho; další uložení (close/switch) zapíše čistý.
                _prefs.MiniAppLayouts?.Remove(idfa);
                _prefs.Save();
                // Ani po zahozeném layoutu nesmí chybět pinned komunikační kanál. Vlastní guard —
                // stav docku po nezdařeném Deserialize je nedefinovaný a druhý pád by shodil start.
                try { _miniAppManager.EnsurePinnedOpen(); }
                catch { }
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
            MiniAppCatalog miniAppCatalog,
            ModuleShortcutsService? shortcuts = null)
        {
            _treeGetter = treeService;
            _switcher = switcher;
            _switchUser = switchUser;
            _prefs = prefs;
            _getActiveModules = getActiveModules;
            _miniAppCatalog = miniAppCatalog;
            // Sdílená služba zástupců plochy (strom ★ ⇄ dlaždice). Bez ní (testy) VM drží vlastní instanci.
            _shortcuts = shortcuts ?? new ModuleShortcutsService();

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
            OpenDefaultMiniAppsCommand = Command(OpenDefaultMiniApps);
            // Uživatelské „Zavřít miniaplikace" nechá pinned otevřené (CloseAll zůstává pro session/shutdown).
            CloseMiniAppsCommand     = Command(() => _miniAppManager?.CloseUnpinned());
            // Reset = zavřít vše (proběhne OnClosed úklid VM) a znovu otevřít výchozí sadu.
            ResetMiniAppsCommand     = Command(() => { _miniAppManager?.CloseAll(); OpenDefaultMiniApps(); });
            // Proužek pod stromem: otevře Plochu jako řadu dole (mirror výchozího umístění).
            ShowDesktopMiniAppCommand = Command(() =>
            {
                if (_miniAppCatalog.Find(DesktopMiniAppProvider.DesktopId) is { } d)
                    _miniAppManager?.Open(d, newRow: true);
            });

            // EDISON menu
            ConnectToDatabaseCommand      = Command(NotYetImplemented);
            RunEdisonCopyCommand          = Command(RunEdisonCopy);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = Command(LoginAsAnotherUser);
            ShowUserEditCommand           = Command(() => WindowNavigator.ShowDialog(new UserProfileWnd(StartupWindowDisplayText())));
            ChangePasswordCommand         = Command(() =>
            {
                bool? changed = WindowNavigator.ShowDialog(
                    new EDISONMAIN2.Windows.LoginWindow.ChangePasswordWindow(Rest, UserName));
                // Změna hesla za běhu zneplatní zapamatované (uložené při loginu se starým heslem).
                if (changed == true && _prefs.RememberedPassword != null)
                {
                    _prefs.RememberedPassword = null;
                    _prefs.Save();
                }
            });
            SetStartupWindowCommand         = Command<IModule>(SetStartupWindow);
            ClearStartupWindowCommand       = Command(ClearStartupWindow);
            ExpandAllCommand                = Command(() => SetActiveTreeExpanded(true), () => ActiveExpandableTree() is not null);
            CollapseAllCommand              = Command(() => SetActiveTreeExpanded(false), () => ActiveExpandableTree() is not null);
            ExpandSubtreeCommand            = Command<IModule>(m => SetSubtreeExpanded(m, true));
            CollapseSubtreeCommand          = Command<IModule>(m => SetSubtreeExpanded(m, false));

            // Nástroje menu — Správa zámků a Aktivita uživatelů jsou v USERS.exe (retrofitted).
            ShowBgTasksCommand            = CommandAsync(() => LaunchDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList"));
            RefreshLicenseCommand         = Command(RefreshLicense);
            // Admin-only — legacy frmNoMDI.vb:1101-1102 gatuje obě menu items přes UserIsAdmin.
            ShowUsersLocksCommand         = CommandAsync(() => LaunchModeless("USERS.exe", "ALVA.USERS.formUSERSLocks"), () => _services.IsAdmin);
            ShowUsersActivityCommand      = CommandAsync(() => LaunchModeless("USERS.exe", "ALVA.USERS.formUSERSActivity"), () => _services.IsAdmin);

            // Nastavení menu
            FontSettingsCommand           = Command(ShowFontSettings);
            ShowLanguageSettingsCommand   = CommandAsync(() => LaunchDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLanguageList"));
            ResetFormsCommand             = Command(ResetForms);

            // Nápověda menu
            ShowInfoCommand               = Command(ShowAppInfo);
            OpenHelpdeskWebCommand        = Command(() => OpenWebUrl("http://podpora.m-line.cz"));
            SendSystemLogCommand          = CommandAsync(() => LaunchDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLogList"));

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

            // Změny plochy z miniaplikace (DnD add, remove, reorder) → přestav strom + persist.
            // Seed při loadu je pod _suppressFavoritesSync (nemá znovu ukládat načtené).
            _shortcuts.Items.CollectionChanged += (_, _) => { if (!_suppressFavoritesSync) SyncFavorites(); };
            // Přesun dlaždice (reorder v miniaplikaci) → persistuj pozice (bez změny členství).
            _shortcuts.SlotsChanged += PersistShortcuts;
            // Spuštění z dlaždice jde přes OpenModule (usage + overlay), ne duplikovaně v miniaplikaci.
            _shortcuts.LaunchRequested += LaunchShortcut;

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
            _services.AccessTable = AccessLoader.Load(Rest, licence, _services.IsAdmin);
            LoadAndFilterTrees();
            LoadPersonalization();
            LoadStartupWindow();

            // Nové instance uzlů mají IsVisibleBySearch=true — trvající hledaný text je nutné znovu aplikovat.
            if (_treeSearchText.Trim().Length > 0) ApplySearch();
        }

        private int _defaultWindowId;
        /// <summary>Id uživatelova výchozího okna (EDISFormEnum) pro aktuální IDFa; 0 = žádný.</summary>
        public int DefaultWindowId { get => _defaultWindowId; private set => Set(ref _defaultWindowId, value); }

        // Načte výchozí okno (per IDFa) a označí odpovídající uzel stromu. Volá se z
        // RebuildSessionState → po loginu i po přepnutí firmy (jiná firma může mít jiný default).
        private void LoadStartupWindow()
        {
            DefaultWindowId = UserProfileService.LoadDefaultWindow(Rest);
            ApplyStartupWindowMark();
        }

        // Přeznačí uzly stromu — právě jeden (nebo žádný) modul má IsStartupWindow=true.
        private void ApplyStartupWindowMark()
        {
            var id = DefaultWindowId.ToString();
            foreach (var exe in AllExecutables())
                exe.IsStartupWindow = DefaultWindowId != 0 && exe.ElementId == id;
        }

        private void SetStartupWindow(IModule module)
        {
            if (module is not ExecutableModule { ElementId: { Length: > 0 } elementId }) return;
            if (!int.TryParse(elementId, out var formId) || formId == 0) return;
            UserProfileService.SetDefaultWindow(Rest, formId);
            DefaultWindowId = formId;
            ApplyStartupWindowMark();
        }

        private void ClearStartupWindow()
        {
            if (DefaultWindowId == 0) return;
            UserProfileService.SetDefaultWindow(Rest, 0);
            DefaultWindowId = 0;
            ApplyStartupWindowMark();
        }

        private bool _startupWindowOpened;
        /// <summary>
        /// Po prvním vykreslení shellu otevře uživatelův výchozí okno — jednou za běh (ne po
        /// přepnutí firmy). Nepovolený/chybějící modul (není ve stromu) se tiše přeskočí.
        /// Volá code-behind shellu z <c>ContentRendered</c>.
        /// </summary>
        public void OpenStartupWindowIfAny()
        {
            if (_startupWindowOpened) return;
            _startupWindowOpened = true;
            if (DefaultWindowId == 0) return;
            var module = AllExecutables().FirstOrDefault(e => e.ElementId == DefaultWindowId.ToString());
            if (module != null && OpenModuleCommand.CanExecute(module))
                OpenModuleCommand.Execute(module);
        }

        // Oblíbené + používanost jsou per-user × IDFa → načítají se po (pře)stavbě stromů.
        // _modulePrefs se vytváří tady (ne v ctoru), aby reflektoval případnou výměnu UserPrefs po switch-user.
        private void LoadPersonalization()
        {
            _modulePrefs = new ModulePreferencesService(UserPrefs);
            _usage = _modulePrefs.LoadUsage();
            _expansion = _modulePrefs.LoadExpansion();

            // Seed sdílené služby z uložených položek (nastaví IsFavorite na sdílených instancích).
            // Pod suppressem → CollectionChanged handler nespustí zbytečný re-save načteného.
            _suppressFavoritesSync = true;
            _shortcuts.Seed(AllExecutables(), _modulePrefs.LoadShortcuts());
            _suppressFavoritesSync = false;

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
            foreach (var root in BuildFavoritesTree(_sourceTrees.Select(t => t.Tree), FavoriteIdSet()))
                FavoritesTree.Add(root);

            // Po přestavbě (i mimo load — po toggle) znovu aplikuj/napoj rozbalení Oblíbených.
            bool prev = _suppressExpansionSave;
            _suppressExpansionSave = true;
            ApplyExpansion(FavoritesTree, "favorites");

            // PruneFamily staví nové rodinné wrappery (IsVisibleBySearch=true) — při aktivním hledání přefiltrovat.
            var searchText = _treeSearchText.Trim();
            if (searchText.Length > 0)
                foreach (var root in FavoritesTree)
                    ApplySearchToModule(root, searchText);
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

        // ---- rozbalit / sbalit vše (toolbar + kontextové menu) ---------------

        // Strom aktivní záložky (index 0-2 zdrojové, 3 Oblíbené); Používané (ListBox) i cokoli mimo → null.
        private ObservableCollection<IModule>? ActiveExpandableTree() => SelectedTreeTabIndex switch
        {
            0 => MainMenuTree,
            1 => SystemAdminTree,
            2 => PartnerSolutionsTree,
            3 => FavoritesTree,
            _ => null,
        };

        // Hromadné rozbalení/sbalení celého aktivního stromu. Suppress + jeden save na konci
        // (jinak by každá rodina zvlášť triggerovala SaveExpansionAsync — vzor jako ApplySearch).
        private void SetActiveTreeExpanded(bool expanded)
        {
            if (ActiveExpandableTree() is not { } tree) return;
            _suppressExpansionSave = true;
            try
            {
                foreach (var module in tree)
                    SetExpandedRecursive(module, expanded);
            }
            finally { _suppressExpansionSave = false; }
            _ = _modulePrefs.SaveExpansionAsync(CollectExpandedKeys());
        }

        // Rozbalit/sbalit rodinu i její potomky (kontextové menu „úroveň").
        private void SetSubtreeExpanded(IModule module, bool expanded)
        {
            if (module is not ModuleFamily) return;
            _suppressExpansionSave = true;
            try { SetExpandedRecursive(module, expanded); }
            finally { _suppressExpansionSave = false; }
            _ = _modulePrefs.SaveExpansionAsync(CollectExpandedKeys());
        }

        // internal kvůli unit testům (InternalsVisibleTo EDISONMAIN2.Tests).
        internal static void SetExpandedRecursive(IModule module, bool expanded)
        {
            if (module is not ModuleFamily family) return;
            family.IsExpanded = expanded;
            foreach (var child in family.NestedModules)
                SetExpandedRecursive(child, expanded);
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

        private DispatcherTimer? _searchDebouncer;

        // Filtrace se pouští ~150 ms po posledním stisku — synchronní průchod všemi stromy
        // na každý znak jinak sekal UI. Programový re-apply (po přepnutí firmy) volá ApplySearch přímo.
        private void ScheduleSearch()
        {
            _searchDebouncer ??= CreateSearchDebouncer();
            _searchDebouncer.Stop();
            _searchDebouncer.Start();
        }

        private DispatcherTimer CreateSearchDebouncer()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => { timer.Stop(); ApplySearch(); };
            return timer;
        }

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
        /// název obsahuje <paramref name="text"/> (case + diakritika insensitive) NEBO má viditelného
        /// potomka (prázdný text = vše viditelné). Rodina s nalezeným potomkem se při aktivním hledání
        /// rozbalí. Vrací, zda je modul viditelný.
        /// </summary>
        internal static bool ApplySearchToModule(IModule module, string text)
        {
            var needle = SearchNormalization.NormalizeForSearch(text);
            bool match = needle.Length == 0
                || SearchNormalization.NormalizeForSearch(module.DisplayedName).Contains(needle);

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

        // Per stanice konstantní (DNS doména / registry override) — stačí vyhodnotit jednou.
        private static readonly bool IsMlineDomainStation = MlineDomainDetector.IsDomainMline();

        private void LoadAndFilterTree(ObservableCollection<IModule> target, string treeName)
        {
            target.Clear();
            var tree = _treeGetter.GetModuleTree(Rest, treeName);
            foreach (var module in tree)
            {
                if (TryFilter(module, _services.ActiveLicence, _services.AccessTable, IsMlineDomainStation) is { } filtered)
                {
                    // Obarvit moduly dle launchovatelnosti (stub / chybí exe).
                    filtered = ModuleLaunchabilityCheck.Mark(filtered);
                    target.Add(filtered);
                }
            }
        }

        /// <summary>
        /// 5-vrstvá filtrace stromu (viz RESPONSIBILITIES § 5.2):
        /// 1) licence — <see cref="ExecutableModule.IsLicensed"/> uzel musí být v <see cref="Lic.Elements"/>;
        /// 2) hidden — <see cref="ExecutableModule.Hidden"/> uzly se vždy odfiltrují (legacy `hidden="1"`);
        /// 3) mlineOnly — mimo doménu M-line skryto, v doméně suffix
        ///    <see cref="MlineDomainDetector.Suffix"/> (legacy frmMain.vb:1204/1227);
        /// 4) aplikační přístup — <c>AccessTable.Contains(ElementId)</c> pro každý EXE uzel;
        /// 5) admin fallback — řešen v <see cref="AccessLoader"/> už při loadu (přidá konkrétní IDs).
        /// Vrstvy 1+3+4 platí i pro FAMILY (package ID — legacy BuildTreeXML gate-oval skupiny
        /// stejně jako formy, složka mimo licenci/AP/doménu skryje celou větev); FAMILY se navíc
        /// auto-skryje, pokud jsou všechny děti odfiltrované.
        /// </summary>
        internal static IModule? TryFilter(IModule module, Lic? licence, AccessTable? access = null, bool isMlineDomain = false) => module switch
        {
            ExecutableModule exe => IsAllowed(exe, licence, access, isMlineDomain)
                ? (exe.MlineOnly ? exe with { DisplayedName = exe.DisplayedName + MlineDomainDetector.Suffix } : exe)
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
            // Gate složky PŘED rekurzí — mimo licenci/AP/doménu je skrytá celá větev včetně
            // jinak povolených dětí (např. admin fallback přidá formTMList, ale bez AP
            // na package Kmenová evidence se nezobrazí — parita s legacy BuildTreeXML).
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
                DisplayedName = family.DisplayedName + (family.MlineOnly ? MlineDomainDetector.Suffix : ""),
                IsExpanded    = family.IsExpanded,
                ElementId     = family.ElementId,
                IsLicensed    = family.IsLicensed,
                MlineOnly     = family.MlineOnly,
            };
            foreach (var child in visibleChildren) filtered.NestedModules.Add(child!);
            return filtered;
        }

        // StatusBar — Identity props z MLRest (per session konstantní).
        public string Server    => Rest.Server;
        public int    RestPort  => Rest.Port;
        public string UserName  => Rest.LoginName;
        public string FullName  => Rest.FullName;
        public string Mlp       => Rest.Mlp;
        // Prázdná = chybí VERZE.INF vedle exe (typicky Release build spuštěný z VS, ne z instalace).
        public string Version   => VerzeInf.Current == "" ? "nezjištěna" : VerzeInf.Current;

        /// <summary>Server a port jako jeden údaj status baru (dva popisky vedle sebe byly zbytečný šum).</summary>
        public string ConnectionText => $"{Server}:{RestPort}";

        /// <summary>Jméno v status baru; MLRest nemusí plné jméno vrátit, pak padáme na login.</summary>
        public string UserDisplay => string.IsNullOrEmpty(FullName) ? UserName : FullName;

        private ReleaseNoticeResult? _releaseNotice;
        /// <summary>Výsledek notice-only porovnání s release serverem M-line (plní AppBootstrapper po startu).</summary>
        public ReleaseNoticeResult? ReleaseNotice
        {
            get => _releaseNotice;
            set
            {
                if (Set(ref _releaseNotice, value))
                {
                    OnPropertyChanged(nameof(NewVersionText));
                    OnPropertyChanged(nameof(NewVersionTooltip));
                    OnPropertyChanged(nameof(UpToDateText));
                }
            }
        }
        // Krátký text drží šířku chipu v status baru; plná věta je v jeho tooltipu.
        public string NewVersionText => _releaseNotice?.Outcome == ReleaseNoticeOutcome.NewVersionAvailable
            ? $"Nová verze {_releaseNotice.Version}" : "";
        public string NewVersionTooltip => _releaseNotice?.Outcome == ReleaseNoticeOutcome.NewVersionAvailable
            ? $"K dispozici je nová verze {_releaseNotice.Version} (nainstalovaná {Version})." : "";
        public string UpToDateText => _releaseNotice?.Outcome == ReleaseNoticeOutcome.UpToDate
            ? "Jde o nejnovější verzi." : "";

        public ObservableCollection<IModule> MainMenuTree { get; }
        public ObservableCollection<IModule> SystemAdminTree { get; }
        public ObservableCollection<IModule> PartnerSolutionsTree { get; }

        /// <summary>Ořezaný strom jen s oblíbenými (ruční ★) — zachovává hierarchii hlavních stromů.</summary>
        public ObservableCollection<IModule> FavoritesTree { get; }

        /// <summary>Žebříček nejpoužívanějších modulů (automaticky dle počtu spuštění × recency).</summary>
        public ObservableCollection<ExecutableModule> UsedModules { get; }

        /// <summary>Pro empty-state coaching v záložce Oblíbené.</summary>
        public bool HasNoFavorites => FavoritesTree.Count == 0;

        private IModule? _selectedModule;

        /// <summary>
        /// Aktuálně vybraný uzel viditelného stromu (nastavuje code-behind ze <c>SelectedItemChanged</c> /
        /// přepnutí záložky). Řídí toolbar akce (oblíbené, výchozí okno) — na kolekcích/ListBoxu <c>null</c>.
        /// </summary>
        public IModule? SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (!Set(ref _selectedModule, value)) return;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanSetStartup));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Je ve stromu vybraný uzel? Řídí IsEnabled toolbar tlačítka oblíbených.</summary>
        public bool HasSelection => _selectedModule != null;

        /// <summary>Lze vybraný uzel nastavit jako výchozí okno? (jen spustitelný modul s ElementId).</summary>
        public bool CanSetStartup => _selectedModule is ExecutableModule { ElementId.Length: > 0 };

        private string _treeSearchText = "";

        /// <summary>Hledaný text pro filtrování stromů (napříč všemi záložkami). Prázdný = bez filtru.</summary>
        public string TreeSearchText
        {
            get => _treeSearchText;
            set { if (Set(ref _treeSearchText, value)) ScheduleSearch(); }
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

        // Per-user × IDFa stav (plocha + používanost). _modulePrefs se re-vytváří v RebuildSessionState
        // (UserPrefs se může po switch-user vyměnit). Stromové leaf instance se sdílí napříč kolekcemi.
        private ModulePreferencesService _modulePrefs = null!;
        private readonly ModuleShortcutsService _shortcuts;
        // Guard: potlačí auto-sync handler kolekce plochy během Seed / hromadné změny (family toggle).
        private bool _suppressFavoritesSync;
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

        // Legacy launche z menu se stejným overlayem jako spouštění ze stromu.
        private async Task LaunchModeless(string exe, string formFqn)
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(exe, formFqn);
        }

        private async Task LaunchDialog(string exe, string formFqn)
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsDialogAsync(exe, formFqn);
        }

        // Přepnutí oblíbeného přes sdílenou službu (stejný zdroj jako miniaplikace Oblíbené). EXE =
        // toggle jednoho; FAMILY = hromadně (přidá vše, pokud nejsou všechny oblíbené; jinak vše odebere).
        // Změna se přes _shortcuts propíše i do miniaplikace; SyncFavorites přestaví strom + uloží.
        private void ToggleFavorite(IModule module)
        {
            switch (module)
            {
                case ExecutableModule { ElementId: { Length: > 0 } id } exe:
                    ApplyFavoritesChange(() =>
                    {
                        if (_shortcuts.Contains(id)) _shortcuts.Remove(exe);
                        else _shortcuts.Add(exe);
                    });
                    break;

                case ModuleFamily family:
                    var exes = Flatten(family).Where(e => e.ElementId.Length > 0).ToList();
                    if (exes.Count == 0) return;
                    bool allFav = exes.All(e => _shortcuts.Contains(e.ElementId));
                    ApplyFavoritesChange(() =>
                    {
                        foreach (var e in exes)
                            if (allFav) _shortcuts.Remove(e);
                            else _shortcuts.Add(e);
                    });
                    break;
            }
        }

        // Hromadná změna oblíbených: potlač per-item auto-sync a přestav/ulož jednou na konci.
        private void ApplyFavoritesChange(Action mutate)
        {
            _suppressFavoritesSync = true;
            mutate();
            _suppressFavoritesSync = false;
            SyncFavorites();
        }

        // Přestaví záložku Oblíbené + agregační flagy rodin a uloží plochu. Volá se po každé změně
        // členství (tree toggle i změny z miniaplikace přes CollectionChanged).
        private void SyncFavorites()
        {
            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
            PersistShortcuts();
        }

        // Uloží plochu (druh, identita, pozice). Volá se při změně členství i při přesunu dlaždice.
        private void PersistShortcuts() => _ = _modulePrefs?.SaveShortcutsAsync(_shortcuts.Persistable());

        // Spuštění z dlaždice. Modul hlavní nabídky jde přes OpenModule (usage + overlay); ostatní
        // druhy zástupců (tiskové sestavy, pohledy DS) shell zatím spustit neumí - viz
        // docs/DESKTOP_SHORTCUTS.md, kanál spuštění přes EDISONMAIN2API.
        private void LaunchShortcut(DesktopShortcut shortcut)
        {
            if (shortcut.Module is { } module) OpenModuleCommand.Execute(module);
            else PopupDisplayer.Info("Tento typ zástupce zatím nelze z plochy spustit.");
        }

        // Oblíbené = položky plochy, které jsou modulem hlavní nabídky (★ ve stromu jiné druhy nezná).
        private HashSet<string> FavoriteIdSet() =>
            [.. _shortcuts.Items.Where(i => i.Kind == ShortcutKind.MenuModule).Select(i => i.ElementId)];

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

        // Startup window id (EDISFormEnum) → název modulu ve stromu. ElementId je totéž numerické
        // id jako uložené výchozí okno; pro značení stromu i dialog „Vlastnosti uživatele".
        private string? ResolveModuleName(int formId) =>
            AllExecutables().FirstOrDefault(e => e.ElementId == formId.ToString())?.DisplayedName;

        // Text výchozího okna pro dialog (server ho nezná — dodáváme z modulového stromu).
        // Prázdné = nenastaveno (pole zůstane prázdné, kontext dá helpText pole).
        private string StartupWindowDisplayText() =>
            DefaultWindowId == 0 ? "" : ResolveModuleName(DefaultWindowId) ?? $"#{DefaultWindowId}";

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

        // Jeden text pro obě cesty ukončení (menu i zavření okna), aby nedriftovaly.
        private const string ExitConfirmMessage =
            "Opravdu chcete ukončit aplikaci? Zavřou se všechna otevřená agendová okna.";

        private bool _closeConfirmSuppressed;

        /// <summary>Programové ukončení (force logout, licence, menu Ukončit) — Closing se už neptá.</summary>
        public void SuppressCloseConfirm() => _closeConfirmSuppressed = true;

        /// <summary>Closing shellu: false = zavření zrušit.</summary>
        public bool ConfirmShellClose()
        {
            if (!_closeConfirmSuppressed && !PopupDisplayer.Confirm(ExitConfirmMessage)) return false;
            PersistMiniAppLayout(Idfa);
            return true;
        }

        private void ShutdownWithoutConfirm()
        {
            SuppressCloseConfirm();
            Application.Current.Shutdown();
        }

        private void ExitApplication()
        {
            if (PopupDisplayer.Confirm(ExitConfirmMessage))
                ShutdownWithoutConfirm();
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
                    ShutdownWithoutConfirm();
                    return;
                case SwitchUserResult.LicenceCancelled:
                    ShutdownWithoutConfirm();
                    return;
                case SwitchUserResult.Success:
                    RebuildSessionState(outcome.Licence);
                    ReplaceMiniAppSession(Idfa);   // přihlášení jiného uživatele: stejná firma, jiná data
                    // StatusBar identity bindings refreshne na novém MLRest.
                    OnPropertyChanged(nameof(UserName));
                    OnPropertyChanged(nameof(FullName));
                    OnPropertyChanged(nameof(Mlp));
                    OnPropertyChanged(nameof(UserDisplay));
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
            Rest.QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", new CVar());
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

        // Tmavý režim — sdílená preference (%APPDATA%\m-line\common\theme.json) přes ThemeService;
        // přepne živě + propaguje do ostatních m-line procesů (TemplateDesigner) přes watcher.
        public bool IsDarkMode
        {
            get => EDControls.EDTheme.ThemeService.IsDark;
            set
            {
                if (EDControls.EDTheme.ThemeService.IsDark == value) return;
                EDControls.EDTheme.ThemeService.SetDarkMode(value);
                OnPropertyChanged();
            }
        }


        // Nastavení → Nastavení fontu — dialog v EDControls (uložení i aplikaci řeší sám).
        // Storage je shared font.json, takže ostatní m-line tooly (TemplateDesigner) přes
        // FileSystemWatcher okamžitě reagují na změnu.
        private static void ShowFontSettings()
            => FontSettingsWindow.ShowDialogFor(Application.Current?.MainWindow);

        private void OpenWebUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // Týž text jako tlačítko „Informace o verzi" v panelu Podpora — jeden zdroj pravdy.
        private void ShowAppInfo() =>
            PopupDisplayer.Info(SupportActions.BuildVersionInfo(
                MLRest, _services.AppDisplayName, _services.AppVersion));

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
        public string ChordHintText => IsChordWaiting ? "Stisknuto Ctrl+W - čekám na druhý stisk…" : "";

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
