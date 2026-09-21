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
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.ConnectionWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.UserProfileWindow;
// Alias - class i namespace se jmenují stejně, plné qualifikování by bylo ukecané.
using EDCompTestWnd = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
using UserProfileWnd = EDISONMAIN2.Windows.UserProfileWindow.UserProfileWindow;
using LogoutCountdownWnd = EDISONMAIN2.Windows.LogoutCountdownWindow.LogoutCountdownWindow;
using ChangelogWnd = EDISONMAIN2.Windows.ChangelogWindow.ChangelogWindow;
using MiniAppPickerWnd = EDISONMAIN2.Windows.MiniAppPickerWindow.MiniAppPickerWindow;
using ConnectionWnd = EDISONMAIN2.Windows.ConnectionWindow.ConnectionWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Name, string Database)
    {
        // Stejný formát jako display přepínače firem (EDFilterComboBox „Key - Label"),
        // aby confirm dialog a ToolTip mluvily o téže položce stejně.
        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Id} - {Name}";
    }

    class EdisonMainViewModel : EDViewModel
    {
        private readonly ISessionDataLoader _sessionLoader;
        private readonly CompanySwitcher _switcher;
        private readonly Func<SwitchUserOutcome> _switchUser;
        private readonly ILocalPrefs _prefs;
        private readonly Func<IReadOnlyList<RunningModuleInfo>> _getActiveModules;
        // Vynucené ukončení aplikace (ShellShutdown.Force) - obchází potvrzovací dialog shellu.
        private readonly Action _forceShutdown;
        private readonly MiniAppCatalog _miniAppCatalog;
        // Manager žije až po AttachMiniAppHost (potřebuje DockingManager z view) - do té doby null.
        private MiniAppManager? _miniAppManager;
        // Přístup k šířce stromu (ColumnDefinition ve view) - registruje code-behind přes AttachTreeLayout.
        private Func<double>? _getTreeWidth;
        private Action<double>? _applyTreeWidth;

        public ICommand OpenModuleCommand { get; }
        /// <summary>Spuštění zástupce mimo hlavní nabídku (tisková sestava, pohled DS) přes hosta.</summary>
        public ICommand LaunchShortcutCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand OpenLogoutCountdownTestCommand { get; }
        public ICommand TestActionOverlayCommand { get; }
        /// <summary>Varianty overlaye „Akce probíhá" pro menu Test funkcí.</summary>
        public IReadOnlyList<OverlayTestVariant> ActionOverlayVariants { get; } =
            [new("Kolečko", null),
             .. AgendaSprites.AllSets.Select(s => new OverlayTestVariant(AgendaSprites.LabelFor(s), s))];
        public ICommand TriggerErrorCommand { get; }
        /// <summary>Menu „Test funkcí" jen pro vývoj a stanice M-line - u zákazníka by „Vyvolat chybu" založilo tiket.</summary>
        public bool IsTestMenuVisible => InternalDiagnostics.Enabled;
        public ICommand OpenTemplateDesignerCommand { get; }
        public ICommand OpenLegacyShellCommand { get; }
        public ICommand CloseAllCommand { get; }
        public ICommand OpenMiniAppCommand { get; }
        public ICommand CloseMiniAppsCommand { get; }
        public ICommand ResetMiniAppsCommand { get; }

        // Hlavní menu (replikuje legacy frmNoMDI.mnuMain).
        public ICommand ConnectToDatabaseCommand { get; }
        public ICommand RunEdisonCopyCommand { get; }
        public ICommand ExitCommand { get; }
        // Přepínač firem v horní liště (setter SelectedCompanyId ho spouští za binding).
        public ICommand SwitchCompanyCommand { get; }
        public ICommand LoginAsAnotherUserCommand { get; }
        public ICommand ShowUserProfileCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        // Kontextové menu stromu - výchozí okno (auto-open po loginu)
        public ICommand SetStartupWindowCommand { get; }
        public ICommand ClearStartupWindowCommand { get; }
        // Toolbar stromu - rozbalit/sbalit celý aktivní strom + rozbalit/sbalit podstrom rodiny.
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandSubtreeCommand { get; }
        public ICommand CollapseSubtreeCommand { get; }
        public ICommand ShowBgTasksCommand { get; }
        public ICommand RefreshLicenceCommand { get; }
        public ICommand ShowUsersLocksCommand { get; }
        public ICommand ShowUsersActivityCommand { get; }
        public ICommand FontSettingsCommand { get; }
        public ICommand ShowLanguageSettingsCommand { get; }
        public ICommand ResetFormsCommand { get; }
        public ICommand ShowInfoCommand { get; }
        public ICommand ShowShellChangelogCommand { get; }
        public ICommand ShowModuleChangelogCommand { get; }
        public ICommand OpenHelpdeskWebCommand { get; }
        public ICommand RefreshRunningModulesCommand { get; }
        public ICommand ActivateModuleCommand { get; }

        /// <summary>Otevře okno s jednoúrovňovým výběrem miniaplikace.</summary>
        public ICommand PickMiniAppCommand { get; }

        /// <summary>
        /// Napojí docking host (z view vrstvy) → vytvoří <see cref="MiniAppManager"/> a obnoví
        /// uložený layout aktuální firmy. Volá code-behind shellu po načtení okna.
        /// </summary>
        public void AttachMiniAppHost(IMiniAppHost host)
        {
            _miniAppManager = new MiniAppManager(host, _miniAppCatalog);
            RestoreMiniAppLayout(Idfa);
        }

        /// <summary>
        /// Napojí přístup k šířce stromu z view (ColumnDefinition). Šířka se persistuje per IDFa
        /// společně s layoutem miniaplikací. Volat **před** <see cref="AttachMiniAppHost"/> - ten
        /// spouští první restore, který už šířku aplikuje.
        /// </summary>
        public void AttachTreeLayout(Func<double> getWidth, Action<double> applyWidth)
        {
            _getTreeWidth = getWidth;
            _applyTreeWidth = applyWidth;
        }

        // Výběr miniaplikace je vlastní okno, ne vnořené submenu - katalog roste a seskupení
        // per provider se v menu neosvědčilo (HD 13132/F2).
        private void PickMiniApp()
        {
            var picker = new MiniAppPickerWnd(_miniAppCatalog, _miniAppManager?.Targets ?? []);
            if (WindowNavigator.ShowDialog(picker) == true && picker.Picked is { } descriptor)
                _miniAppManager?.Open(descriptor, picker.Placed);
        }

        // Výchozí rozložení: Plocha jako jediný sloupec přes celou plochu docku. Podpora tu
        // není - kanál IT → uživatelé nese fixní pruh v shellu, mimo dock.
        private void OpenDefaultMiniApps()
        {
            var desktop = _miniAppCatalog.Find(DesktopMiniAppProvider.DesktopId);
            _miniAppManager?.OpenDefaultLayout(desktop is null ? [] : [desktop], bottomRow: null);
        }

        // Plocha musí být otevřená vždy - uložený layout ji mít nemusí (pochází z verze, kde šla
        // zavřít). Bez ní by dock zůstal prázdný a uživatel by o dlaždice se zástupci přišel.
        private void EnsureDesktopOpen()
        {
            if (_miniAppManager is null) return;
            if (_miniAppManager.OpenInstances.Any(i => i.DescriptorId == DesktopMiniAppProvider.DesktopId)) return;
            if (_miniAppCatalog.Find(DesktopMiniAppProvider.DesktopId) is { } desktop)
                _miniAppManager.Open(desktop);
        }

        // Po dobu výměny session (persist staré → CloseAll → async rebuild → restore nové) nesmí
        // nikdo layout ukládat: dock je mezitím prázdný a Idfa už ukazuje na novou firmu, takže
        // zavření shellu během rebuildu (nebo force logout) by jí přepsalo uložený layout.
        private bool _sessionSwapInProgress;

        private void PersistMiniAppLayout(string idfa)
        {
            if (_miniAppManager is null || _sessionSwapInProgress) return;
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
            // Šířka stromu (nezávislá na tom, zda je uložený layout miniaplikací) - bez uložené
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
                EnsureDesktopOpen();
            }
            catch
            {
                // Poškozený / nekompatibilní uložený layout nesmí shodit start shellu (letí z Loaded).
                // Zahodíme ho; další uložení (close/switch) zapíše čistý.
                _prefs.MiniAppLayouts?.Remove(idfa);
                _prefs.Save();
                // Po zahozeném layoutu postavit výchozí, ať dock nezůstane prázdný. Vlastní guard -
                // stav docku po nezdařeném Deserialize je nedefinovaný a druhý pád by shodil start.
                try { OpenDefaultMiniApps(); }
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
            IModuleTreeLoader treeLoader,
            Lic? initialLicence,
            CompanySwitcher switcher,
            Func<SwitchUserOutcome> switchUser,
            ILocalPrefs prefs,
            Func<IReadOnlyList<RunningModuleInfo>> getActiveModules,
            Action closeAllWindows,
            Action forceShutdown,
            MiniAppCatalog miniAppCatalog,
            ModuleShortcutsService shortcuts,
            IChangelogSource? changelogSource,
            IRestCallRunner? restRunner = null)
        {
            _switcher = switcher;
            _switchUser = switchUser;
            _prefs = prefs;
            _getActiveModules = getActiveModules;
            _forceShutdown = forceShutdown;
            _miniAppCatalog = miniAppCatalog;
            // Null = novinky vypnuté (testy); produkci dává InfChangelogSource AppBootstrapper.
            _changelogSource = changelogSource;
            _shortcuts = shortcuts;

            OpenModuleCommand        = CommandAsync<IModule>(OpenModuleAsync);
            LaunchShortcutCommand    = CommandAsync<DesktopShortcut>(LaunchShortcutViaHostAsync);
            ToggleFavoriteCommand    = Command<IModule>(ToggleFavorite);
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show(new EDInputFormTestWindow()));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show(new EDListTestWindow()));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog(new EDCompTestWnd()));
            // Test trigger pro cooperative logout - countdown se po vypršení sám zavře (onExpired no-op).
            OpenLogoutCountdownTestCommand = Command(() => WindowNavigator.Show(new LogoutCountdownWnd(15, () => { })));
            TestActionOverlayCommand = CommandAsync<OverlayTestVariant>(v => TestActionOverlayAsync(v.SpriteKey));
            // Test trigger pro error popup - výjimka projde RelayCommand handlerem do EDErrorPopup.
            TriggerErrorCommand = Command(() => throw new InvalidOperationException("Testovací výjimka z toolbaru."));
            OpenTemplateDesignerCommand = Command(OpenTemplateDesigner);
            OpenLegacyShellCommand = Command(OpenLegacyShell);
            CloseAllCommand          = Command(() => { PersistMiniAppLayout(Idfa); closeAllWindows(); _miniAppManager?.CloseAll(); });
            OpenMiniAppCommand       = Command<MiniAppDescriptor>(d => _miniAppManager?.Open(d));
            PickMiniAppCommand       = Command(PickMiniApp);
            // Plocha zůstává - zavřít ji nejde a prázdný dock nemá uživateli co dát.
            CloseMiniAppsCommand     = Command(() => _miniAppManager?.CloseClosable());
            // Reset = zavřít vše (proběhne OnClosed úklid VM) a znovu otevřít výchozí sadu.
            ResetMiniAppsCommand     = Command(() => { _miniAppManager?.CloseAll(); OpenDefaultMiniApps(); });

            // EDISON menu
            ConnectToDatabaseCommand      = Command(ConnectToDatabase);
            RunEdisonCopyCommand          = Command(RunEdisonCopy);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = CommandAsync(LoginAsAnotherUserAsync);
            ShowUserProfileCommand        = Command(() => WindowNavigator.ShowDialog(new UserProfileWnd(StartupWindowDisplayText())));
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

            SwitchCompanyCommand          = CommandAsync<int>(SwitchCompanyAsync);

            // Nástroje menu - Správa zámků a Aktivita uživatelů jsou v USERS.exe (retrofitted).
            ShowBgTasksCommand            = CommandAsync(() => LaunchDialogAsync("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList"));
            RefreshLicenceCommand         = CommandAsync(RefreshLicenceAsync);
            // Admin-only - legacy frmNoMDI.vb:1101-1102 gatuje obě menu items přes UserIsAdmin.
            ShowUsersLocksCommand         = CommandAsync(() => LaunchModelessAsync("USERS.exe", "ALVA.USERS.formUSERSLocks"), () => _services.IsAdmin);
            ShowUsersActivityCommand      = CommandAsync(() => LaunchModelessAsync("USERS.exe", "ALVA.USERS.formUSERSActivity"), () => _services.IsAdmin);

            // Nastavení menu
            FontSettingsCommand           = Command(ShowFontSettings);
            ShowLanguageSettingsCommand   = CommandAsync(() => LaunchDialogAsync("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINLanguageList"));
            ResetFormsCommand             = Command(ResetForms);

            // Nápověda menu
            ShowInfoCommand               = Command(ShowAppInfo);
            ShowShellChangelogCommand     = CommandAsync(() => ShowChangelogForAsync("EDISONMAIN", "EDISON"));
            ShowModuleChangelogCommand    = CommandAsync<IModule>(ShowModuleChangelogAsync);
            OpenHelpdeskWebCommand        = Command(() => OpenWebUrl("http://podpora.m-line.cz"));

            // Spuštěné moduly menu - dynamický seznam, refresh při SubmenuOpened.
            RefreshRunningModulesCommand  = Command(RefreshRunningModules);
            ActivateModuleCommand         = Command<RunningModuleInfo>(m => m.Activate());

            MainMenuTree = [];
            SystemAdminTree = [];
            PartnerSolutionsTree = [];
            FavoritesTree = [];
            UsedModules = [];
            // Pořadí = pořadí záložek v UI. Přidání záložky je změna jen tady (+ případný control v XAML).
            TreeTabs =
            [
                new("main",      "Hlavní nabídka",     "EDIcons.Home",      MainMenuTree,         Expandable: true,  ServerLoaded: true),
                new("system",    "Správa systému",     "EDIcons.Gears",     SystemAdminTree,      Expandable: true,  ServerLoaded: true),
                new("partners",  "Partnerská řešení",  "EDIcons.Component", PartnerSolutionsTree, Expandable: true,  ServerLoaded: true),
                new("favorites", "Oblíbené",           "EDIcons.Star",      FavoritesTree,        Expandable: true,  ServerLoaded: false),
                new("used",      "Používané",          "EDIcons.History",   null,                 Expandable: false, ServerLoaded: false),
            ];
            _selectedTab = TreeTabs[0];
            _sourceTrees = [.. TreeTabs.Where(t => t.ServerLoaded).Select(t => (t.Key, t.Tree!))];
            // Runner drží politiku „klon per paralelní volání"; testy podstrčí sekvenční variantu.
            _sessionLoader = new SessionDataLoader(restRunner ?? new RestCallRunner(() => Rest),
                                                  treeLoader, [.. _sourceTrees.Select(t => t.Name)]);

            // GetCompanyList už vrací řazené podle Name & Id (SortedDictionary v ALVA.EDISONAPI.Database).
            Companies = [.. companies.Select(c => new CompanyItem(c.Id, c.Name ?? "", c.Database))];
            // Index musí odpovídat MLRest.Idfa - tam má AppBootstrapper vytvořený ACTIVITY zámek.
            _selectedCompanyIndex = Companies.Select((c, i) => (c, i)).First(x => x.c.Id == mlRest.Idfa).i;

            _services.MLRest = mlRest;

            // Změny plochy z miniaplikace (DnD add, remove, reorder) → přestav strom + persist.
            // Seed při loadu je pod _suppressFavoritesSync (nemá znovu ukládat načtené).
            _shortcuts.Items.CollectionChanged += (_, _) => { if (!_suppressFavoritesSync) SyncFavorites(); };
            // Přesun dlaždice (reorder v miniaplikaci) → persistuj pozice (bez změny členství).
            _shortcuts.PositionsChanged += PersistShortcuts;
            // Spuštění z dlaždice jde přes OpenModuleAsync (usage + overlay), ne duplikovaně v miniaplikaci.
            _shortcuts.LaunchRequested += LaunchShortcut;
            // Novinky z dlaždice: klíč nese sama položka (i u zástupců mimo strom), modul se nedohledává.
            _shortcuts.ChangelogRequested += s => _ = ShowChangelogForAsync(s.ExecutablePath, s.DisplayedName);
            // Agenda mohla přidat zástupce na plochu (píše přímo do Caché) - po jejím ukončení
            // si blob přečteme znovu, jinak by dlaždice naskočila až po restartu shellu.
            WindowNavigator.LegacyModuleExited += () => _ = ReloadShortcutsAsync();

            _initialLicence = initialLicence;
        }

        private readonly Lic? _initialLicence;
        private Task? _initialization;

        /// <summary>
        /// První naplnění session-scoped stavu. Ctor ho záměrně nedělá - shell se ukáže hned
        /// a data dorazí pod overlayem. Volá code-behind z <c>ContentRendered</c> PŘED
        /// <see cref="OpenStartupWindowIfAny"/> (to potřebuje hotové stromy). Idempotentní.
        /// </summary>
        public Task InitializeAsync() => _initialization ??= RebuildSessionStateAsync(_initialLicence);

        /// <summary>
        /// Po změně session state (initial load, switch firmy, switch uživatele, manual refresh
        /// licence) přebuduje VEŠKERÝ session-scoped derived state. Jediný bod rozšíření pro budoucí
        /// session-scoped data (ECL, user settings, font scheme per firma, …) - přidá se sem,
        /// callsity nepotřebují vědět.
        /// </summary>
        private async Task RebuildSessionStateAsync(Lic? licence)
        {
            _services.ActiveLicence = licence;
            // _modulePrefs se vytváří tady (ne v ctoru), aby reflektoval výměnu UserPrefs po switch-user.
            _modulePrefs = new ModulePreferencesService(UserPrefs);
            // Nová instance per session: resetuje per-session dedup popupů (snooze je per-user × IDFa).
            _changelogPrompt = _changelogSource is { } src
                ? new ChangelogPromptService(src, _modulePrefs, ShowChangelogWindow)
                : null;

            using (ActionInProgress())
            {
                var data = await _sessionLoader.LoadAsync(licence, _services.IsAdmin, _modulePrefs);
                ApplySessionData(data);
            }

            // Nové instance uzlů mají IsVisibleBySearch=true - trvající hledaný text je nutné znovu aplikovat.
            if (_treeSearchText.Trim().Length > 0) ApplySearch();
        }

        /// <summary>
        /// Promítne načtená data do VM. Musí zůstat SYNCHRONNÍ: guardy <c>_suppressXxx</c> níž
        /// spoléhají na nepřerušený běh, přes <c>await</c> uvnitř by přestaly platit. Zároveň
        /// tím platí, že selhání loadu nechá předchozí stav (stromy, oblíbené) nedotčený.
        /// </summary>
        private void ApplySessionData(SessionData data)
        {
            _services.AccessTable = data.AccessTable;
            // Uzly se nahradí novými instancemi, takže zapamatovaný výběr míří do prázdna.
            ForgetSelection();
            foreach (var (name, tree) in _sourceTrees)
                FillFilteredTree(tree, data.Trees[name]);
            ApplyPersonalization(data);
            ApplyStartupWindow(data.DefaultWindowId);
            _changelogPrompt?.SeedSnooze(data.ChangelogSnooze);
        }

        /// <summary>
        /// Zahodí session-scoped stav do prázdna. Pro cesty, kde už se session přepnula (jiná firma
        /// / uživatel) a load selhal - držet dál data předchozí session by znamenalo spustitelné
        /// moduly mimo licenci nové firmy a zápis cizích preferencí do jejích blobů.
        /// </summary>
        private void ClearSessionState() => ApplySessionData(new SessionData(
            new AccessTable([], isAllAccess: false),
            _sourceTrees.ToDictionary(t => t.Name, _ => (IReadOnlyList<IModule>)[]),
            Shortcuts: null, Usage: [], Expansion: null, SelectedTab: null, ChangelogSnooze: [], DefaultWindowId: 0));

        private void ForgetSelection()
        {
            _selectionByTab.Clear();
            SelectedModule = null;
        }

        private int _defaultWindowId;
        /// <summary>Id uživatelova výchozího okna (EDISFormEnum) pro aktuální IDFa; 0 = žádný.</summary>
        public int DefaultWindowId { get => _defaultWindowId; private set => Set(ref _defaultWindowId, value); }

        // Výchozí okno je per IDFa → přebírá se při každém rebuildu (jiná firma může mít jiný default).
        private void ApplyStartupWindow(int defaultWindowId)
        {
            DefaultWindowId = defaultWindowId;
            ApplyStartupWindowMark();
        }

        // Přeznačí uzly stromu - právě jeden (nebo žádný) modul má IsStartupWindow=true.
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
        /// Po prvním vykreslení shellu otevře uživatelův výchozí okno - jednou za běh (ne po
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

        // Oblíbené + používanost jsou per-user × IDFa → aplikují se po (pře)stavbě stromů.
        /// <summary>
        /// Přenačte plochu z Caché. Agendy zapisují zástupce přímo na server (nemají na shell
        /// spojení), takže po jejich ukončení může mít blob víc, než co drží tenhle shell.
        /// Ticho při chybě je záměr - kvůli nedostupnému serveru nemá smysl obtěžovat uživatele,
        /// který zrovna zavřel agendu.
        /// </summary>
        private async Task ReloadShortcutsAsync()
        {
            if (_modulePrefs is null) return;
            var stored = await _modulePrefs.LoadShortcutsAsync();
            // Selhání loadu: v paměti zůstává stav téže session (agenda mohla přidat, nikdy neubírá);
            // seed prázdnem by ho zahodil a další toggle prázdno zapsal do Caché.
            if (stored is null) return;
            SeedShortcuts(stored);
            _shortcutsPersistable = true;

            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
        }

        private void SeedShortcuts(List<DesktopShortcutDto> stored)
        {
            var labels = BuildModuleLabels(_sourceTrees.Select(t => t.Tree));
            // Seed pod suppressem → CollectionChanged handler nespustí zbytečný re-save načteného.
            _suppressFavoritesSync = true;
            _shortcuts.Seed(AllExecutables(), stored, exe =>
                labels.TryGetValue(ModuleKey(exe.ExecutablePath, exe.FormName, exe.InputParam), out var label)
                    ? label.Path : "");
            _suppressFavoritesSync = false;
        }

        private void ApplyPersonalization(SessionData data)
        {
            _usage = data.Usage;
            _expansion = data.Expansion;

            // Seed sdílené služby z uložených položek (nastaví IsFavorite na sdílených instancích).
            // Null = load plochy selhal: zobrazit prázdno (data předchozí session sem nepatří), ale
            // nepersistovat, dokud se blob nenačte - jinak by první toggle přepsal uživatelovu plochu.
            SeedShortcuts(data.Shortcuts ?? []);
            _shortcutsPersistable = data.Shortcuts is not null;

            RefreshFamilyFavoriteFlags();
            RebuildFavoritesTree();
            RebuildUsedList();

            // Obnoví uložené rozbalení a napojí persistenci na VŠECH stromech (vč. Oblíbených).
            // Suppress brání zápisu během programového apply.
            using (SuppressExpansionSave())
                ApplyTreeExpansion();

            // Poslední otevřená záložka (per-user × IDFa); bez uložené volby výchozí dle oblíbených.
            _suppressTabSave = true;
            SelectedTab = data.SelectedTab is { } key ? TabByKey(key) : DefaultTreeTab();
            _suppressTabSave = false;
        }

        private void RebuildFavoritesTree()
        {
            FavoritesTree.Clear();
            foreach (var root in BuildFavoritesTree(_sourceTrees.Select(t => t.Tree), FavoriteIdSet()))
                FavoritesTree.Add(root);

            // Po přestavbě (i mimo load - po toggle) znovu aplikuj/napoj rozbalení Oblíbených.
            using (SuppressExpansionSave())
            {
                ApplyExpansion(FavoritesTree, FavoritesTab.Key);

                // PruneFamily staví nové rodinné wrappery (IsVisibleBySearch=true) - při aktivním hledání přefiltrovat.
                var searchText = _treeSearchText.Trim();
                if (searchText.Length > 0)
                    foreach (var root in FavoritesTree)
                        ModuleTreeState.ApplySearchToModule(root, searchText);
            }

            OnPropertyChanged(nameof(HasNoFavorites));
        }

        private void RebuildUsedList()
        {
            UsedModules.Clear();
            foreach (var module in BuildUsedList(_sourceTrees.Select(t => t.Tree), _usage, UsedModulesCap))
                UsedModules.Add(module);
        }

        // Klíč uzlu = "<tree>:<index-cesta>" (např. "main:5.2") - stabilní vůči přejmenování i suffixům
        // z ModuleLaunchabilityCheck, na rozdíl od cesty přes DisplayedName. Save běží na každou změnu
        // rozbalení (IDFa je tehdy stabilní), proto switch/close nepotřebují zvláštní obsluhu.

        private void ApplyTreeExpansion()
        {
            foreach (var (name, tree) in ExpandableTrees)
                ApplyExpansion(tree, name);
        }

        /// <summary>
        /// Potlačí persistenci rozbalení po dobu programové změny stromu. Refcount, ne bool:
        /// cesty se vnořují (přestavba Oblíbených uvnitř session loadu, re-filtrace při hledání)
        /// a plochý reset na false by vnitřní blok odemkl dřív, než skončí ten vnější.
        /// <c>internal</c> kvůli testu na vnoření.
        /// </summary>
        internal IDisposable SuppressExpansionSave()
        {
            _expansionSaveSuppressed++;
            return new ExpansionSaveScope(this);
        }

        private sealed class ExpansionSaveScope(EdisonMainViewModel vm) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                vm._expansionSaveSuppressed--;
            }
        }

        // Strom aktivní záložky; Používané (ListBox) strom nemá → null.
        private ObservableCollection<IModule>? ActiveExpandableTree() =>
            SelectedTab.Expandable ? SelectedTab.Tree : null;

        // Hromadné rozbalení/sbalení celého aktivního stromu. Suppress + jeden save na konci
        // (jinak by každá rodina zvlášť triggerovala SaveExpansionAsync - vzor jako ApplySearch).
        private void SetActiveTreeExpanded(bool expanded)
        {
            if (ActiveExpandableTree() is not { } tree) return;
            using (SuppressExpansionSave())
            {
                foreach (var module in tree)
                    ModuleTreeState.SetExpandedRecursive(module, expanded);
            }
            _ = _modulePrefs?.SaveExpansionAsync(CollectExpandedKeys());
        }

        // Rozbalit/sbalit rodinu i její potomky (kontextové menu „úroveň").
        private void SetSubtreeExpanded(IModule module, bool expanded)
        {
            if (module is not ModuleFamily) return;
            using (SuppressExpansionSave())
                ModuleTreeState.SetExpandedRecursive(module, expanded);
            _ = _modulePrefs?.SaveExpansionAsync(CollectExpandedKeys());
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
                family.IsExpanded = saved.Contains(ModuleTreeState.ExpansionPathKey(treeName, path));

            family.PropertyChanged -= OnFamilyPropertyChanged;
            family.PropertyChanged += OnFamilyPropertyChanged;

            for (int i = 0; i < family.NestedModules.Count; i++)
                ApplyExpansionNode(family.NestedModules[i], treeName, [.. path, i]);
        }

        private void OnFamilyPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModuleFamily.IsExpanded) && _expansionSaveSuppressed == 0)
                _ = _modulePrefs?.SaveExpansionAsync(CollectExpandedKeys());
        }

        private List<string> CollectExpandedKeys()
        {
            var keys = new List<string>();
            foreach (var (name, tree) in ExpandableTrees)
                ModuleTreeState.CollectExpanded(tree, name, keys);
            return keys;
        }

        private DispatcherTimer? _searchDebouncer;

        // Filtrace se pouští ~150 ms po posledním stisku - synchronní průchod všemi stromy
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
            using (SuppressExpansionSave())
            {
                foreach (var (_, tree) in ExpandableTrees)
                    foreach (var root in tree)
                        ModuleTreeState.ApplySearchToModule(root, text);
            }

            if (text.Length == 0) ApplyTreeExpansion();
        }

        /// <summary>
        /// První modul odpovídající hledání v aktivní záložce, nebo <c>null</c> (prázdné hledání,
        /// záložka bez stromu, žádný nález). Okno na něj po Enteru v poli hledání přenese fokus.
        /// </summary>
        internal ExecutableModule? FirstSearchResult()
        {
            if (_treeSearchText.Trim().Length == 0) return null;
            // Enter hned po psaní by jinak četl IsVisibleBySearch z předchozího běhu - filtrace
            // je debouncovaná a čekající tick ještě nedoběhl.
            if (_searchDebouncer is { IsEnabled: true })
            {
                _searchDebouncer.Stop();
                ApplySearch();
            }
            return SelectedTab.Tree is { } tree ? ModuleTreeState.FirstVisibleExecutable(tree) : null;
        }

        // Clear až tady (po úspěšném loadu, ne před requestem) - selhání jinak nechá záložku
        // trvale prázdnou a při přepnutí firmy zahodí i strom, který byl platný.
        private void FillFilteredTree(ObservableCollection<IModule> target, IReadOnlyList<IModule> tree)
        {
            target.Clear();
            foreach (var module in tree)
            {
                if (ModuleTreeState.TryFilter(module, _services.ActiveLicence, _services.AccessTable,
                                              ModuleTreeState.IsMlineDomainStation) is { } filtered)
                {
                    // Obarvit moduly dle launchovatelnosti (stub / chybí exe).
                    filtered = ModuleLaunchabilityCheck.Mark(filtered);
                    target.Add(filtered);
                }
            }
        }

        // StatusBar - Identity props z MLRest (per session konstantní).
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

        /// <summary>Ořezaný strom jen s oblíbenými (ruční ★) - zachovává hierarchii hlavních stromů.</summary>
        public ObservableCollection<IModule> FavoritesTree { get; }

        /// <summary>Žebříček nejpoužívanějších modulů (automaticky dle počtu spuštění × recency).</summary>
        public ObservableCollection<ExecutableModule> UsedModules { get; }

        /// <summary>Pro empty-state coaching v záložce Oblíbené.</summary>
        public bool HasNoFavorites => FavoritesTree.Count == 0;

        private IModule? _selectedModule;
        // Výběr si pamatujeme per záložka: stromy zůstávají živé, takže po návratu na záložku
        // musí toolbar akce cílit na uzel, který je v ní vidět, ne na ten z minule aktivní.
        private readonly Dictionary<string, IModule?> _selectionByTab = [];

        /// <summary>
        /// Aktuálně vybraný uzel viditelného stromu (nastavuje code-behind ze <c>SelectedItemChanged</c>).
        /// Řídí toolbar akce (oblíbené, výchozí okno) - na kolekcích/ListBoxu <c>null</c>.
        /// </summary>
        public IModule? SelectedModule
        {
            get => _selectedModule;
            set
            {
                _selectionByTab[SelectedTab.Key] = value;
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

        private TreeTab _selectedTab = null!;   // plní ctor po vytvoření TreeTabs
        private bool _suppressTabSave;

        /// <summary>
        /// Pořadí i identita záložek launcher panelu - jediný seznam, ze kterého se odvozuje
        /// tab strip, viditelnost obsahu, cíl Rozbalit/Sbalit vše i uložená volba.
        /// </summary>
        public IReadOnlyList<TreeTab> TreeTabs { get; }

        /// <summary>Aktivní záložka. TwoWay-bound; persistuje se per-user × IDFa (poslední otevřená).</summary>
        public TreeTab SelectedTab
        {
            get => _selectedTab;
            set
            {
                // TabControl při přestavbě ItemsSource krátce nastaví null - držíme poslední platnou.
                if (value == null || !Set(ref _selectedTab, value)) return;
                SelectedModule = _selectionByTab.GetValueOrDefault(value.Key);
                if (!_suppressTabSave)
                    _ = _modulePrefs?.SaveSelectedTabAsync(value.Key);
            }
        }

        // Výchozí záložka při prvním spuštění (bez uložené volby) - vždy Hlavní nabídka; existující
        // oblíbené ji nepřebíjejí, protože skok jinam než do plné nabídky uživatele překvapil.
        private TreeTab DefaultTreeTab() => TabByKey("main");

        private TreeTab TabByKey(string? key) =>
            TreeTabs.FirstOrDefault(t => t.Key == key) ?? TreeTabs[0];

        // Oblíbené se nenačítají ze serveru, ale rozbalení se jim persistuje jako ostatním.
        private TreeTab FavoritesTab => TabByKey("favorites");

        private readonly IChangelogSource? _changelogSource;
        // Auto-popup novinek (infotexty). Žije se session jako _modulePrefs; null = vypnuto (testy).
        private ChangelogPromptService? _changelogPrompt;

        // Per-user × IDFa stav (plocha + používanost). _modulePrefs se re-vytváří v každém rebuildu
        // (UserPrefs se může po switch-user vyměnit); null do prvního initu - bindingy okna jedou dřív
        // než on, takže save cesty musí být null-safe (jinak NRE z TwoWay bindingu záložky).
        private ModulePreferencesService? _modulePrefs;
        private readonly ModuleShortcutsService _shortcuts;
        // Guard: potlačí auto-sync handler kolekce plochy během Seed / hromadné změny (family toggle).
        private bool _suppressFavoritesSync;
        private Dictionary<string, UsageEntry> _usage = [];
        // Klíče rozbalených uzlů (index-cesta). null = nikdy neuloženo → ponech serverové výchozí rozbalení.
        private List<string>? _expansion;
        private int _expansionSaveSuppressed;
        private const int UsedModulesCap = 20;

        // Zdrojové stromy plněné ze serveru (per-IdFa). Jedna kolekce pro společné operace (load, filtrace,
        // oblíbené/používané, family flagy). Odvozené z TreeTabs, inicializuje je ctor.
        private readonly (string Name, ObservableCollection<IModule> Tree)[] _sourceTrees;

        // Všechny rozbalovatelné stromy = zdrojové + odvozený Oblíbené (persistence rozbalení i nad Oblíbenými).
        private IEnumerable<(string Name, ObservableCollection<IModule> Tree)> ExpandableTrees =>
            TreeTabs.Where(t => t.Expandable).Select(t => (t.Key, t.Tree!));

        public ObservableCollection<CompanyItem> Companies { get; }

        private int _selectedCompanyIndex;
        /// <summary>Vybraná firma jako čistý stav - přepnutí dělá <see cref="SwitchCompanyCommand"/>.</summary>
        public int SelectedCompanyIndex
        {
            get => _selectedCompanyIndex;
            private set
            {
                if (!Set(ref _selectedCompanyIndex, value)) return;
                RaiseSessionPropertiesChanged();
            }
        }

        // Derived properties nad vybranou firmou - jeden seznam pro přepnutí i pro odmítnutí.
        private void RaiseSessionPropertiesChanged()
        {
            OnPropertyChanged(nameof(Namespace));
            OnPropertyChanged(nameof(Idfa));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(SelectedCompanyId));
            OnPropertyChanged(nameof(SelectedCompany));
        }

        // Pustí render (overlay) před blokující synchronní prací. Guard na běžící Application,
        // ne jen na dispatcher vlákna: DispatcherTimer ve VM dispatcher testovacímu vláknu
        // vytvoří, ale nikdo ho nepumpuje - await Yield by tam nikdy nedoběhl.
        private static async Task YieldToRenderAsync()
        {
            if (Application.Current?.Dispatcher is { } d && d.CheckAccess())
                await Dispatcher.Yield(DispatcherPriority.Background);
        }

        /// <summary>
        /// Přepnutí firmy: potvrzení → <see cref="CompanySwitcher"/> (zavře okna, vymění Idfa,
        /// licence) → async rebuild session state → výměna layoutu miniaplikací. Index se nastaví
        /// teprve po <c>Accepted</c>, takže odmítnutí ani cancel nepotřebují rollback.
        /// <c>internal</c> kvůli testům - <see cref="AsyncRelayCommand"/> nemá awaitovatelný Execute.
        /// </summary>
        internal async Task SwitchCompanyAsync(int index)
        {
            if (index < 0 || index >= Companies.Count || index == _selectedCompanyIndex) return;

            var target = Companies[index];
            // Potvrzení jen když je co ztratit - bez otevřených oken je přepnutí nedestruktivní
            // (layout miniaplikací se persistuje per IDFa a při návratu obnoví).
            if (_getActiveModules().Count > 0
                && !PopupDisplayer.Confirm($"Přepnout na firmu {target.DisplayName}? Zavřou se všechna otevřená okna i miniaplikace."))
                return;

            var oldIdfa = Idfa;
            SwitchOutcome result;
            // Synchronní část (zavření oken, VerifyLic, logout staré firmy) blokuje UI thread -
            // overlay „Akce probíhá" se musí stihnout vykreslit před ní (yield). Scope nesmí
            // přesáhnout do RebuildSessionStateAsync (má vlastní; IsActionInProgress není
            // refcounted) ani pod popupy níž.
            using (ActionInProgress())
            {
                await YieldToRenderAsync();
                result = _switcher.Switch(target.Id, target.Database, _services.ActiveLicence);
            }

            if (result.Outcome != LicenceAcquireOutcome.Accepted)
            {
                // Combobox si drží cílovou firmu z vlastního editu → notifikací ho vrať na platnou.
                RaiseSessionPropertiesChanged();
                // Cancelled bez hlášky záměrně: admin odmítl licenční retry dialog, nebo okno
                // s neuloženými daty vetovalo zavření - obojí aktivní volba uživatele,
                // další popup by byl jen friction.
                if (result.Outcome == LicenceAcquireOutcome.Refused)
                    PopupDisplayer.Info("Pro tuto firmu byl překročen počet licencí. Firma nebyla přepnuta.");
                return;
            }

            // Layout staré firmy ulož a dock zavři JEŠTĚ než se Idfa posune - mezi indexem a restore
            // je await, během kterého jde shell zavřít (persist by dock staré firmy uložil pod novou).
            PersistMiniAppLayout(oldIdfa);
            _miniAppManager?.CloseAll();

            _sessionSwapInProgress = true;
            try
            {
                SelectedCompanyIndex = index;
                await RebuildSessionStateAsync(result.Licence);
            }
            catch
            {
                // Session už patří nové firmě, takže stará data jsou cizí: šla by spustit mimo její
                // licenci a fire-and-forget save by je zapsal do jejích blobů. Prázdný shell + chyba
                // je jediný konzistentní stav (recovery: Aktualizovat licenci nebo restart).
                ClearSessionState();
                throw;
            }
            finally
            {
                _sessionSwapInProgress = false;
            }
            RestoreMiniAppLayout(Idfa);
        }

        public CompanyItem SelectedCompany => Companies[_selectedCompanyIndex];

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        public string WindowTitle => $"M-line EDISON [{Idfa}]";

        /// <summary>
        /// IDFa aktuální firmy pro filtrovatelný přepínač (EDFilterComboBox.SelectedKey, TwoWay).
        /// Setter jen spustí <see cref="SwitchCompanyCommand"/> a hned notifikuje zpět, takže
        /// combobox drží potvrzenou firmu, dokud přepnutí neproběhne (příp. neselže).
        /// </summary>
        public string SelectedCompanyId
        {
            get => SelectedCompany.Id;
            set
            {
                int index = Companies.ToList().FindIndex(c => c.Id == value);
                if (index >= 0 && index != _selectedCompanyIndex && SwitchCompanyCommand.CanExecute(index))
                    SwitchCompanyCommand.Execute(index);
                OnPropertyChanged(nameof(SelectedCompanyId));
            }
        }

        /// <summary>Firmy jako nápověda pro filtrovatelný přepínač (Key = IDFa, Label = název).</summary>
        public IEnumerable<ListItem> CompanySuggestions =>
            Companies.Select(c => new ListItem(c.Id, c.Name));

        /// <summary>Jediná firma → přepínač se zdisabluje (zůstává jako indikace aktuální firmy).</summary>
        public bool HasMultipleCompanies => Companies.Count > 1;

        private async Task OpenModuleAsync(IModule module)
        {
            if (module is not ExecutableModule { ExecutablePath: { Length: > 0 } path } em) return;

            RecordUsage(em);
            // Novinky blokují otevření agendy: modální dialog před launchem, ať změny nezapadnou.
            if (_changelogPrompt is { } prompt)
                await prompt.ShowIfNewAsync(path, em.DisplayedName);

            // Overlay „akce probíhá" po dobu náběhu modulu (samostatný proces - čekáme na jeho okno).
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(path, em.FormName, inputParam: em.InputParam);
        }

        // Test funkcí: overlay s vybranou variantou na 10 s - dost na posouzení animace,
        // netřeba nic rušit ručně.
        private async Task TestActionOverlayAsync(string? spriteKey)
        {
            using (ActionInProgress(spriteKey))
                await Task.Delay(TimeSpan.FromSeconds(10));
        }

        /// <summary>Novinky shellu po loginu (EDISONMAIN.INF - obsah sdílený s legacy shellem).</summary>
        public Task ShowShellChangelogIfAnyAsync() =>
            _changelogPrompt?.ShowIfNewAsync("EDISONMAIN", "EDISON") ?? Task.CompletedTask;

        // Novinky agendy na vyžádání (kontextové menu stromu). Rodina modulů nemá exe - žádný INF.
        private Task ShowModuleChangelogAsync(IModule module) =>
            module is ExecutableModule { ExecutablePath: { Length: > 0 } path } em
                ? ShowChangelogForAsync(path, em.DisplayedName)
                : Task.CompletedTask;

        // Vynucené novinky: obcházejí snooze i session dedup, protože si o ně uživatel řekl sám.
        // Chybějící INF nesmí skončit „klik nic neudělal".
        private async Task ShowChangelogForAsync(string moduleKey, string displayName)
        {
            if (_changelogPrompt is { } prompt && await prompt.ShowAlwaysAsync(moduleKey, displayName)) return;
            PopupDisplayer.Info($"Pro „{displayName}\" nejsou k dispozici informace o změnách.");
        }

        private void ShowChangelogWindow(string title, ChangelogContent content, bool snoozed, Action<bool> onClosed)
        {
            var wnd = new ChangelogWnd(title, content, snoozed);
            // Modální: ShowIfNewAsync se vrátí až po zavření, volající tak čeká s dalším otvíráním.
            WindowNavigator.ShowDialog(wnd);
            onClosed(wnd.SnoozeChecked);
        }

        // Legacy launche z menu se stejným overlayem jako spouštění ze stromu.
        private async Task LaunchModelessAsync(string exe, string formFqn)
        {
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(exe, formFqn);
        }

        private async Task LaunchDialogAsync(string exe, string formFqn)
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

        /// <summary>Blob plochy se v této session podařilo načíst; do té doby se nesmí zapisovat (viz ApplyPersonalization).</summary>
        private bool _shortcutsPersistable;

        // Uloží plochu (druh, identita, pozice). Volá se při změně členství i při přesunu dlaždice.
        private void PersistShortcuts()
        {
            if (!_shortcutsPersistable) return;
            _ = _modulePrefs?.SaveShortcutsAsync(_shortcuts.Persistable());
        }

        // Spuštění z dlaždice. Modul hlavní nabídky jde přes OpenModuleAsync (usage + overlay); ostatní
        // druhy zástupců (tiskové sestavy, pohledy DS) pouští hostitelská agenda přes EDISONMAIN2API.
        private void LaunchShortcut(DesktopShortcut shortcut)
        {
            if (shortcut.Module is { } module) { OpenModuleCommand.Execute(module); return; }
            LaunchShortcutCommand.Execute(shortcut);
        }

        // Zástupce mimo hlavní nabídku (tisková sestava, uložený pohled): host ho místo formy
        // pustí jako proceduru agendy. Exe i formu nese sama položka - hostitelská forma nemusí
        // být ve stromu, takže se nedohledávají.
        private async Task LaunchShortcutViaHostAsync(DesktopShortcut shortcut)
        {
            if (shortcut.ExecutablePath.Length == 0 || shortcut.FormName.Length == 0)
            {
                PopupDisplayer.Info($"Zástupce „{shortcut.DisplayedName}\" nemá uloženou agendu ani formulář, "
                                    + "nelze ho spustit. Vytvořte ho znovu z modulu.");
                return;
            }
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyShortcutAsync(shortcut.ExecutablePath, shortcut.FormName,
                    ShortcutLaunchPayloadCodec.Encode(shortcut), inputParam: shortcut.InputParam);
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
            _ = _modulePrefs?.SaveUsageAsync(_usage);
        }

        // Všechny EXE instance napříč hlavními stromy. FavoritesTree i UsedModules sdílí tytéž instance,
        // takže stačí projít hlavní tři stromy (flip IsFavorite se projeví všude).
        private IEnumerable<ExecutableModule> AllExecutables() =>
            _sourceTrees.SelectMany(t => t.Tree).SelectMany(Flatten);

        // Startup window id (EDISFormEnum) → název modulu ve stromu. ElementId je totéž numerické
        // id jako uložené výchozí okno; pro značení stromu i dialog „Vlastnosti uživatele".
        private string? ResolveModuleName(int formId) =>
            AllExecutables().FirstOrDefault(e => e.ElementId == formId.ToString())?.DisplayedName;

        // Text výchozího okna pro dialog (server ho nezná - dodáváme z modulového stromu).
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
        /// z hlavních stromů - tím respektuje aktuální licenci/přístup (nezobrazený modul se přeskočí)
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

        /// <summary>Programové ukončení (force logout, licence, menu Ukončit) - Closing se už neptá.</summary>
        public void SuppressCloseConfirm() => _closeConfirmSuppressed = true;

        /// <summary>Closing shellu: false = zavření zrušit.</summary>
        public bool ConfirmShellClose()
        {
            if (!_closeConfirmSuppressed && !PopupDisplayer.Confirm(ExitConfirmMessage)) return false;
            PersistMiniAppLayout(Idfa);
            return true;
        }

        private void ExitApplication()
        {
            if (PopupDisplayer.Confirm(ExitConfirmMessage))
                _forceShutdown();
        }

        /// <summary><c>internal</c> kvůli testům - <see cref="AsyncRelayCommand"/> nemá awaitovatelný Execute.</summary>
        internal async Task LoginAsAnotherUserAsync()
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
                    _forceShutdown();
                    return;
                case SwitchUserResult.LicenceCancelled:
                    _forceShutdown();
                    return;
                case SwitchUserResult.Success:
                    // Selhání loadu po výměně uživatele: viz ClearSessionState - preference jsou
                    // per-user, takže stará data by se zapisovala pod nového uživatele.
                    try { await RebuildSessionStateAsync(outcome.Licence); }
                    catch { ClearSessionState(); throw; }
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
            // Reset persistovaných pozic/velikostí oken (per MLP × IDFa) - legacy endpoint.
            Rest.QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", new CVar());
            PopupDisplayer.Info("Nastavení oken bylo obnoveno. Změna se projeví po jejich příštím otevření.");
        }

        private static void RunEdisonCopy()
        {
            var exe = CurrentExecutable();
            if (exe.Length == 0) return;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        // Parita s legacy frmConnection: výchozí připojení řídí centrálně EDISONini.xml (jen pro čtení),
        // další si uživatel ukládá lokálně. Jiný server = nová instance shellu - session v procesu
        // je na jeden server (IMLRest.Server/Port jsou konstanty).
        private void ConnectToDatabase()
        {
            var window = new ConnectionWnd(_prefs, DefaultConnection());
            if (WindowNavigator.ShowDialog(window) != true || window.Picked is not { } picked) return;
            var exe = CurrentExecutable();
            if (exe.Length == 0) return;
            Process.Start(ConnectionLaunchInfo(exe, picked));
        }

        // Bez EDISONini.xml (instance spuštěná s argumenty připojení) je výchozí aktuální session.
        private ConnectionChoice DefaultConnection()
        {
            var ini = SettingsLoader.Load().Server;
            return ini.Host.Length > 0
                ? new ConnectionChoice(ini.Name.Length > 0 ? ini.Name : "EDISONini.xml", ini.Host, ini.Port, IsDefault: true)
                : new ConnectionChoice("Aktuální", Rest.Server, Rest.Port, IsDefault: true);
        }

        /// <summary>Nová instance shellu proti vybranému serveru; argumenty čte <see cref="ConnectionOverride.Parse"/>.</summary>
        internal static ProcessStartInfo ConnectionLaunchInfo(string exe, ConnectionChoice connection)
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = true };
            foreach (var arg in new ConnectionOverride(connection.Server, connection.RestPort).ToArguments())
                info.ArgumentList.Add(arg);
            return info;
        }

        private static string CurrentExecutable() => Process.GetCurrentProcess().MainModule?.FileName ?? "";

        // TD přebírá shell session z launch argumentů (GUID + CacheLocks=1) - jeho requesty tak
        // drží ACTIVITY historii shellu (HD 13132/F17) a umírá se shellem jako agendové moduly.
        private void OpenTemplateDesigner() => WindowNavigator.ShowLegacy("TemplateDesigner.exe", formFqn: "");

        private const string LegacyShellExe = "EDISONMAIN.exe";
        // reason=2 je EdisonLaunchReasonEnum.RunEdison; bez něj frmNoMDI ohlásí "nelze spustit
        // samostatně" a zavře se (kontrola, že shell spustil loader, ne uživatel ručně).
        private const string LegacyShellArgs = "reason=2";

        // Starý shell zůstává dostupný i po přepnutí EDLOADERu na EDISONMAIN2. Záměrně plain
        // Process.Start, ne WindowNavigator: ten child váže na KillOnExit job (starý shell by
        // umřel s tímhle), ukazuje ho ve "Spuštěné moduly" a dedupuje modeless launche.
        private static void OpenLegacyShell() => Process.Start(LegacyShellStartInfo());

        internal static ProcessStartInfo LegacyShellStartInfo() =>
            new(EDControls.EDViewModel.WindowNavigator.ResolveExecutablePath(LegacyShellExe))
            {
                Arguments = LegacyShellArgs,
                UseShellExecute = true,
            };

        private async Task RefreshLicenceAsync()
        {
            await RebuildSessionStateAsync(_services.LicenceLoader?.Invoke());
            PopupDisplayer.Info("Licence byla aktualizována.");
        }

        // Tmavý režim - sdílená preference (%APPDATA%\m-line\common\theme.json) přes ThemeService;
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


        // Nastavení → Nastavení fontu - dialog v EDControls (uložení i aplikaci řeší sám).
        // Storage je shared font.json, takže ostatní m-line tooly (TemplateDesigner) přes
        // FileSystemWatcher okamžitě reagují na změnu.
        private static void ShowFontSettings()
            => FontSettingsWindow.ShowDialogFor(Application.Current?.MainWindow);

        private void OpenWebUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        // Týž text jako tlačítko „Informace o verzi" v panelu Podpora - jeden zdroj pravdy.
        private void ShowAppInfo() =>
            PopupDisplayer.Info(SupportActions.BuildVersionInfo(
                MLRest, _services.AppDisplayName, _services.AppVersion));

        // Stálý text bez timer refreshe - shell se typicky spouští denně, půlnoční rollover neřešíme.
        public string InformationText { get; } = BuildInformationText(DateOnly.FromDateTime(DateTime.Today));

        internal static string BuildInformationText(DateOnly today)
        {
            var text = $"Dnes je {today:dd.MM.yyyy}";
            var nameday = CzechNamedays.GetByDate(today);
            if (!string.IsNullOrEmpty(nameday)) text += $", svátek má {nameday}";
            return text;
        }

        // Initial = placeholder: WPF neotevře prázdný submenu, takže MouseEnter refresh by se nevyvolal.
        private IReadOnlyList<RunningModuleInfo> _runningModules = [EmptyPlaceholder];
        public IReadOnlyList<RunningModuleInfo> RunningModules
        {
            get => _runningModules;
            private set => Set(ref _runningModules, value);
        }

        private static readonly RunningModuleInfo EmptyPlaceholder =
            new("Žádné moduly nejsou spuštěné", () => { }, IsEnabled: false);

        private void RefreshRunningModules()
        {
            var modules = _getActiveModules();
            if (modules.Count == 0) { RunningModules = [EmptyPlaceholder]; return; }

            var labels = BuildModuleLabels(_sourceTrees.Select(t => t.Tree));
            RunningModules = [.. modules.Select(m => m with { DisplayName = ResolveRunningLabel(m, labels) })];
        }

        /// <summary>
        /// Mapa <c>exe|forma|param</c> → popisky modulu. Klíč je záměrně tentýž, kterým
        /// <c>WindowNavigator</c> deduplikuje běžící procesy.
        /// </summary>
        internal static Dictionary<string, ModuleLabel> BuildModuleLabels(IEnumerable<IEnumerable<IModule>> trees)
        {
            var labels = new Dictionary<string, ModuleLabel>();
            foreach (var tree in trees)
                foreach (var root in tree)
                    Collect(root, "");
            return labels;

            void Collect(IModule module, string prefix)
            {
                switch (module)
                {
                    case ModuleFamily family:
                        var branch = prefix.Length == 0 ? family.DisplayedName : prefix + PathSeparator + family.DisplayedName;
                        foreach (var child in family.NestedModules) Collect(child, branch);
                        break;
                    case ExecutableModule exe:
                        // První nález vyhrává - týž modul může být ve stromě víckrát (např. i v Oblíbených).
                        labels.TryAdd(ModuleKey(exe.ExecutablePath, exe.FormName, exe.InputParam),
                            new ModuleLabel(
                                prefix.Length == 0 ? exe.DisplayedName : prefix + PathSeparator + exe.DisplayedName,
                                exe.LongName));
                        break;
                }
            }
        }

        private const string PathSeparator = " › ";

        private static string ModuleKey(string exe, string form, string inputParam) => $"{exe}|{form}|{inputParam}";

        /// <summary>
        /// Popisky modulu ve stromu: <c>Path</c> = cesta agendou včetně listu
        /// („Mzdy a personalistika › Zaměstnanci"), <c>LongName</c> = legacy <c>nameEx</c>
        /// (může být prázdný - má ho jen pětina modulů).
        /// </summary>
        internal record ModuleLabel(string Path, string LongName);

        /// <summary>
        /// Label do menu „Spuštěné moduly": dlouhý název modulu, jinak cesta agendou, jinak
        /// (modul mimo strom) vlastní titulek okna bez koncového <c>[IDFa]</c>. Krátký název sám
        /// nestačí - dvě agendy ho mají běžně shodný (Zaměstnanci z MZDY2 vs. z Požadavků na
        /// volno). Firma je v položce zbytečná - při přepnutí firmy se otevřená okna zavírají.
        /// </summary>
        internal static string ResolveRunningLabel(RunningModuleInfo info, IReadOnlyDictionary<string, ModuleLabel> labels) =>
            labels.TryGetValue(ModuleKey(info.ExeName, info.FormFqn, info.InputParam), out var label)
                ? (label.LongName.Length > 0 ? label.LongName : label.Path)
                : StripIdfaSuffix(info.DisplayName);

        /// <summary>Odřízne jeden koncový <c>[...]</c> (suffix s firmou). Hranatá závorka uvnitř názvu zůstane.
        /// Suffix přidávají legacy formy samy (<c>clsForm.ShowIDFA</c>), okna EDISON2 ho nemají.</summary>
        internal static string StripIdfaSuffix(string title)
        {
            var trimmed = title.TrimEnd();
            if (!trimmed.EndsWith(']')) return title;
            var open = trimmed.LastIndexOf('[');
            return open <= 0 ? title : trimmed[..open].TrimEnd();
        }

        // Ctrl+W+W (dvojhmat) - skrytá zkratka pro M-line utility.
        // Stav perzistuje v UI threadu, není potřeba timeout - jakákoliv jiná klávesa čekání zruší.
        private bool _isWaitingForSecondKey;
        public bool IsWaitingForSecondKey
        {
            get => _isWaitingForSecondKey;
            private set
            {
                if (Set(ref _isWaitingForSecondKey, value))
                    OnPropertyChanged(nameof(SecondKeyHintText));
            }
        }
        public string SecondKeyHintText => IsWaitingForSecondKey ? "Stisknuto Ctrl+W - čekám na druhý stisk…" : "";

        public void HandleCtrlW()
        {
            if (IsWaitingForSecondKey)
            {
                IsWaitingForSecondKey = false;
                // UTILITIES.exe ukáže vlastní FrmPassword (legacy date-dependent vzorec) -
                // shell jen detekuje dvojhmat a spustí. Single source of truth pro formuli.
                WindowNavigator.ShowLegacy("UTILITIES.exe", "ALVA.UTILITIES.formUTILITIESMain");
            }
            else
            {
                IsWaitingForSecondKey = true;
            }
        }

        public void CancelSecondKeyWait()
        {
            if (IsWaitingForSecondKey) IsWaitingForSecondKey = false;
        }
    }
}
