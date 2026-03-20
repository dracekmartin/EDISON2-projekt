using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using System.Windows;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Database);

    class EdisonMainViewModel : EDViewModel
    {
        private readonly IModuleTreeService _treeService;
        private readonly IProcessLaunchService _processService;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand CloseAllCommand { get; }

        public EdisonMainViewModel(
            IReadOnlyList<CompanyStruct> companies,
            IMLRest mlRest,
            IModuleTreeService treeService,
            IProcessLaunchService processService)
        {
            _treeService = treeService;
            _processService = processService;

            OpenModuleCommand        = new RelayCommand<IModule>(OpenModule);
            OpenInputFormTestCommand = new RelayCommand(() => WindowNavigator.Show("EDInputFormTestWindow"));
            OpenEDListTestCommand    = new RelayCommand(() => WindowNavigator.Show("EDListTestWindow"));
            OpenEDComponentAndViewModelTestCommand = new RelayCommand(() => WindowNavigator.ShowDialog("EDComponentAndViewModelTestWindow"));
            CloseAllCommand          = new RelayCommand(_processService.CloseAll);

            TopLevelModules = [];

            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Database))];
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i;

            _services.MLRest = mlRest;

            var tree = _treeService.GetModuleTree(MLRest!);
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
                    var result = MessageBox.Show(
                        "Změna společnosti zavře všechna otevřená okna. Pokračovat?",
                        "Upozornění",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes) return;

                    Set(ref _selectedCompanyIndex, value);
                    OnPropertyChanged(nameof(Namespace));
                    OnPropertyChanged(nameof(Idfa));
                    _processService.CloseAll();
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
                _processService.Launch(executableModule, Namespace, Idfa);
        }
    }
}
