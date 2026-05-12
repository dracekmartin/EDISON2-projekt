using EDControls;
using System.Collections.Generic;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public interface IModuleTreeGetter
    {
        /// <summary>Načte a naparsuje strom modulů přes sdílené IMLRest spojení.</summary>
        IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest);
    }
}
