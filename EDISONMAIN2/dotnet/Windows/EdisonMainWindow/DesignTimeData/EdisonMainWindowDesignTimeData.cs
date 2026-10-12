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
                Name = "MHD",
                IsExpanded = true,
                NestedModules =
                [
                    new ExecutableModule { Name = "Plán dopravy" },
                    new ExecutableModule { Name = "Dispečink" },
                    new ModuleFamily
                    {
                        Name = "Číselníky",
                        IsExpanded = true,
                        NestedModules =
                        [
                            new ExecutableModule { Name = "Typy provozů" },
                            new ExecutableModule { Name = "Pracoviště" },
                        ]
                    },
                ]
            },
        ];

        public ObservableCollection<IModule> SystemAdminTree { get; } =
        [
            new ExecutableModule { Name = "EDISON" },
            new ModuleFamily
            {
                Name = "DPmCB",
                IsExpanded = false,
                NestedModules =
                [
                    new ExecutableModule { Name = "Návrhář sestav" },
                    new ExecutableModule { Name = "Správce úloh" },
                ]
            },
            new ExecutableModule { Name = "Systémové nástroje" },
        ];

        public ObservableCollection<IModule> PartnerSolutionsTree { get; } =
        [
            new ModuleFamily
            {
                Name = "AURIS",
                IsExpanded = false,
                NestedModules = [ new ExecutableModule { Name = "AURIS" } ]
            },
            new ModuleFamily
            {
                Name = "BC Dopravní systémy",
                IsExpanded = false,
                NestedModules =
                [
                    new ExecutableModule { Name = "Cena jízdného" },
                    new ExecutableModule { Name = "Clearing" },
                ]
            },
            new ExecutableModule { Name = "Telmax" },
        ];

        public ObservableCollection<IModule> FavoritesTree { get; } = [];

        public ObservableCollection<ExecutableModule> UsedModules { get; } = [];

        // Bez záložek by designer ukázal prázdný pruh.
        public IReadOnlyList<TreeTab> TreeTabs { get; }

        public TreeTab SelectedTab { get; set; }

        public EdisonMainWindowDesignTimeData()
        {
            TreeTabs = TreeTab.Launcher(MainMenuTree, SystemAdminTree, PartnerSolutionsTree, FavoritesTree);
            SelectedTab = TreeTabs[0];
        }

        public ObservableCollection<CompanyItem> Companies { get; } =
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

        public static string Version         => "4.12.3";
        public static string ConnectionText  => "localhost:52773";
        public static string UserDisplay     => "Design User";
        public static string NewVersionText  => "Nová verze 4.13.0";
        public static string NewVersionTooltip => "K dispozici je nová verze 4.13.0 (nainstalovaná 4.12.3).";
        public static string UpToDateText    => "";
        public static string SecondKeyHintText   => "";
    }
}
