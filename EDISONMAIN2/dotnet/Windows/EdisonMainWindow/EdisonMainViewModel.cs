using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDControls.EDViewModel;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using System.Windows;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Database);

    class EdisonMainViewModel : EDViewModel
    {
        private readonly IEdisonMainDataService _dataService;
        private readonly IProcessLaunchService _processService;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand OpenEDComponentAndViewModelTestCommand { get; }
        public ICommand CloseAllCommand { get; }

        public EdisonMainViewModel(IEdisonMainDataService dataService, IProcessLaunchService processService, ConnectionParams connParams)
        {
            ConnParams = connParams;
            _dataService = dataService;
            _processService = processService;

            OpenModuleCommand        = new RelayCommand<IModule>(OpenModule);
            OpenInputFormTestCommand = new RelayCommand(() => Navigator.Show("EDInputFormTestWindow", MLRest!));
            OpenEDListTestCommand    = new RelayCommand(() => Navigator.Show("EDListTestWindow",        MLRest!));
            OpenEDComponentAndViewModelTestCommand = new RelayCommand(() => Navigator.ShowDialog("EDComponentAndViewModelTestWindow", MLRest!));
            CloseAllCommand          = new RelayCommand(_processService.CloseAll);

            TopLevelModules = [];

            var companies = _dataService.GetCompanies();
            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Database))];
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i;

            MLRest = new MLRest(ConnParams.Server, ConnParams.RestPort, Namespace, Idfa)
            {
                LoginName = ConnParams.UserName,
                FullName  = ConnParams.FullName,
                Mlp       = ConnParams.Mlp.ToString()
            };

            var tree = _dataService.GetModuleTree(Namespace, Idfa);
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

        public ConnectionParams ConnParams { get; init; }

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        private void OpenModule(IModule module)
        {
            if (module is ExecutableModule executableModule)
                _processService.Launch(executableModule, Namespace, Idfa);
        }
    }
}
