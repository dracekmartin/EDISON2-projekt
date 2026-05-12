using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public interface IModule
    {
        string DisplayedName { get; }
    }
}
