using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ALVA.EDISONAPI;
using ALWA.SYS;
using EDISONMAIN2.Commands;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    class MainViewModel : INotifyPropertyChanged
    {
        private readonly List<Process> _processes = new List<Process>();

        public ICommand OpenModuleCommand { get; }
        public MLRest CreateMLRest()
        {
            return new MLRest(Server, RestPort, Namespace, Idfa)
            {
                LoginName = UserName,
                FullName = FullName,
                Mlp = Mlp.ToString()
            };
        }

        public MainViewModel()
        {
            OpenModuleCommand = new RelayCommand<IModule>(OpenModule);

            TopLevelModules = new ObservableCollection<IModule>();

            var mlRest = CreateMLRest();

            string errState = "";
            var companyList = ALVA.EDISONAPI.Database.GetCompanyList(ref errState, mlRest, false);

            if (!string.IsNullOrEmpty(errState)) MessageBox.Show(errState);

            VisibleIdfas = [.. companyList];
            SelectedCompany = VisibleIdfas.First(x => x.Id == "CB_DP");

            mlRest = CreateMLRest();
            object[] treeData = mlRest.Query("ALVA.EDISONMAIN2.API.Tree", "GetTree", Array.Empty<object>());
            mlRest.RestStatus.ThrowIfError();

            var familyStack = new Stack<ObservableCollection<IModule>>();
            ObservableCollection<IModule> currentList = TopLevelModules;

            for (int i = 1; i < treeData.Length; i++)
            {
                string[] parts = treeData[i]?.ToString()?.Split(mlRest.Delimiter) ?? [];
                switch (parts[0])
                {
                    case "FAMILY":
                        var family = new ModuleFamily { DisplayedName = parts[1], IsExpanded = parts[2] == "1" };
                        currentList.Add(family);
                        familyStack.Push(currentList);
                        currentList = family.NestedModules;
                        break;
                    case "EXE":
                        currentList.Add(new ExecutableModule { DisplayedName = parts[1], ExecutablePath = parts[2], FormName = parts[3] });
                        break;
                    case "FAMILY_END":
                        if (familyStack.Count > 0) currentList = familyStack.Pop();
                        break;
                }
            }
        }

        public ObservableCollection<IModule> TopLevelModules { get; set; }

        public ObservableCollection<CompanyStruct> VisibleIdfas { get; set; }

        string _server = "192.168.16.1";
        public string Server
        {
            get => _server;
            set
            {
                _server = value;
                NotifyPropertyChanged();
            }
        }

        int _restPort = 57770;
        public int RestPort
        {
            get => _restPort;
            set
            {
                _restPort = value;
                NotifyPropertyChanged();
            }
        }

        public CompanyStruct SelectedCompany { get; set; }

        public string Namespace
        {
            get => SelectedCompany.Database;
        }

        public string Idfa
        {
            get => SelectedCompany.Id;
        }

        string _userName = "martin.hubata";
        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                NotifyPropertyChanged();
            }
        }

        string _fullName = "martin.hubata";
        public string FullName
        {
            get => _fullName;
            set
            {
                _fullName = value;
                NotifyPropertyChanged();
            }
        }

        int _mlp = 167;
        public int Mlp
        {
            get => _mlp;
            set
            {
                _mlp = value;
                NotifyPropertyChanged();
            }
        }

        private void OpenModule(IModule module)
        {
            if (module is ExecutableModule executableModule && !string.IsNullOrEmpty(executableModule.ExecutablePath))
            {
                var prc = Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = Path.Combine("C:\\EDISON", executableModule.ExecutablePath),
                    Arguments = $"EDISONMAIN2 \"{Server}\" \"{RestPort}\" \"{Namespace}\" \"{Idfa}\" \"{UserName}\" \"{FullName}\" \"{Mlp}\" \"{executableModule.FormName}\"",
                });
                if (prc != null) _processes.Add(prc);
            }
        }

        public void CloseAllProcesses()
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing process: {ex.Message}");
                }
            }
        }

        void NotifyPropertyChanged([CallerMemberName] string property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

}
