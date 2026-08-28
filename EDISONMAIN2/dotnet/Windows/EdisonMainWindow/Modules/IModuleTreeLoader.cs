using EDControls;
using System.Collections.Generic;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public interface IModuleTreeLoader
    {
        /// <summary>
        /// Načte a naparsuje strom modulů přes sdílené IMLRest spojení.
        /// <paramref name="treeName"/>: "main" (Hlavní nabídka, default), "system" (Správa systému),
        /// "partners" (Partnerská řešení).
        /// </summary>
        IReadOnlyList<IModule> GetModuleTree(IMLRest mlRest, string treeName = "main");
    }
}
