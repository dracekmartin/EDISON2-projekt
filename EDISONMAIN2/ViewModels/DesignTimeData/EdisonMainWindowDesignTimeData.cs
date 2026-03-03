using System.Collections.ObjectModel;
using EDISONMAIN2.Modules;
using EDISONMAIN2.Services;

namespace EDISONMAIN2.ViewModels.DesignTimeData
{
    class EdisonMainWindowDesignTimeData
    {
        public ObservableCollection<IModule> TopLevelModules { get; } =
        [
            new ModuleFamily
            {
                DisplayedName = "MHD",
                IsExpanded = true,
                NestedModules =
                [
                    new ExecutableModule { DisplayedName = "Plán dopravy" },
                    new ExecutableModule { DisplayedName = "Dispečink" },
                    new ModuleFamily
                    {
                        DisplayedName = "Číselníky",
                        IsExpanded = true,
                        NestedModules =
                        [
                            new ExecutableModule { DisplayedName = "Typy provozů" },
                            new ExecutableModule { DisplayedName = "Pracoviště" },
                        ]
                    },
                ]
            },
        ];

        public ObservableCollection<CompanyItem> VisibleIdfas { get; } =
        [
            new CompanyItem("CB_DP", "CBDB"),
        ];

        public int SelectedCompanyIndex { get; set; } = 0;

        public ConnectionParams ConnParams { get; } = new("localhost", 52773, "designer", "Design User", 0);

        public static string Namespace => "CBDB";
        public static string Idfa => "CB_DP";
    }
}
