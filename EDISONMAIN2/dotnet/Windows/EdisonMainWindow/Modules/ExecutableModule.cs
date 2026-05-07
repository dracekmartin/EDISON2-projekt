using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    public class ExecutableModule : IModule
    {
        public string DisplayedName { get; set; } = "";

        public string ExecutablePath { get; set; } = "";

        public string FormName { get; set; } = "";

        /// <summary>
        /// Numerické ID prvku z EDISFormEnum jako string. Slouží jako unikátní
        /// identifikátor uzlu (pro budoucí navigaci, tracking historie, atd.)
        /// a zároveň jako klíč při licenční gate, pokud <see cref="IsLicensed"/>
        /// je true.
        /// </summary>
        public string ElementId { get; set; } = "";

        /// <summary>
        /// Pokud true, modul je licenčně gateovaný (porovnává se
        /// <see cref="ElementId"/> proti <c>Lic.Elements</c> whitelistu).
        /// Pokud false, modul se zobrazí vždy bez ohledu na licenci.
        /// Sémantika převzata z atributu <c>licensed="1"</c> ve starém
        /// <c>edisontree.xml</c>.
        /// </summary>
        public bool IsLicensed { get; set; } = false;
    }
}
