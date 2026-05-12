using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using EDISONMAIN2.Windows.EdisonMainWindow.Processes;
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
        private readonly IProcessLauncher _processLauncher;
        private readonly Func<Lic?> _licenceLoader;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
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
        public ICommand ToggleDialogModeCommand { get; }
        public ICommand ToggleMiniAppsCommand { get; }
        public ICommand ToggleLoadBoardsCommand { get; }
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
        // Styl menu
        public ICommand ApplyStyleCommand { get; }
        public ICommand SelectFontCommand { get; }

        public EdisonMainViewModel(
            IReadOnlyList<CompanyStruct> companies,
            IMLRest mlRest,
            IModuleTreeGetter treeService,
            IProcessLauncher processService,
            Func<Lic?> licenceLoader)
        {
            _treeGetter = treeService;
            _processLauncher = processService;
            _licenceLoader = licenceLoader;

            OpenModuleCommand        = Command<IModule>(OpenModule);
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show("EDInputFormTestWindow"));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show("EDListTestWindow"));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog("EDComponentAndViewModelTestWindow"));
            CloseAllCommand          = Command(_processLauncher.CloseAll);

            // EDISON menu
            ConnectToDatabaseCommand      = Command(NotYetImplemented);
            RunEdisonCopyCommand          = Command(NotYetImplemented);
            ExitCommand                   = Command(ExitApplication);

            // Uživatel menu
            LoginAsAnotherUserCommand     = Command(NotYetImplemented);
            ShowUserEditCommand           = Command(NotYetImplemented);
            ShowBgTasksCommand            = Command(NotYetImplemented);
            ToggleDialogModeCommand       = Command(NotYetImplemented);
            ToggleMiniAppsCommand         = Command(NotYetImplemented);
            ToggleLoadBoardsCommand       = Command(NotYetImplemented);
            ResetFormsCommand             = Command(ResetForms);

            // Nástroje menu — Správa zámků a Aktivita uživatelů jsou v USERS.exe (retrofitted).
            RefreshLicenseCommand         = Command(NotYetImplemented);
            ShowUsersLocksCommand         = Command(() => LaunchLegacyForm("USERS.exe", "ALVA.USERS.formUSERSLocks", "Správa zámků"));
            ShowUsersActivityCommand      = Command(() => LaunchLegacyForm("USERS.exe", "ALVA.USERS.formUSERSActivity", "Aktivita uživatelů"));
            ShowLanguageSettingsCommand   = Command(NotYetImplemented);
            SendSystemLogCommand          = Command(NotYetImplemented);

            // Nápověda menu
            ShowInfoCommand               = Command(ShowAppInfo);
            OpenHelpdeskWebCommand        = Command(() => OpenWebUrl("http://podpora.m-line.cz"));

            // Styl menu
            ApplyStyleCommand             = Command(NotYetImplemented);
            SelectFontCommand             = Command(NotYetImplemented);

            TopLevelModules = [];

            // GetCompanyList už vrací řazené podle Name & Id (SortedDictionary v ALVA.EDISONAPI.Database).
            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Name ?? "", c.Database))];
            // Index musí odpovídat MLRest.Idfa — tam má App.xaml.cs vytvořený ACTIVITY zámek.
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == mlRest.Idfa).i;

            _services.MLRest = mlRest;

            LoadAndFilterTree();
        }

        // Strom i licenční gating jsou per-IdFa, takže po přepnutí firmy refresh.
        private void LoadAndFilterTree()
        {
            TopLevelModules.Clear();
            var tree = _treeGetter.GetModuleTree(MLRest!);
            foreach (var module in tree)
            {
                if (TryFilter(module, _services.ActiveLicence) is { } filtered)
                    TopLevelModules.Add(filtered);
            }
        }

        /// <summary>
        /// Profiltruje strom podle licence. EXE projde pokud není gateovaný nebo je v
        /// <see cref="Lic.Elements"/>; FAMILY zůstává, pokud má aspoň jedno viditelné dítě.
        /// </summary>
        internal static IModule? TryFilter(IModule module, Lic? licence) => module switch
        {
            ExecutableModule exe => IsAllowed(exe, licence) ? exe : null,
            ModuleFamily family  => FilterFamily(family, licence),
            _                    => module,
        };

        internal static bool IsAllowed(ExecutableModule exe, Lic? licence) =>
            !exe.IsLicensed || (licence?.Elements?.Contains(exe.ElementId) ?? false);

        internal static ModuleFamily? FilterFamily(ModuleFamily family, Lic? licence)
        {
            var visibleChildren = family.NestedModules
                .Select(child => TryFilter(child, licence))
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

        public ObservableCollection<IModule> TopLevelModules { get; }

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
                string originalIdfa = MLRest!.Idfa;
                string originalNamespace = MLRest!.DataNamespace;
                Lic? originalLicence = _services.ActiveLicence;

                Set(ref _selectedCompanyIndex, value);
                OnPropertyChanged(nameof(Namespace));
                OnPropertyChanged(nameof(Idfa));
                OnPropertyChanged(nameof(WindowTitle));
                _processLauncher.CloseAll();
                MLRest!.DataNamespace = Namespace;
                MLRest!.Idfa = Idfa;

                // ACTIVITY zámek + počet licencí jsou per-IdFa — pro VerifyLic potřebujeme
                // licenci cílové firmy, ne původní (jinak bychom kontrolovali špatný limit).
                // Při překročení licencí + admin nabídne LicenceVerifier okno aktivity uživatelů.
                Lic? newLicence = originalLicence;
                var outcome = LicenceAcquireOutcome.Accepted;
                if (originalLicence != null)
                    (outcome, newLicence) = LicenceVerifier.TryAcquireSession(
                        MLRest!, _licenceLoader, _services.IsAdmin, PopupDisplayer);

                if (outcome == LicenceAcquireOutcome.Accepted)
                {
                    _services.ActiveLicence = newLicence;
                    LocalPrefs.Current.LastIdfa = MLRest!.Idfa;
                    LocalPrefs.Current.Save();
                    LoadAndFilterTree();
                }
                else
                {
                    MLRest.Idfa = originalIdfa;
                    MLRest.DataNamespace = originalNamespace;
                    Set(ref _selectedCompanyIndex, originalIndex);
                    OnPropertyChanged(nameof(Namespace));
                    OnPropertyChanged(nameof(Idfa));
                    OnPropertyChanged(nameof(WindowTitle));
                    if (outcome == LicenceAcquireOutcome.Refused)
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
            if (module is ExecutableModule executableModule)
                _processLauncher.Launch(executableModule);
        }

        // Pro legacy moduly, na které neukazuje žádný uzel module-tree (typicky systémové
        // formy v USERS.exe). Stejný kontrakt jako ProcessLauncher.Launch — propaguje
        // shellův GUID, aby modul sdílel ACTIVITY zámek.
        private void LaunchLegacyForm(string exePath, string formFqn, string displayName)
        {
            _processLauncher.Launch(new ExecutableModule
            {
                DisplayedName  = displayName,
                ExecutablePath = exePath,
                FormName       = formFqn,
            });
        }

        private void ExitApplication()
        {
            if (PopupDisplayer.Confirm("Opravdu chcete ukončit aplikaci?"))
                Application.Current.Shutdown();
        }

        private void ResetForms()
        {
            if (!PopupDisplayer.Confirm("Obnovit výchozí pozice a velikosti všech oken?"))
                return;
            // Reset persistovaných pozic/velikostí oken (per MLP × IDFa) — legacy endpoint.
            MLRest!.QueryOrThrow("ALVA.LIBRARY.Form", "ResetSettings", new CVar());
            PopupDisplayer.Info("Nastavení oken bylo obnoveno. Změna se projeví po jejich příštím otevření.");
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

        private void NotYetImplemented()
        {
            PopupDisplayer.Info("Tato funkce zatím není v EDISONu 2 dostupná.");
        }
    }
}
