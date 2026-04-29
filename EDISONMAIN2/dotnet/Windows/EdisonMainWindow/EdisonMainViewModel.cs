using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Database);

    class EdisonMainViewModel : EDViewModel
    {
        private readonly IModuleTreeGetter _treeGetter;
        private readonly IProcessLauncher _processLauncher;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand CloseAllCommand { get; }

        public EdisonMainViewModel(
            IReadOnlyList<CompanyStruct> companies,
            IMLRest mlRest,
            IModuleTreeGetter treeService,
            IProcessLauncher processService,
            ConnectionParams connParams,
            Lic? activeLicence)
        {
            _treeGetter = treeService;
            _processLauncher = processService;
            ConnParams = connParams;

            OpenModuleCommand        = Command<IModule>(OpenModule);
            OpenInputFormTestCommand = Command(() => WindowNavigator.Show("EDInputFormTestWindow"));
            OpenEDListTestCommand    = Command(() => WindowNavigator.Show("EDListTestWindow"));
            OpenEDComponentAndViewModelTestCommand = Command(() => WindowNavigator.ShowDialog("EDComponentAndViewModelTestWindow"));
            CloseAllCommand          = Command(_processLauncher.CloseAll);

            TopLevelModules = [];

            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Database))];
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i;

            _services.MLRest = mlRest;

            var tree = _treeGetter.GetModuleTree(MLRest!);
            foreach (var module in tree)
            {
                if (TryFilter(module, activeLicence) is { } filtered)
                    TopLevelModules.Add(filtered);
            }
        }

        /// <summary>
        /// Rekurzivně profiltruje uzel stromu podle licence.
        /// EXE projde pokud <see cref="ExecutableModule.IsLicensed"/> je <c>false</c>
        /// (vždy povoleno), nebo pokud je <see cref="ExecutableModule.ElementId"/>
        /// v <see cref="Lic.Elements"/> whitelistu.
        /// FAMILY se zachová, pokud po filtraci zbývá alespoň jedno dítě.
        /// Vrací <c>null</c> pro uzly, které nemají projít.
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

        /// <summary>Exposed for StatusBar bindings in EdisonMainWindow.xaml.</summary>
        public ConnectionParams ConnParams { get; }

        public ObservableCollection<IModule> TopLevelModules { get; }

        public ObservableCollection<CompanyItem> VisibleIdfas { get; }

        private int _selectedCompanyIndex;
        public int SelectedCompanyIndex
        {
            get => _selectedCompanyIndex;
            set
            {
                if (_selectedCompanyIndex != value)
                {
                    if (!PopupDisplayer.Confirm("Změna společnosti zavře všechna otevřená okna. Pokračovat?"))
                        return;

                    Set(ref _selectedCompanyIndex, value);
                    OnPropertyChanged(nameof(Namespace));
                    OnPropertyChanged(nameof(Idfa));
                    OnPropertyChanged(nameof(WindowTitle));
                    _processLauncher.CloseAll();
                    MLRest!.DataNamespace = Namespace;
                    MLRest!.Idfa = Idfa;
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
                _processLauncher.Launch(executableModule, Namespace, Idfa);
        }
    }
}
