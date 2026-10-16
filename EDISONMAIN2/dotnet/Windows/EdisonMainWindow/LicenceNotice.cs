using System.Globalization;
using ALVA.LICENCE.EDLicence;
using EDControls;

namespace EDISONMAIN2.Windows.EdisonMainWindow
{
    /// <summary>
    /// Upozornění nástěnky starého shellu (<c>frmMain.vb:813-906</c>) pro chip ve status baru:
    /// stará verze, blížící se nebo proběhlý konec licence, překročené počty vozidel a zaměstnanců.
    /// </summary>
    public static class LicenceNotice
    {
        /// <summary>Legacy <c>WEBINFO_LicenseDaysLeftZone</c>.</summary>
        internal const int DaysLeftZone = 7;
        internal const int VersionMaxAgeMonths = 2;

        /// <param name="Short">Text chipu.</param>
        /// <param name="Full">Věty starého shellu do tooltipu.</param>
        public sealed record Notice(string Short, string Full);

        /// <summary>
        /// Pořadí a texty jako legacy. Jediná odchylka: legacy text o počtech přepsal „Licence EDISON skončí",
        /// tady se obě věty spojí (chip ukáže první).
        /// </summary>
        public static Notice? Build(Lic? licence, DateTime today, string version, int? vehicles, int? employees)
        {
            if (IsVersionTooOld(version, today))
                return new("Stará verze EDISON",
                           "Verze EDISON je příliš stará, funkčnost může být omezena, proveďte aktualizaci.");

            var shorts = new List<string>();
            var fulls = new List<string>();
            if (licence != null && TryParseDate(licence.ValidityTo, out var validTo))
            {
                int daysLeft = (validTo.Date - today.Date).Days;
                if (daysLeft < 0)
                    return new("Licence skončila", "Licence EDISON skončila, není možné vkládat data.");
                if (daysLeft <= DaysLeftZone)
                {
                    var when = DaysLeftText(daysLeft);
                    shorts.Add($"Licence skončí {when}");
                    fulls.Add($"Licence EDISON skončí {when}.");
                }
            }

            bool overVehicles = licence is { CountsRZ: > 0 } && vehicles > licence.CountsRZ;
            bool overEmployees = licence is { CountsZAM: > 0 } && employees > licence.CountsZAM;
            if (overVehicles && overEmployees)
            {
                shorts.Add("Překročen počet vozidel a zaměstnanců");
                fulls.Add("Je překročen počet aktivních VOZIDEL a ZAMĚSTNANCŮ oproti licenci, funkčnost EDISON může být omezena.");
            }
            else if (overVehicles)
            {
                shorts.Add("Překročen počet vozidel");
                fulls.Add("Je překročen počet aktivních VOZIDEL oproti licenci, funkčnost EDISON může být omezena.");
            }
            else if (overEmployees)
            {
                shorts.Add("Překročen počet zaměstnanců");
                fulls.Add("Je překročen počet aktivních ZAMĚSTNANCŮ oproti licenci, funkčnost EDISON může být omezena.");
            }

            return shorts.Count == 0 ? null : new(shorts[0], string.Join(" ", fulls));
        }

        private static string DaysLeftText(int days) => days switch
        {
            0 => "dnes",
            1 => "za 1 den",
            2 or 3 or 4 => $"za {days} dny",
            _ => $"za {days} dnů",
        };

        /// <summary>Legacy <c>IsDateOlderThanXMonths</c>: první segment verze je <c>yyMMdd</c> (rok + 2000).</summary>
        internal static bool IsVersionTooOld(string version, DateTime today)
        {
            var yyMMdd = version.Split('.')[0];
            if (!DateTime.TryParseExact(yyMMdd, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var built))
                return false;
            // Legacy srovnává s Now včetně času, takže den přesně před dvěma měsíci už je „starý".
            return built <= today.Date.AddMonths(-VersionMaxAgeMonths);
        }

        // Legacy Date.TryParse s kulturou stanice (cs-CZ); ISO tvar projde v obou.
        private static bool TryParseDate(string? value, out DateTime date) =>
            DateTime.TryParse(value, CultureInfo.GetCultureInfo("cs-CZ"), DateTimeStyles.None, out date);

        /// <summary>Provozovaná vozidla firmy (legacy <c>EDAPI.VOZIDLA.Licence.PocetProvozovanychVozidel</c>).
        /// Chyba (balík VOZIDLA u zákazníka chybí) = null, varování se nezobrazí - legacy ji také spolkl.</summary>
        public static int? LoadVehicleCount(IMLRest mlRest)
        {
            try
            {
                // object[]: legacy endpoint čte IN(index) jako "klíč{D}hodnota".
                var raw = mlRest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel",
                                              new object[] { $"pro{mlRest.Delimiter}{mlRest.Idfa}" });
                return ParseNamedCount(raw, mlRest.Delimiter, "celkem");
            }
            catch (Exception) { return null; }
        }

        /// <summary>Zaměstnanci firmy (legacy <c>ALVA.MZDY2API.DB.gPocZam</c>); chyba = null jako u vozidel.
        /// Není jen čtení: server počet přepočítá, uloží do <c>^MZ(dz,"Licence","PocZam")</c> a pošle do
        /// telemetrie - legacy to dělal při každém načtení plochy.</summary>
        public static int? LoadEmployeeCount(IMLRest mlRest)
        {
            try
            {
                var raw = mlRest.QueryOrThrow("ALVA.MZDY2.MLAPI", "gPocZam", new object[] { });
                return ParseNamedCount(raw, mlRest.Delimiter, "PocZam");
            }
            catch (Exception) { return null; }
        }

        /// <summary>Číslo z řádku <c>klíč{D}n</c>; chybějící řádek nebo nečíselná hodnota = null.</summary>
        internal static int? ParseNamedCount(object[] raw, char delimiter, string key) =>
            int.TryParse(LegacyOutput.Named(raw, delimiter, key), out var count) ? count : null;
    }
}
