using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    class ModuleFamily : IModule
    {
        public string DisplayedName { get; set; } = "";

        public bool IsExpanded { get; set; } = false;

        public ObservableCollection<IModule> NestedModules { get; set; } = [];

        /// <summary>
        /// Hex barva (<c>"#RRGGBB"</c>) pro Foreground rodiny ve stromu; "" = výchozí barva tématu.
        /// V autorovaných datech vždy "" — nastavuje ji runtime ModuleLaunchabilityCheck (DEBUG)
        /// agregací stavu dětí (sbalená rodina pak signalizuje stub/broken bez rozbalení).
        /// </summary>
        public string Color { get; set; } = "";
    }
}
