using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    /// <summary>
    /// Changelog z legacy .INF souborů: &lt;klíč&gt;.INF = celá historie, &lt;klíč&gt;_DIFF.INF = změny
    /// od poslední instalace (generuje server-updater; jeho existence = „jsou nové změny").
    /// </summary>
    public class InfChangelogSource : IChangelogSource
    {
        // Staré INF jsou ANSI CP1250 bez BOM, nové UTF-8 s BOM - sniff jako legacy frmInformation.
        private static readonly Encoding Cp1250;

        static InfChangelogSource()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Cp1250 = Encoding.GetEncoding(1250);
        }

        private readonly string[] _searchDirs;

        /// <summary>Bez argumentů hledá vedle exe a v C:\Edison (politika ResolveExecutablePath - DEBUG běží z bin).</summary>
        public InfChangelogSource(params string[]? searchDirs) =>
            _searchDirs = searchDirs is { Length: > 0 }
                ? searchDirs
                : [AppContext.BaseDirectory, EDControls.EDViewModel.WindowNavigator.EdisonDeployRoot];

        public Task<ChangelogContent?> LoadAsync(string moduleKey) => Task.Run(() => LoadSync(moduleKey));

        private ChangelogContent? LoadSync(string moduleKey)
        {
            try
            {
                var key = InfKeyFor(moduleKey);
                foreach (var dir in _searchDirs)
                {
                    var fullPath = Path.Combine(dir, key + ".INF");
                    // DIFF jen ze stejného adresáře jako full - mix dev kopie a produkčního diffu by lhal.
                    var diffPath = Path.Combine(dir, key + "_DIFF.INF");
                    bool hasFull = File.Exists(fullPath), hasDiff = File.Exists(diffPath);
                    if (!hasFull && !hasDiff) continue;

                    var changes = hasDiff ? ReadInfText(diffPath) : null;
                    if (string.IsNullOrWhiteSpace(changes)) changes = null;
                    var full = hasFull ? ReadInfText(fullPath) : changes ?? "";
                    return new ChangelogContent(key, changes, full,
                        changes is null ? "" : ComputeChangeSetId(changes));
                }
                return null;
            }
            catch (Exception ex)
            {
                // Vadný / nečitelný INF nesmí shodit otevírání agendy - novinky prostě nebudou.
                Debug.WriteLine($"[Changelog] {moduleKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Basename INF souboru pro modul - zrcadlí legacy EDConversion.Package.ToInformationFile
        /// (EDLIBRARY/clsConversion.vb): víc agend sdílí jeden soubor. Neznámý klíč degraduje
        /// na vlastní jméno (soubor nejspíš neexistuje → žádný popup).
        /// </summary>
        internal static string InfKeyFor(string moduleKey)
        {
            var name = Path.GetFileNameWithoutExtension(moduleKey).ToUpperInvariant();
            switch (name)
            {
                case "TISKMHD": return "JR";
                // NDMAIN je v legacy tabulce záměrně vynechaný (má vlastní NDMAIN.INF).
                case "NAKLADNIDOPRAVA":
                case "NDOBJEDNAVKY": return "ND";
                case "POKLADNAEX": return "POKLADNA";
                case "VOZIDLA2": return "VOZIDLA";
                case "AUTOOPRAVNA2": return "AUTOOPRAVNA";
                case "ZAKAZNICI2": return "ZAKAZNICI";
                case "ISYCISELNIKY": return "ISY";
                case "ZAUCTOVANIMZDY": return "ZAUCTOVANI";
                case "DISPECINK2": return "DISPECINK";
            }
            if (name.StartsWith("JR")) return "JR";
            if (name.StartsWith("DS")) return "DS";
            if (name.StartsWith("MHD")) return "MHD";
            if (name.StartsWith("AD")) return "AD";
            return name;
        }

        internal static string ReadInfText(string path)
        {
            using var reader = new StreamReader(path, Cp1250, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        // Hash dekódovaného textu (ne raw bajtů): budoucí převod ANSI→UTF-8 shodného obsahu není změna.
        internal static string ComputeChangeSetId(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
