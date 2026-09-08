using System;
using System.Collections.Generic;
using System.IO;

namespace EDISONMAIN2.Windows.EdisonMainWindow.Modules
{
    /// <summary>
    /// Maskot overlaye „Akce probíhá" per agenda: exe agendy → klíč vyhrazeného spritu
    /// v ActionInProgressWindow.xaml (bez prefixu „Sprite."). Agenda bez záznamu = null
    /// (náhodný maskot ze společného poolu).
    /// </summary>
    internal static class AgendaSprites
    {
        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            // Mzdy
            ["MZDY"] = "Money",
            ["MZDY2"] = "Money",
            ["ZauMzdy"] = "Money",
            ["DSPODILOVANI"] = "Money",
            // Účetnictví (ekonomický systém)
            ["ESDOKLADY"] = "Money",
            ["ESFINANCOVANI"] = "Money",
            ["ESKOMPENZACE"] = "Money",
            ["ESMAIN"] = "Money",
            ["ESMAINEX"] = "Money",
            ["ESREPORT"] = "Money",
            ["ESUTILITY"] = "Money",
            ["PREVODYES"] = "Money",
            // Fakturace a pokladny
            ["FAKTURACEEX"] = "Money",
            ["POKLADNA"] = "Money",
            ["POKLADNAEX"] = "Money",
            ["PREPRAVNIPOKLADNA"] = "Money",
            // Dotace, jízdné, prodej
            ["DOTACE"] = "Money",
            ["BCCENAJIZDNEHO"] = "Money",
            ["BCCLEARING"] = "Money",
            ["PRODEJ"] = "Money",
        };

        /// <summary>Strom nese exe s příponou („MZDY2.exe") - klíčuje se holé jméno.</summary>
        public static string? ForExe(string exeName) =>
            Map.GetValueOrDefault(Path.GetFileNameWithoutExtension(exeName));
    }
}
