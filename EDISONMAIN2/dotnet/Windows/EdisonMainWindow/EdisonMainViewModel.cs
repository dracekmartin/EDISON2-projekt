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
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.Desktop;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Modules;
using EDISONMAIN2.SupportStrip;
using EDISONMAIN2.Windows.ChangelogWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias - class i namespace se jmenují stejně, plné qualifikování by bylo ukecané.
using EDCompTestWnd = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
using LogoutCountdownWnd = EDISONMAIN2.Windows.LogoutCountdownWindow.LogoutCountdownWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Name, string Database)
    {
        // Stejný formát jako display přepínače firem (EDFilterComboBox „Key - Label"),
        // aby confirm dialog a ToolTip mluvily o téže položce stejně.
        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : $"{Id} - {Name}";
    }

    partial class EdisonMainViewModel : EDViewModel
    {
        private readonly ISessionDataLoader _sessionLoader;
        private readonly IRestCallRunner _restRunner;
        private readonly SessionEpoch _session;
        private readonly SessionLicenceCoordinator _licence;
        private readonly Func<IMLRest, IReadOnlyList<CompanyStruct>> _loadAllowedCompanies;
        private readonly CompanySwitcher _switcher;
        private readonly Func<SwitchUserOutcome> _switchUser;
        private readonly ILocalPrefs _prefs;
        private readonly Func<IReadOnlyList<RunningModuleInfo>> _getActiveModules;
        private readonly Action _closeAllWindows;
        // Vynucené ukončení aplikace (ShellShutdown.Force) - obchází potvrzovací dialog shellu.
        private readonly Action _forceShutdown;
        private readonly MiniAppCatalog _miniAppCatalog;
        private readonly MiniAppSessionController _miniApps;
        private readonly IShellDialogs _dialogs;
        // Start další instance shellu. Testy ho podstrčí, aby nespouštěly skutečný proces.
        private readonly Action<ProcessStartInfo> _startProcess;
        private readonly bool _internalDiagnostics;
        private readonly bool _isMlineStation;
        // Okno Správa systému > EDISON > EDISON je v odfiltrovaném stromu (licence, AccessTable, brány složek).
        private bool _settingsWindowInTree;

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
        public bool IsTestMenuVisible => _internalDiagnostics;
        /// <summary>Starý shell jen pro admina a stanice M-line - běžný uživatel má zůstat v novém shellu.</summary>
        public bool IsLegacyShellVisible => _services.IsAdmin || _internalDiagnostics;
        public ICommand OpenLegacyShellCommand { get; }
        /// <summary>Kontrola aktualizace jako legacy tlačítko v Správa systému > EDISON > EDISON: kdo okno vidí
        /// a je správce nebo má právo „Aktualizovat EDISON" (formEDISONMAINSettingsShow.vb:1488).</summary>
        public bool IsServerUpdateVisible =>
            ManualUpdate is not null && _settingsWindowInTree && (_services.IsAdmin || _services.MayUpdate);
        /// <summary>TEST verze jen pro stanice M-line jako legacy tsbCheckTESTVersion (IsDomainMline, ne DEBUG build).</summary>
        public bool IsTestUpdateVisible => IsServerUpdateVisible && _isMlineStation;
        public ICommand CheckUpdateCommand { get; }
        public ICommand CheckTestUpdateCommand { get; }
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
        /// Napojí view: docking host miniaplikací, šířku stromu a výšku pruhu podpory (persistují se s layoutem
        /// per session: server, firma, uživatel). Pak obnoví uložený layout. Volá code-behind po načtení okna.
        /// </summary>
        public void AttachShell(IMiniAppHost host, PanelSize? treeWidth = null, PanelSize? supportHeight = null) =>
            _miniApps.AttachHost(host, DockScope, treeWidth, supportHeight);

        // Výběr miniaplikace je vlastní okno, ne vnořené submenu - katalog roste a seskupení
        // per provider se v menu neosvědčilo (HD 13132/F2).
        private void PickMiniApp()
        {
            if (_dialogs.PickMiniApp(_miniAppCatalog, _miniApps.Targets) is { } picked)
                _miniApps.Open(picked.Descriptor, picked.Placement);
        }

        public EdisonMainViewModel(
            EDServices services,
            IReadOnlyList<CompanyStruct> companies,
            Func<IMLRest, IReadOnlyList<CompanyStruct>> loadAllowedCompanies,
            IModuleTreeLoader treeLoader,
            CompanySwitcher switcher,
            Func<SwitchUserOutcome> switchUser,
            ILocalPrefs prefs,
            Func<IReadOnlyList<RunningModuleInfo>> getActiveModules,
            Action closeAllWindows,
            Action forceShutdown,
            MiniAppCatalog miniAppCatalog,
            ModuleShortcutsService shortcuts,
            IChangelogSource? changelogSource,
            bool internalDiagnostics,
            Func<IMLRest, LicenceService.MlineSyncResult> licenceSync,
            IRestCallRunner? restRunner = null,
            ModuleTreeStation? station = null,
            IShellDialogs? dialogs = null,
            Action<ProcessStartInfo>? startProcess = null)
            : base(services)
        {
            _internalDiagnostics = internalDiagnostics;
            _isMlineStation = (station ?? ModuleTreeStation.Production).IsMlineDomain;
            _loadAllowedCompanies = loadAllowedCompanies;
            _switcher = switcher;
            _switchUser = switchUser;
            _prefs = prefs;
            _getActiveModules = getActiveModules;
            _closeAllWindows = closeAllWindows;
            _forceShutdown = forceShutdown;
            _miniAppCatalog = miniAppCatalog;
            _miniApps = new MiniAppSessionController(prefs, miniAppCatalog);
            _dialogs = dialogs ?? new ShellDialogs(() => WindowNavigator);
            _startProcess = startProcess ?? (info => Process.Start(info));
            // Null = novinky vypnuté (testy); produkci dává InfChangelogSource AppBootstrapper.
            _changelogSource = changelogSource;
            _shortcuts = shortcuts;
            // Lazy prefs: _modulePrefs vzniká až v session rebuildu, stromy i plocha ho čtou při každém save.
            _trees = new ModuleTreeState(() => _modulePrefs, station);
            _favorites = new FavoritesCoordinator(shortcuts, _trees, () => _modulePrefs, WindowNavigator,
                () => OnPropertyChanged(nameof(HasNoFavorites)));

            OpenModuleCommand        = CommandAsync<IModule>(OpenModuleAsync);
            LaunchShortcutCommand    = CommandAsync<DesktopShortcut>(LaunchShortcutViaHostAsync);
            ToggleFavoriteCommand    = Command<IModule>(m => _favorites.Toggle(m));
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show(new EDInputFormTestWindow()));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show(new EDListTestWindow()));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog(new EDCompTestWnd()));
            // Test trigger pro cooperative logout - countdown se po vypršení sám zavře (onExpired no-op).
            OpenLogoutCountdownTestCommand = Command(() => WindowNavigator.Show(new LogoutCountdownWnd(15, () => { })));
            TestActionOverlayCommand = CommandAsync<OverlayTestVariant>(v => TestActionOverlayAsync(v.SpriteKey));
            // Test trigger pro error popup - výjimka projde RelayCommand handlerem do EDErrorPopup.
            TriggerErrorCommand = Command(() => throw new InvalidOperationException("Testovací výjimka z toolbaru."));
            OpenLegacyShellCommand = Command(OpenLegacyShell, () => IsLegacyShellVisible);
            CheckUpdateCommand     = CommandAsync(() => CheckUpdateAsync(testVersion: false), () => IsServerUpdateVisible);
            CheckTestUpdateCommand = CommandAsync(() => CheckUpdateAsync(testVersion: true), () => IsTestUpdateVisible);
            OpenMiniAppCommand       = Command<MiniAppDescriptor>(d => _miniApps.Open(d));
            PickMiniAppCommand       = Command(PickMiniApp);
            CloseMiniAppsCommand     = Command(() => _miniApps.CloseClosable());
            ResetMiniAppsCommand     = Command(() => _miniApps.Reset());

            // EDISON menu
            ConnectToDatabaseCommand      = Command(ConnectToDatabase);
            RunEdisonCopyCommand          = Command(RunEdisonCopy);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = CommandAsync(LoginAsAnotherUserAsync);
            ShowUserProfileCommand        = Command(() => _dialogs.ShowUserProfile(StartupWindowDisplayText(), ChangePassword));
            ChangePasswordCommand         = Command(() => ChangePassword());
            SetStartupWindowCommand         = Command<IModule>(SetStartupWindow);
            ClearStartupWindowCommand       = Command(ClearStartupWindow);
            ExpandAllCommand                = Command(() => SetActiveTreeExpanded(true), () => ActiveExpandableTree() is not null);
            CollapseAllCommand              = Command(() => SetActiveTreeExpanded(false), () => ActiveExpandableTree() is not null);
            ExpandSubtreeCommand            = Command<IModule>(m => _trees.SetSubtreeExpanded(m, true));
            CollapseSubtreeCommand          = Command<IModule>(m => _trees.SetSubtreeExpanded(m, false));

            SwitchCompanyCommand          = CommandAsync<int>(SwitchCompanyAsync);

            // Nástroje menu - Správa zámků a Aktivita uživatelů jsou v USERS.exe (retrofitted).
            ShowBgTasksCommand            = CommandAsync(() => LaunchDialogAsync("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINTaskList"));
            RefreshLicenceCommand         = CommandAsync(RefreshLicenceAsync);
            // Admin-only - legacy frmNoMDI.vb:1101-1102 gatuje obě menu items přes UserIsAdmin.
            ShowUsersLocksCommand         = CommandAsync(() => LaunchModelessAsync("USERS.exe", "ALVA.USERS.formUSERSLocks"), () => _services.IsAdmin);
            ShowUsersActivityCommand      = CommandAsync(() => LaunchModelessAsync("USERS.exe", "ALVA.USERS.formUSERSActivity"), () => _services.IsAdmin);

            // Nastavení menu
            FontSettingsCommand           = Command(ShowFontSettings);
            ResetFormsCommand            = Command(ResetForms);

            // Nápověda menu
            ShowInfoCommand               = Command(ShowAppInfo);
            ShowShellChangelogCommand     = CommandAsync(() => ShowChangelogForAsync("EDISONMAIN", "EDISON"));
            ShowModuleChangelogCommand    = CommandAsync<IModule>(ShowModuleChangelogAsync);
            OpenHelpdeskWebCommand        = Command(() => OpenWebUrl("http://podpora.m-line.cz"));

            // Spuštěné moduly menu - dynamický seznam, refresh při SubmenuOpened.
            RefreshRunningModulesCommand  = Command(RefreshRunningModules);
            ActivateModuleCommand         = Command<RunningModuleInfo>(m => m.Activate());

            UsedModules = [];
            _selectedTab = TreeTabs[0];
            // Runner drží politiku „klon per paralelní volání"; testy podstrčí sekvenční variantu.
            _restRunner = restRunner ?? new RestCallRunner(() => Rest);
            _sessionLoader = new SessionDataLoader(_restRunner, treeLoader, [.. _trees.SourceTrees.Select(t => t.Name)]);
            _session = new SessionEpoch(() => Rest, () => Idfa);
            _licence = new SessionLicenceCoordinator(_restRunner, licenceSync, _session,
                RebuildSessionStateAsync, () => LicenceVerifier.ReloadActiveLicence(_services),
                () => _services.IsAdmin || _services.MayUpdate,
                () => { OnPropertyChanged(nameof(LicenceNoticeText)); OnPropertyChanged(nameof(LicenceNoticeTooltip)); });

            Companies = ToCompanyItems(companies);
            // Index musí odpovídat MLRest.Idfa - tam má AppBootstrapper vytvořený ACTIVITY zámek.
            _selectedCompanyIndex = Companies.Select((c, i) => (c, i)).First(x => x.c.Id == Rest.Idfa).i;

            // Spuštění z dlaždice jde přes OpenModuleAsync (usage + overlay), ne duplikovaně v miniaplikaci.
            _shortcuts.LaunchRequested += LaunchShortcut;
            _shortcuts.ChangelogRequested += ShowShortcutChangelog;
        }

        /// <summary>
        /// Geometrii shellu drží local.json per server, ne Caché: týž záznam čte i přihlašovací okno,
        /// které před loginem na serverové úložiště (per uživatel × IDFa) nedosáhne. Obě okna
        /// tak naskočí na stejném monitoru.
        /// </summary>
        protected override IWindowStateStore? WindowStateStore => ShellWindowState;

        internal ShellWindowStateStore ShellWindowState => new(_prefs, Rest.Server, Rest.Port);

        /// <summary>
        /// Start po prvním vykreslení shellu (WPF analog legacy <c>frmMain_Shown</c>), volá ho code-behind
        /// z <c>ContentRendered</c>: načtení session → novinky shellu → výchozí okno. Výchozí okno se hledá
        /// ve stromu a modální novinky blokují otvírání, proto přesně v tomhle pořadí. Chybu loadu ukáže
        /// popupem a dál nepokračuje: volající je <c>async void</c> handler.
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                PopupDisplayer.Error(ex);
                return;
            }
            await ShowShellChangelogIfAnyAsync();
            OpenStartupWindowIfAny(skipHostedModule: LegacyHostMissing());
        }

        /// <summary>Kontrola hosta legacy modulů po startu, null = vypnuto (E2E běh, testy).</summary>
        internal LegacyHostCheck? LegacyHostCheck { get; set; }

        /// <summary>Kontrola aktualizace z Nástrojů, null = položky skryté (E2E běh, testy).</summary>
        internal ManualUpdate? ManualUpdate { get; set; }

        private bool LegacyHostMissing()
        {
            if (LegacyHostCheck?.MissingMessage() is not { } message) return false;
            LegacyHostCheck.Report(message);
            PopupDisplayer.Info(message);
            return true;
        }

        private Task? _initialization;

        /// <summary>
        /// První naplnění session-scoped stavu. Ctor ho záměrně nedělá - shell se ukáže hned
        /// a data dorazí pod overlayem. Jede jako první krok <see cref="StartAsync"/>. Idempotentní.
        /// </summary>
        public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

        private async Task InitializeCoreAsync()
        {
            await RebuildSessionStateAsync();
            _ = SyncMlineLicenceAfterLoginAsync();
        }

        /// <summary>
        /// Po změně session state (initial load, switch firmy, switch uživatele, manual refresh
        /// licence) přebuduje VEŠKERÝ session-scoped derived state. Jediný bod rozšíření pro budoucí
        /// session-scoped data (ECL, user settings, font scheme per firma, …) - přidá se sem,
        /// callsity nepotřebují vědět. Licenci session už zapsal <see cref="LicenceVerifier"/>.
        /// </summary>
        private async Task RebuildSessionStateAsync()
        {
            _session.Invalidate();
            var licence = _services.ActiveLicence;
            // Před stromy: upozornění patří licenci session, i když load stromů selže.
            _ = _licence.RefreshNoticeAsync(licence);
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

            _trees.ReapplySearch();
        }

        /// <summary>Stažení licence z M-line po přihlášení; <c>internal</c> kvůli testům souběhu se změnou session.</summary>
        internal Task SyncMlineLicenceAfterLoginAsync() => _licence.SyncAfterLoginAsync();

        /// <summary>
        /// Promítne načtená data do VM. Musí zůstat SYNCHRONNÍ: guardy <c>_suppressXxx</c> níž
        /// spoléhají na nepřerušený běh, přes <c>await</c> uvnitř by přestaly platit. Zároveň
        /// tím platí, že selhání loadu nechá předchozí stav (stromy, oblíbené) nedotčený.
        /// </summary>
        private void ApplySessionData(SessionData data)
        {
            // Jediný zapisovatel AccessTable: načítá se až s daty session, takže mezi Accepted a loadem
            // platí tabulka předchozí firmy - okna shellu jsou v tu chvíli zavřená.
            _services.AccessTable = data.AccessTable;
            // Uzly se nahradí novými instancemi, takže zapamatovaný výběr míří do prázdna.
            ForgetSelection();
            _trees.Load(data.Trees, _services.ActiveLicence, data.AccessTable, data.Expansion);
            _settingsWindowInTree = _trees.AllExecutables().Any(e => e.ElementId == AccessLoader.FormEdisonmainSettingsShow);
            OnPropertyChanged(nameof(IsServerUpdateVisible));
            OnPropertyChanged(nameof(IsTestUpdateVisible));
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
            _trees.SourceTrees.ToDictionary(t => t.Name, _ => (IReadOnlyList<IModule>)[]),
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
            foreach (var exe in _trees.AllExecutables())
                exe.IsStartupWindow = DefaultWindowId != 0 && exe.ElementId == id;
        }

        private void SetStartupWindow(IModule module)
        {
            if (module is not ExecutableModule { ElementId: { Length: > 0 } elementId }) return;
            if (!int.TryParse(elementId, out var formId) || formId == 0) return;
            DefaultWindowService.Set(Rest, formId);
            DefaultWindowId = formId;
            ApplyStartupWindowMark();
        }

        private void ClearStartupWindow()
        {
            if (DefaultWindowId == 0) return;
            DefaultWindowService.Set(Rest, 0);
            DefaultWindowId = 0;
            ApplyStartupWindowMark();
        }

        private bool _startupWindowOpened;

        /// <summary>False = E2E běh: výchozí okno spouští legacy proces, na který test nečeká.</summary>
        public bool OpensStartupWindow { get; set; } = true;

        /// <summary>
        /// Po prvním vykreslení shellu otevře uživatelův výchozí okno - jednou za běh (ne po
        /// přepnutí firmy). Nepovolený/chybějící modul (není ve stromu) se tiše přeskočí.
        /// Poslední krok <see cref="StartAsync"/>.
        /// </summary>
        internal void OpenStartupWindowIfAny(bool skipHostedModule = false)
        {
            if (_startupWindowOpened) return;
            _startupWindowOpened = true;
            if (!OpensStartupWindow || DefaultWindowId == 0) return;
            var module = _trees.AllExecutables().FirstOrDefault(e => e.ElementId == DefaultWindowId.ToString());
            // Chybějící host právě ohlásila hláška, výchozí modul přes něj by ji jen zopakoval.
            if (module != null && skipHostedModule
                && EDControls.EDViewModel.WindowNavigator.LaunchesThroughLegacyHost(module.ExecutablePath)) return;
            if (module != null && OpenModuleCommand.CanExecute(module))
                OpenModuleCommand.Execute(module);
        }

        // Novinky z dlaždice: klíč nese sama položka (i u zástupců mimo strom), modul se nedohledává.
        private void ShowShortcutChangelog(DesktopShortcut shortcut) =>
            _ = ShowChangelogForAsync(shortcut.ExecutablePath, shortcut.Name);

        // Při zavření shellu se zabíjejí agendy - bez odhlášení by každá spustila reload plochy.
        private void DetachShortcutHandlers()
        {
            _favorites.Detach();
            _shortcuts.LaunchRequested -= LaunchShortcut;
            _shortcuts.ChangelogRequested -= ShowShortcutChangelog;
        }

        // Oblíbené + používanost jsou per-user × IDFa → aplikují se po (pře)stavbě stromů.
        private void ApplyPersonalization(SessionData data)
        {
            _usage = data.Usage;
            _favorites.Apply(data.Shortcuts);
            RebuildUsedList();

            _trees.ApplySavedExpansion();

            // Poslední otevřená záložka (per-user × IDFa); bez uložené volby výchozí dle oblíbených.
            _suppressTabSave = true;
            SelectedTab = data.SelectedTab is { } key ? _trees.TabByKey(key) : DefaultTreeTab();
            _suppressTabSave = false;
        }

        private void RebuildUsedList()
        {
            UsedModules.Clear();
            foreach (var module in BuildUsedList(_trees.SourceTrees.Select(t => t.Tree), _usage, UsedModulesCap))
                UsedModules.Add(module);
        }

        // Strom aktivní záložky; Používané (ListBox) strom nemá → null.
        private ObservableCollection<IModule>? ActiveExpandableTree() =>
            SelectedTab.Expandable ? SelectedTab.Tree : null;

        private void SetActiveTreeExpanded(bool expanded)
        {
            if (ActiveExpandableTree() is { } tree) _trees.SetTreeExpanded(tree, expanded);
        }

        /// <summary>Aplikuje čekající hledání hned (debounce v testech netiká).</summary>
        internal void FlushPendingSearch() => _trees.FlushPendingSearch();

        /// <summary>
        /// První modul odpovídající hledání v aktivní záložce, nebo <c>null</c>. Okno na něj po Enteru
        /// v poli hledání přenese fokus.
        /// </summary>
        internal ExecutableModule? FirstSearchResult() => _trees.FirstSearchResult(SelectedTab);

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

        /// <summary>
        /// Widget pruhu podpory - podpora podle firmy, uživatele a e-mailu cílí sdělení, proto se
        /// notifikuje po přepnutí firmy i po přihlášení jiného uživatele.
        /// </summary>
        public string SupportWidgetUrl => WidgetUrls.Edison2(Rest.Idfa, Rest.LoginName, _services.UserEmail);

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

        /// <summary>Chip upozornění licence a verze (nástěnka starého shellu); prázdný = skrytý.</summary>
        public string LicenceNoticeText => _licence.Notice?.Short ?? "";
        public string LicenceNoticeTooltip => _licence.Notice?.Full ?? "";

        // Stromy, hledání a rozbalení launcher panelu; VM je vystavuje bindingům a orchestruje session.
        private readonly ModuleTreeState _trees;

        public ObservableCollection<IModule> MainMenuTree => _trees.MainMenuTree;
        public ObservableCollection<IModule> SystemAdminTree => _trees.SystemAdminTree;
        public ObservableCollection<IModule> PartnerSolutionsTree => _trees.PartnerSolutionsTree;

        /// <summary>Ořezaný strom jen s oblíbenými (ruční ★) - zachovává hierarchii hlavních stromů.</summary>
        public ObservableCollection<IModule> FavoritesTree => _trees.FavoritesTree;

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

        /// <summary>Hledaný text pro filtrování stromů (napříč všemi záložkami). Prázdný = bez filtru.</summary>
        public string TreeSearchText
        {
            get => _trees.SearchText;
            set
            {
                if (_trees.SearchText == value) return;
                _trees.SearchText = value;
                OnPropertyChanged();
            }
        }

        private TreeTab _selectedTab = null!;   // plní ctor po vytvoření TreeTabs
        private bool _suppressTabSave;

        /// <inheritdoc cref="ModuleTreeState.TreeTabs"/>
        public IReadOnlyList<TreeTab> TreeTabs => _trees.TreeTabs;

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
        private TreeTab DefaultTreeTab() => _trees.TabByKey("main");

        private readonly IChangelogSource? _changelogSource;
        // Auto-popup novinek (infotexty). Žije se session jako _modulePrefs; null = vypnuto (testy).
        private ChangelogPromptService? _changelogPrompt;

        // Per-user × IDFa stav (plocha + používanost). _modulePrefs se re-vytváří v každém rebuildu
        // (UserPrefs se může po switch-user vyměnit); null do prvního initu - bindingy okna jedou dřív
        // než on, takže save cesty musí být null-safe (jinak NRE z TwoWay bindingu záložky).
        private ModulePreferencesService? _modulePrefs;
        private readonly ModuleShortcutsService _shortcuts;
        private readonly FavoritesCoordinator _favorites;
        private Dictionary<string, UsageEntry> _usage = [];
        private const int UsedModulesCap = 20;

        /// <summary>Firmy, do kterých smí přihlášený uživatel (a vždy ta aktuální).</summary>
        public IReadOnlyList<CompanyItem> Companies { get; private set; }

        // GetCompanyList už vrací řazené podle Name & Id (SortedDictionary v ALVA.EDISONAPI.Database).
        private static List<CompanyItem> ToCompanyItems(IReadOnlyList<CompanyStruct> companies) =>
            [.. companies.Select(c => new CompanyItem(c.Id, c.Name ?? "", c.Database))];

        /// <summary>
        /// Nový seznam povolených firem - mění se s uživatelem i s firmou (funkční omezení DB je per IDFa).
        /// Aktuální firma v něm zůstane i bez oprávnění, protože index musí odpovídat MLRest.Idfa.
        /// </summary>
        private void ReplaceCompanies(IReadOnlyList<CompanyStruct> companies, string currentIdfa)
        {
            var items = ToCompanyItems(companies);
            if (!items.Any(c => c.Id == currentIdfa))
                items.Add(Companies.FirstOrDefault(c => c.Id == currentIdfa) ?? new CompanyItem(currentIdfa, "", Rest.DataNamespace));
            Companies = items;
            _selectedCompanyIndex = items.FindIndex(c => c.Id == currentIdfa);
            OnPropertyChanged(nameof(Companies));
            OnPropertyChanged(nameof(CompanySuggestions));
            OnPropertyChanged(nameof(HasMultipleCompanies));
            OnPropertyChanged(nameof(SelectedCompanyIndex));
            RaiseSessionPropertiesChanged();
        }

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
            OnPropertyChanged(nameof(SupportWidgetUrl));
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
        /// Přepnutí firmy: potvrzení → zavření miniaplikací → <see cref="CompanySwitcher"/> (zavře okna,
        /// vymění Idfa, licence) → async rebuild session state → layout miniaplikací nové firmy. Index se
        /// nastaví teprve po <c>Accepted</c>, takže odmítnutí ani cancel jen vrátí miniaplikace staré firmy.
        /// <c>internal</c> kvůli testům - <see cref="AsyncRelayCommand"/> nemá awaitovatelný Execute.
        /// </summary>
        internal async Task SwitchCompanyAsync(int index)
        {
            if (index < 0 || index >= Companies.Count || index == _selectedCompanyIndex) return;

            var target = Companies[index];
            // Potvrzení jen když je co ztratit - bez otevřených oken je přepnutí nedestruktivní
            // (layout miniaplikací se persistuje per session a při návratu obnoví).
            if (_getActiveModules().Count > 0
                && !PopupDisplayer.Confirm($"Přepnout na firmu {target.DisplayName}? Zavřou se všechna otevřená okna i miniaplikace."))
                return;

            _session.Invalidate();
            // Dashboardy v Unloaded ukládají layout přes sdílený MLRest. WPF jim Unloaded doručí během
            // yieldu níž, tedy ještě před výměnou Idfa.
            _miniApps.CloseSession();
            LicenceAcquireOutcome result;
            // Synchronní část (zavření oken, VerifyLic, logout staré firmy) blokuje UI thread -
            // overlay „Akce probíhá" se musí stihnout vykreslit před ní (yield). Scope nesmí
            // přesáhnout do RebuildSessionStateAsync (má vlastní; IsActionInProgress není
            // refcounted) ani pod popupy níž.
            using (ActionInProgress())
            {
                await YieldToRenderAsync();
                try
                {
                    result = _switcher.Switch(target.Id, target.Database, _services.ActiveLicence);
                }
                catch
                {
                    // Po výměně Idfa ji CompanySwitcher při výjimce nevrací, dock staré firmy by mluvil s cizí.
                    if (Rest.Idfa == Idfa) _miniApps.Restore(DockScope);
                    throw;
                }
            }

            if (result != LicenceAcquireOutcome.Accepted)
            {
                // CompanySwitcher vrátil MLRest na starou firmu, dock patří zase jí.
                _miniApps.Restore(DockScope);
                // Combobox si drží cílovou firmu z vlastního editu → notifikací ho vrať na platnou.
                RaiseSessionPropertiesChanged();
                // Cancelled bez hlášky záměrně: admin odmítl licenční retry dialog, nebo okno
                // s neuloženými daty vetovalo zavření - obojí aktivní volba uživatele,
                // další popup by byl jen friction.
                if (result == LicenceAcquireOutcome.Refused)
                    PopupDisplayer.Info("Pro tuto firmu byl překročen počet licencí. Firma nebyla přepnuta.");
                return;
            }

            try
            {
                SelectedCompanyIndex = IndexOfOrAppend(target);
                await RebuildSessionStateAsync();
            }
            catch
            {
                // Session už patří nové firmě, takže stará data jsou cizí: šla by spustit mimo její
                // licenci a fire-and-forget save by je zapsal do jejích blobů. Prázdný shell + chyba
                // je jediný konzistentní stav (recovery: Aktualizovat licenci nebo restart).
                ClearSessionState();
                throw;
            }
            _miniApps.Restore(DockScope);
            _ = ReloadAllowedCompaniesAsync();
        }

        // Seznam se mohl během dialogů switche vyměnit (doběhl seznam povolených firem předchozího
        // přepnutí), takže index z doby před switchem by mířil jinam než MLRest.Idfa.
        private int IndexOfOrAppend(CompanyItem company)
        {
            for (int i = 0; i < Companies.Count; i++)
                if (Companies[i].Id == company.Id) return i;
            Companies = [.. Companies, company];
            OnPropertyChanged(nameof(Companies));
            OnPropertyChanged(nameof(CompanySuggestions));
            OnPropertyChanged(nameof(HasMultipleCompanies));
            return Companies.Count - 1;
        }

        /// <summary>
        /// Funkční omezení DB je per IDFa, nová firma může povolovat jiné (legacy CreateCompanyMenu po přepnutí).
        /// Na pozadí po přestavbě: přepínač na pomocný dotaz nečeká a selhání nechá funkční session se seznamem
        /// z předchozí firmy. Výsledek i chyba platí jen pro session, ve které dotaz začal.
        /// </summary>
        private async Task ReloadAllowedCompaniesAsync()
        {
            var started = _session.CaptureSession();
            try
            {
                var allowed = await _restRunner.RunAsync(_loadAllowedCompanies);
                if (_session.IsSameSession(started)) ReplaceCompanies(allowed, started.Idfa);
            }
            catch (Exception ex)
            {
                if (_session.IsSameSession(started)) PopupDisplayer.Error(ex);
            }
        }

        public CompanyItem SelectedCompany => Companies[_selectedCompanyIndex];

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        private DockScope DockScope => new(Rest.Server, Rest.Port, Idfa, Rest.LoginName);

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
                await prompt.ShowIfNewAsync(path, em.Name);

            // Overlay „akce probíhá" po dobu náběhu modulu (samostatný proces - čekáme na jeho okno).
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyAsync(path, em.FormName, inputParam: em.InputParam);
        }

        /// <summary>Novinky shellu po loginu (EDISONMAIN.INF - obsah sdílený s legacy shellem), krok <see cref="StartAsync"/>.</summary>
        internal Task ShowShellChangelogIfAnyAsync() =>
            _changelogPrompt?.ShowIfNewAsync("EDISONMAIN", "EDISON") ?? Task.CompletedTask;

        // Novinky agendy na vyžádání (kontextové menu stromu). Rodina modulů nemá exe - žádný INF.
        private Task ShowModuleChangelogAsync(IModule module) =>
            module is ExecutableModule { ExecutablePath: { Length: > 0 } path } em
                ? ShowChangelogForAsync(path, em.Name)
                : Task.CompletedTask;

        // Vynucené novinky: obcházejí snooze i session dedup, protože si o ně uživatel řekl sám.
        // Chybějící INF nesmí skončit „klik nic neudělal".
        private async Task ShowChangelogForAsync(string moduleKey, string displayName)
        {
            if (_changelogPrompt is { } prompt && await prompt.ShowAlwaysAsync(moduleKey, displayName)) return;
            PopupDisplayer.Info($"Pro „{displayName}\" nejsou k dispozici informace o změnách.");
        }

        // Modální: ShowIfNewAsync se vrátí až po zavření, volající tak čeká s dalším otvíráním.
        private void ShowChangelogWindow(string title, ChangelogContent content, bool snoozed, Action<bool> onClosed) =>
            onClosed(_dialogs.ShowChangelog(title, content, snoozed));

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
                PopupDisplayer.Info($"Zástupce „{shortcut.Name}\" nemá uloženou agendu ani formulář, "
                                    + "nelze ho spustit. Vytvořte ho znovu z modulu.");
                return;
            }
            using (ActionInProgress())
                await WindowNavigator.ShowLegacyShortcutAsync(shortcut.ExecutablePath, shortcut.FormName,
                    ShortcutLaunchPayloadCodec.Encode(shortcut), inputParam: shortcut.InputParam);
        }

        private void RecordUsage(ExecutableModule module)
        {
            if (module.ElementId.Length == 0) return;

            var entry = _usage.TryGetValue(module.VariantKey, out var prev) ? prev : new UsageEntry();
            _usage[module.VariantKey] = entry with { Count = entry.Count + 1, LastUsedUtc = DateTime.UtcNow };

            RebuildUsedList();
            _ = _modulePrefs?.SaveUsageAsync(_usage);
        }

        // Startup window id (EDISFormEnum) → název modulu ve stromu. ElementId je totéž numerické
        // id jako uložené výchozí okno; pro značení stromu i dialog „Vlastnosti uživatele".
        private string? ResolveModuleName(int formId) =>
            _trees.AllExecutables().FirstOrDefault(e => e.ElementId == formId.ToString())?.Name;

        // Text výchozího okna pro dialog (server ho nezná - dodáváme z modulového stromu).
        // Prázdné = nenastaveno (pole zůstane prázdné, kontext dá helpText pole).
        private string StartupWindowDisplayText() =>
            DefaultWindowId == 0 ? "" : ResolveModuleName(DefaultWindowId) ?? $"#{DefaultWindowId}";

        /// <summary>
        /// Plochý žebříček nejpoužívanějších modulů: řazení dle počtu spuštění (desc), tie-break dle
        /// posledního použití (desc), cap na <paramref name="cap"/>. Mapuje na EXISTUJÍCÍ leaf instance
        /// z hlavních stromů - tím respektuje aktuální licenci/přístup (nezobrazený modul se přeskočí)
        /// a sdílí IsFavorite. Každá varianta (<see cref="ExecutableModule.VariantKey"/>) právě jednou.
        /// </summary>
        internal static List<ExecutableModule> BuildUsedList(
            IEnumerable<IEnumerable<IModule>> sourceTrees, IReadOnlyDictionary<string, UsageEntry> usage, int cap)
        {
            var byVariant = new Dictionary<string, ExecutableModule>();
            foreach (var tree in sourceTrees)
                foreach (var node in tree)
                    foreach (var exe in ModuleTreeState.Flatten(node))
                        if (exe.ElementId.Length > 0)
                            byVariant.TryAdd(exe.VariantKey, exe);

            return usage
                .Where(kv => byVariant.ContainsKey(kv.Key))
                .OrderByDescending(kv => kv.Value.Count)
                .ThenByDescending(kv => kv.Value.LastUsedUtc)
                .Take(cap)
                .Select(kv => byVariant[kv.Key])
                .ToList();
        }

        // Jeden text pro obě cesty ukončení (menu i zavření okna), aby nedriftovaly.
        private const string ExitConfirmMessage = "Zavřít všechna okna?";

        private bool _closeConfirmSuppressed;

        /// <summary>Programové ukončení (force logout, licence, menu Ukončit) - Closing se už neptá.</summary>
        public void SuppressCloseConfirm() => _closeConfirmSuppressed = true;

        /// <summary>Closing shellu: false = zavření zrušit.</summary>
        public bool ConfirmShellClose() =>
            _closeConfirmSuppressed || PopupDisplayer.ConfirmExit(ExitConfirmMessage);

        /// <summary>
        /// Closed shellu (volá code-behind): jediné místo, které při zavření ukládá layout. Ne v Closing:
        /// <c>Application.Shutdown</c> jeho zamítnutí ignoruje a okno by se zavřelo bez uloženého layoutu.
        /// </summary>
        public void OnShellClosed()
        {
            _miniApps.Persist();
            DetachShortcutHandlers();
            _closeAllWindows();
            _miniApps.CloseAll();
        }

        private void ExitApplication()
        {
            if (PopupDisplayer.ConfirmExit(ExitConfirmMessage))
                _forceShutdown();
        }

        /// <summary><c>internal</c> kvůli testům - <see cref="AsyncRelayCommand"/> nemá awaitovatelný Execute.</summary>
        internal async Task LoginAsAnotherUserAsync()
        {
            if (!PopupDisplayer.Confirm("Přihlásit jiného uživatele zavře všechna otevřená okna. Pokračovat?"))
                return;

            _session.Invalidate();
            // Dock zavřít ještě nad starou session, jako u přepnutí firmy (viz SwitchCompanyAsync). Po selhání
            // licence zůstane zavřený: MLRest už patří novému uživateli a shell končí.
            _miniApps.CloseSession();
            var oldSession = Rest;
            SwitchUserOutcome outcome;
            try
            {
                outcome = _switchUser();
            }
            catch
            {
                if (ReferenceEquals(Rest, oldSession)) _miniApps.Restore(DockScope);
                throw;
            }
            switch (outcome.Outcome)
            {
                case SwitchUserResult.Cancelled:
                    _miniApps.Restore(DockScope);
                    return;
                case SwitchUserResult.LicenceRefused:
                    PopupDisplayer.Info("Pro nového uživatele byl překročen počet licencí. Aplikace bude ukončena.");
                    _forceShutdown();
                    return;
                case SwitchUserResult.LicenceCancelled:
                    _forceShutdown();
                    return;
                case SwitchUserResult.Success:
                    // Identita už patří novému MLRest - notifikovat před rebuildem, ať ji status bar ukáže
                    // i tehdy, když load session selže (pruh podpory notifikuje ReplaceCompanies).
                    OnPropertyChanged(nameof(UserName));
                    OnPropertyChanged(nameof(FullName));
                    OnPropertyChanged(nameof(Mlp));
                    OnPropertyChanged(nameof(UserDisplay));
                    OnPropertyChanged(nameof(IsLegacyShellVisible));
                    OnPropertyChanged(nameof(IsServerUpdateVisible));
                    OnPropertyChanged(nameof(IsTestUpdateVisible));
                    // Admin-gated commandy (ShowUsersLocks/Activity) re-vyhodnotí CanExecute hned,
                    // ne až na první input event po switch.
                    CommandManager.InvalidateRequerySuggested();
                    var oldIdfa = Idfa;
                    try
                    {
                        ReplaceCompanies(outcome.Companies ?? [], Rest.Idfa);
                        if (Idfa != oldIdfa)
                        {
                            _prefs.SetLastIdfa(Rest.Server, Rest.Port, Idfa);
                            _prefs.Save();
                        }
                        await RebuildSessionStateAsync();
                    }
                    catch
                    {
                        // Preference jsou per-user, stará data by se zapisovala pod nového uživatele.
                        ClearSessionState();
                        throw;
                    }
                    _miniApps.Restore(DockScope);
                    _ = SyncMlineLicenceAfterLoginAsync();
                    break;
            }
        }

        private async Task RefreshLicenceAsync()
        {
            var sync = await _licence.RefreshAsync(() => ActionInProgress());
            if (sync.Error.Length > 0)
                throw new InvalidOperationException(sync.Error);
            PopupDisplayer.Info("Licence byla aktualizována.");
        }
    }
}
