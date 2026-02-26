using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ALVA.EDISONAPI;
using ALWA.SYS;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2
{
    class MainViewModel : INotifyPropertyChanged
    {
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
            TopLevelModules = new ObservableCollection<IModule>();

            TopLevelModules.Add(
                new ModuleFamily
                {
                    DisplayedName = "MHD",
                    IsExpanded = true,
                    NestedModules = new ObservableCollection<IModule>
                    {
                        new ExecutableModule
                        {
                            DisplayedName = "Plán dopravy",
                            ExecutablePath = "MHD2PlanDopravy.exe",
                            FormName = "ALVA.MHD2.PlanDopravy.formMHD2PlanDopravyMain"
                        },
                        new ExecutableModule
                        {
                            DisplayedName = "Linky a oběhy",
                            ExecutablePath = "MHD2Linky.exe",
                            FormName = "ALVA.MHD2.Linky.formMHD2LinkyHlavniOkno"
                        },
                    }
                }
            );


            var mlRest = CreateMLRest();

            string errState = "";
            var companyList = ALVA.EDISONAPI.Database.GetCompanyList(ref errState, mlRest, false);

            if (!string.IsNullOrEmpty(errState)) MessageBox.Show(errState);

            VisibleIdfas = [.. companyList];
            SelectedCompany = VisibleIdfas.First(x => x.Id == "CB_DP");
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

        void NotifyPropertyChanged([CallerMemberName] string property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

}
