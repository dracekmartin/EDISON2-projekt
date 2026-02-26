using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EDISONMAIN2.Modules
{
    public class ExecutableModule : IModule
    {
        public string DisplayedName { get; set; } = "";

        public string ExecutablePath { get; set; } = "";

        public string FormName { get; set; } = "";
    }
}
