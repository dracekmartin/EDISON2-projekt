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

        /// <summary>Numerické ID z EDISFormEnum (string). Klíč pro licenční gate; jinak unikátní identifikátor uzlu.</summary>
        public string ElementId { get; set; } = "";

        /// <summary>Když true, <see cref="ElementId"/> se kontroluje proti <c>Lic.Elements</c>; jinak modul vidí každý.</summary>
        public bool IsLicensed { get; set; } = false;
    }
}
