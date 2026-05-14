using System.Collections.ObjectModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Windows.EdisonMainWindow.DesignTimeData
{
    class EdisonMainWindowDesignTimeData
    {
        public ObservableCollection<IModule> MainMenuTree { get; } =
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

        public ObservableCollection<IModule> SystemAdminTree { get; } =
        [
            new ExecutableModule { DisplayedName = "Společná evidence" },
            new ModuleFamily
            {
                DisplayedName = "Kmenová evidence",
                IsExpanded = false,
                NestedModules =
                [
                    new ExecutableModule { DisplayedName = "Návrhář sestav" },
                    new ExecutableModule { DisplayedName = "Správce úloh" },
                ]
            },
            new ExecutableModule { DisplayedName = "Systémové nástroje" },
        ];

        public ObservableCollection<IModule> PartnerSolutionsTree { get; } =
        [
            new ModuleFamily
            {
                DisplayedName = "AURIS",
                IsExpanded = false,
                NestedModules = [ new ExecutableModule { DisplayedName = "AURIS" } ]
            },
            new ModuleFamily
            {
                DisplayedName = "BC Dopravní systémy",
                IsExpanded = false,
                NestedModules =
                [
                    new ExecutableModule { DisplayedName = "Cena jízdného" },
                    new ExecutableModule { DisplayedName = "Clearing" },
                ]
            },
            new ExecutableModule { DisplayedName = "Telmax" },
        ];

        public ObservableCollection<CompanyItem> VisibleIdfas { get; } =
        [
            new CompanyItem("CB_DP", "Dopravní podnik města České Budějovice", "CB_DB"),
        ];

        public int SelectedCompanyIndex { get; set; } = 0;

        public static string Server    => "localhost";
        public static int    RestPort  => 52773;
        public static string UserName  => "designer";
        public static string FullName  => "Design User";
        public static string Mlp       => "0";

        public static string Namespace => "CBDB";
        public static string Idfa => "CB_DP";
        public static string WindowTitle => "M-line EDISON [CB_DP]";
    }
}
