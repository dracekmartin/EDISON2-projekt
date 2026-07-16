using System;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Port legacy <c>ALVA.CORE.Network.IsDomainMline</c> (EDCORE/clsCore.vb) — gate pro
    /// mlineOnly uzly stromu: interní M-line nástroje se mimo doménu M-line nezobrazují.
    /// </summary>
    internal static class MlineDomainDetector
    {
        /// <summary>Suffix názvu mlineOnly uzlů v doméně M-line (legacy frmMain.vb:1228).</summary>
        internal const string Suffix = " [### M-line ###]";

        /// <summary>
        /// True = stanice v doméně M-line: DNS doména <c>main.m-line.cz</c>, nebo dev override
        /// v registry <c>HKCU\Software\Edison\MlineDomain</c> (legacy SetPcVirtualDomainMline).
        /// </summary>
        public static bool IsDomainMline() =>
            GetDomainName().Equals("main.m-line.cz", StringComparison.OrdinalIgnoreCase)
            || GetVirtualDomainFlag();

        private static string GetDomainName()
        {
            try { return IPGlobalProperties.GetIPGlobalProperties().DomainName; }
            catch { return ""; }
        }

        private static bool GetVirtualDomainFlag()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Edison");
                var value = key?.GetValue("MlineDomain")?.ToString();
                // Legacy zapisuje "0"/"1" a čte přes VB CBool — nenulové číslo i "True" je true.
                if (value == null) return false;
                if (bool.TryParse(value, out var b)) return b;
                return int.TryParse(value, out var i) && i != 0;
            }
            catch { return false; }
        }
    }
}
