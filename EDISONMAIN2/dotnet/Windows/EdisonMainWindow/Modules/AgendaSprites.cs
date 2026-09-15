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
        /// <remarks>
        /// Produkce ji nevolá - overlay jede bez klíče sady, tedy s kolečkem. Mapa zůstává pro
        /// případ návratu maskotů; pustit je jde přes „Test funkcí ▸ Akce probíhá".
        /// </remarks>
        public static string? ForExe(string exeName) =>
            Map.GetValueOrDefault(Path.GetFileNameWithoutExtension(exeName));

        // Pořadí = pořadí v nabídce Test funkcí.
        private static readonly (string Set, string Label)[] SetLabels =
        [
            ("Vehicles", "Dopravní prostředky"),
            ("Money", "Peníze"),
        ];

        /// <summary>Všechny sady spritů pro Test funkcí, v pořadí nabídky.</summary>
        public static IReadOnlyList<string> AllSets { get; } = [.. SetLabels.Select(x => x.Set)];

        /// <summary>Český popisek sady pro Test funkcí; neznámá sada zůstává pod svým klíčem.</summary>
        public static string LabelFor(string set) =>
            SetLabels.FirstOrDefault(x => x.Set == set).Label ?? set;
    }

    /// <summary>Varianta overlaye pro Test funkcí; null klíč = načítací kolečko (produkční vzhled).</summary>
    public sealed record OverlayTestVariant(string Label, string? SpriteKey);
}
