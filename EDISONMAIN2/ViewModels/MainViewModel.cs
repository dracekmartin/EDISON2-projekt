using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ALVA.EDISONAPI;
using EDISONMAIN2.Commands;
using EDISONMAIN2.Services;
using EDISONMAIN2.Services.Data;
using EDISONMAIN2.Services.Processes;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    class MainViewModel : INotifyPropertyChanged
    {
        private readonly IEdisonMainDataService _dataService;
        private readonly IProcessLaunchService _processService;

        public ICommand OpenModuleCommand { get; }

        public MainViewModel(IEdisonMainDataService dataService, IProcessLaunchService processService, ConnectionParams connParams)
        {
            _dataService = dataService;
            _processService = processService;

            OpenModuleCommand = new RelayCommand<IModule>(OpenModule);

            TopLevelModules = new ObservableCollection<IModule>();

            var companies = _dataService.GetCompanies();

            VisibleIdfas = [.. companies];
            _selectedCompanyIndex = VisibleIdfas.Select((c, i) => (c, i)).First(x => x.c.Id == "CB_DP").i;

            var tree = _dataService.GetModuleTree(Namespace, Idfa);
            foreach (var module in tree)
                TopLevelModules.Add(module);

            ConnParams = connParams;
        }

        public ObservableCollection<IModule> TopLevelModules { get; }

        public ObservableCollection<CompanyStruct> VisibleIdfas { get; }

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

        public CompanyStruct SelectedCompany => VisibleIdfas[_selectedCompanyIndex];

        public ConnectionParams ConnParams { get; init; }

        public string Namespace => SelectedCompany.Database;

        public string Idfa => SelectedCompany.Id;

        public void CloseAllProcesses() => _processService.CloseAll();

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
