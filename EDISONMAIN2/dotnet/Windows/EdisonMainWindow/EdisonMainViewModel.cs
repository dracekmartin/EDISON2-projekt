using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
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
            IProcessLauncher processService)
        {
            _treeGetter = treeService;
            _processLauncher = processService;

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
                TopLevelModules.Add(module);
        }

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
                    _processLauncher.CloseAll();
                    MLRest!.DataNamespace = Namespace;
                    MLRest!.Idfa = Idfa;
                }
            }
        }

        public CompanyItem SelectedCompany => VisibleIdfas[_selectedCompanyIndex];

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        private void OpenModule(IModule module)
        {
            if (module is ExecutableModule executableModule)
                _processLauncher.Launch(executableModule, Namespace, Idfa);
        }
    }
}
