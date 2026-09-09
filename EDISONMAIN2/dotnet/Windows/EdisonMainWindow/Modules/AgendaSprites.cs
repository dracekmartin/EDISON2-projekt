using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        /// <summary>Známé sady (distinct hodnoty mapy) - pro Test funkcí.</summary>
        public static IReadOnlyList<string> KnownSets { get; } =
            [.. Map.Values.Distinct().OrderBy(v => v, StringComparer.Ordinal)];

        private static readonly Dictionary<string, string> SetLabels = new()
        {
            ["Vehicles"] = "Dopravní prostředky",
            ["Money"] = "Peníze",
        };

        /// <summary>Český popisek sady pro Test funkcí; neznámá sada zůstává pod svým klíčem.</summary>
        public static string LabelFor(string set) => SetLabels.GetValueOrDefault(set, set);
    }

    /// <summary>Varianta overlaye pro Test funkcí; null klíč = výchozí sada (dopravní prostředky).</summary>
    public sealed record OverlayTestVariant(string Label, string? SpriteKey);
}
