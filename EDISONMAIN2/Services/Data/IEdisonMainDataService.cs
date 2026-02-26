using System.Collections.Generic;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Services.Data
{
    public interface IEdisonMainDataService
    {
        /// <summary>Fetches the list of available companies from the server.</summary>
        IReadOnlyList<CompanyStruct> GetCompanies();

        /// <summary>Fetches and parses the module tree for a specific company namespace.</summary>
        IReadOnlyList<IModule> GetModuleTree(string database, string idfa);
    }
}
