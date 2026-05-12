using System.Collections.ObjectModel;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    class ModuleFamily : IModule
    {
        public string DisplayedName { get; set; } = "";

        public bool IsExpanded { get; set; } = false;

        public ObservableCollection<IModule> NestedModules { get; set; } = [];
    }
}
