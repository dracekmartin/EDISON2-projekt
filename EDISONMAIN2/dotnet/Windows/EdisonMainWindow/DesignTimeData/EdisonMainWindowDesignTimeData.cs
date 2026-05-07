using System.Collections.ObjectModel;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;

namespace EDISONMAIN2.Windows.EdisonMainWindow.DesignTimeData
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
            new CompanyItem("CB_DP", "CB_DB"),
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
