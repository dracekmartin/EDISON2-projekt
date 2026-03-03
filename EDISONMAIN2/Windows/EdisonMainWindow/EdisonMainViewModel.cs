using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ALVA.EDISONAPI;
using EDISONMAIN2.Commands;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Modules;
using EDISONMAIN2.Services.Processes;
using EDISONMAIN2.Services.Windows;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    record CompanyItem(string Id, string Database);

    class EdisonMainViewModel : INotifyPropertyChanged
    {
        private readonly IEdisonMainDataService _dataService;
        private readonly IProcessLaunchService _processService;
        private readonly IWindowService _windowService;

        public ICommand OpenModuleCommand { get; }
        public ICommand OpenInputFormTestCommand { get; }
        public ICommand OpenEDListTestCommand { get; }
        public ICommand CloseAllCommand { get; }

        public EdisonMainViewModel(IEdisonMainDataService dataService, IProcessLaunchService processService, IWindowService windowService, ConnectionParams connParams)
        {
            _dataService = dataService;
            _processService = processService;
            _windowService = windowService;

            OpenModuleCommand = new RelayCommand<IModule>(OpenModule);
            OpenInputFormTestCommand = new RelayCommand(() => _windowService.ShowInputFormTest(ConnParams, Namespace, Idfa));
            OpenEDListTestCommand    = new RelayCommand(() => _windowService.ShowEDListTest(ConnParams, Namespace, Idfa));
            CloseAllCommand = new RelayCommand(_processService.CloseAll);

            TopLevelModules = [];

            var companies = _dataService.GetCompanies();

            VisibleIdfas = [.. companies.Select(c => new CompanyItem(c.Id, c.Database))];
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i;

            var tree = _dataService.GetModuleTree(Namespace, Idfa);
            foreach (var module in tree)
                TopLevelModules.Add(module);

            ConnParams = connParams;
        }

        public ObservableCollection<IModule> TopLevelModules { get; }

        public ObservableCollection<CompanyItem> VisibleIdfas { get; }

        private int _selectedCompanyIndex;
        public int SelectedCompanyIndex
        {
            get => _selectedCompanyIndex;
            set
            {
                _selectedCompanyIndex = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(Namespace));
                NotifyPropertyChanged(nameof(Idfa));
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

        void NotifyPropertyChanged([CallerMemberName] string? property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
