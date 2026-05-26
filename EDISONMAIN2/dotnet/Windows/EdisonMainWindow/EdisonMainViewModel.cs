using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using Application = System.Windows.Application;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserFonts;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias — class i namespace se jmenují stejně, plné qualifikování by bylo ukecané.
using EDCompTestWnd = EDISONMAIN2.Windows.EDComponentAndViewModelTestWindow.EDComponentAndViewModelTestWindow;
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
        private readonly Func<Lic?> _licenceLoader;
        private readonly CompanySwitcher _switcher;
        private readonly Func<SwitchUserOutcome> _switchUser;
        private readonly ILocalPrefs _prefs;
        private readonly Func<IReadOnlyList<RunningModuleInfo>> _getActiveModules;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand OpenTemplateDesignerCommand { get; }
        public ICommand CloseAllCommand { get; }

        // Hlavní menu (replikuje legacy frmNoMDI.mnuMain).
        // EDISON menu
        public ICommand ConnectToDatabaseCommand { get; }
        public ICommand RunEdisonCopyCommand { get; }
        public ICommand ExitCommand { get; }
        // Uživatel menu
        public ICommand LoginAsAnotherUserCommand { get; }
        public ICommand ShowUserEditCommand { get; }
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

        public EdisonMainViewModel(
            IReadOnlyList<CompanyStruct> companies,
            IMLRest mlRest,
            IModuleTreeGetter treeService,
            Func<Lic?> licenceLoader,
            Lic? initialLicence,
            CompanySwitcher switcher,
            Func<SwitchUserOutcome> switchUser,
            ILocalPrefs prefs,
            Func<IReadOnlyList<RunningModuleInfo>> getActiveModules,
            Action closeAllWindows)
        {
            _treeGetter = treeService;
            _licenceLoader = licenceLoader;
            _switcher = switcher;
            _switchUser = switchUser;
            _prefs = prefs;
            _getActiveModules = getActiveModules;

            OpenModuleCommand        = Command<IModule>(OpenModule);
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show(new EDInputFormTestWindow()));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show(new EDListTestWindow()));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog(new EDCompTestWnd()));
            OpenTemplateDesignerCommand = Command(OpenTemplateDesigner);
            CloseAllCommand          = Command(closeAllWindows);

            // EDISON menu
            ConnectToDatabaseCommand      = Command(NotYetImplemented);
            RunEdisonCopyCommand          = Command(RunEdisonCopy);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = Command(LoginAsAnotherUser);
            ShowUserEditCommand           = Command(() => WindowNavigator.ShowLegacyAsDialog("EDISONMAIN.exe", "ALVA.EDISONMAIN.formEDISONMAINUserEdit"));
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
        }

        // Stromy i licenční gating jsou per-IdFa, takže po přepnutí firmy refresh všech tří.
        private void LoadAndFilterTrees()
        {
            LoadAndFilterTree(MainMenuTree, "main");
            LoadAndFilterTree(SystemAdminTree, "system");
            LoadAndFilterTree(PartnerSolutionsTree, "partners");
        }

        private void LoadAndFilterTree(ObservableCollection<IModule> target, string treeName)
        {
            target.Clear();
            var tree = _treeGetter.GetModuleTree(MLRest!, treeName);
            foreach (var module in tree)
            {
                if (TryFilter(module, _services.ActiveLicence, _services.AccessTable) is { } filtered)
                    target.Add(filtered);
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

        public ObservableCollection<IModule> MainMenuTree { get; }
        public ObservableCollection<IModule> SystemAdminTree { get; }
        public ObservableCollection<IModule> PartnerSolutionsTree { get; }

        public ObservableCollection<CompanyItem> VisibleIdfas { get; }

        private int _selectedCompanyIndex;
        public int SelectedCompanyIndex
        {
            get => _selectedCompanyIndex;
            set
            {
                if (_selectedCompanyIndex == value) return;

                if (!PopupDisplayer.Confirm("Změna společnosti zavře všechna otevřená okna. Pokračovat?"))
                    return;

                int originalIndex = _selectedCompanyIndex;

                Set(ref _selectedCompanyIndex, value);
                OnPropertyChanged(nameof(Namespace));
                OnPropertyChanged(nameof(Idfa));
                OnPropertyChanged(nameof(WindowTitle));

                var result = _switcher.Switch(Idfa, Namespace, _services.ActiveLicence);

                if (result.Outcome == LicenceAcquireOutcome.Accepted)
                {
                    RebuildSessionState(result.Licence);
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

        private void OpenModule(IModule module)
        {
            if (module is ExecutableModule { ExecutablePath: { Length: > 0 } path } em)
                WindowNavigator.ShowLegacy(path, em.FormName, inputParam: em.InputParam);
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
                    // StatusBar identity bindings refreshne na novém MLRest.
                    OnPropertyChanged(nameof(UserName));
                    OnPropertyChanged(nameof(FullName));
                    OnPropertyChanged(nameof(Mlp));
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
            RebuildSessionState(_licenceLoader());
            PopupDisplayer.Info("Licence byla aktualizována.");
        }

        // Uživatel → Možnosti — toggles uložené do LocalPrefs. Setter persistuje a notifikuje.
        // ShowWindowsAsDialogs a ShowMiniApps zatím bez behaviorálního efektu (EDISONMAIN2
        // nemá miniaplikace, dialog mode nikdo nečte) — flagy se ukládají pro budoucnost.
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

        public bool ShowMiniApps
        {
            get => _prefs.ShowMiniApps;
            set
            {
                if (_prefs.ShowMiniApps == value) return;
                _prefs.ShowMiniApps = value;
                _prefs.Save();
                OnPropertyChanged();
            }
        }

        public bool LoadBoards
        {
            get => _prefs.LoadBoards;
            set
            {
                if (_prefs.LoadBoards == value) return;
                _prefs.LoadBoards = value;
                _prefs.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoardsVisibility));
            }
        }

        public Visibility BoardsVisibility => LoadBoards ? Visibility.Visible : Visibility.Collapsed;

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
