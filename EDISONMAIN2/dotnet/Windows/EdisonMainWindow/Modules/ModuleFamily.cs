using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    class ModuleFamily : IModule
    {
        public string DisplayedName { get; set; } = "";

        public bool IsExpanded { get; set; } = false;

        public ObservableCollection<IModule> NestedModules { get; set; } = [];
    }
}
