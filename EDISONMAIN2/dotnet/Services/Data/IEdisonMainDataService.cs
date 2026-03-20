using EDControls;
using EDISONMAIN2.Services.Modules;
using System.Collections.Generic;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Services.Data
{
    public interface IEdisonMainDataService
    {
        /// <summary>Fetches the list of available companies from the server.</summary>
        IReadOnlyList<CompanyStruct> GetCompanies();

        /// <summary>Fetches and parses the module tree using the shared IMLRest connection.</summary>
        IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest);
    }
}
