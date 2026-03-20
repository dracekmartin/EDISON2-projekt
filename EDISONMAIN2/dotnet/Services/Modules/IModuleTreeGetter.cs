using EDControls;
using System.Collections.Generic;

namespace EDISONMAIN2.Services.Modules
{
    public interface IModuleTreeGetter
    {
        /// <summary>Fetches and parses the module tree using the shared IMLRest connection.</summary>
        IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest);
    }
}
