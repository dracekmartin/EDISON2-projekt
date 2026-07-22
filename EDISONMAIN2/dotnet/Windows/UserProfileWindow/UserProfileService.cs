using System.Collections.Generic;
using EDControls;

namespace EDISONMAIN2.Windows.UserProfileWindow
{
    /// <summary>
    /// Profil uživatele ze serveru: osobní export adresář (+ příznak použití), centrální
    /// export adresář a id výchozího okna. Výchozí okno je per-uživatel × IDFa
    /// (Caché feature #25) — přepnutí firmy může vrátit jiný.
    /// </summary>
    public record UserProfileData(bool UsePersonDir, string PersonDir, string CentralExportDir, int DefaultWindowId);

    /// <summary>
    /// Wire vrstva okna „Vlastnosti uživatele". Legacy Caché metody čtou/píšou přes numerické
    /// subscripty <c>^||ML("IN",N)</c>, proto <c>object[]</c> (ne CVar) — stejný důvod jako
    /// <see cref="EDISONMAIN2.Windows.LoginWindow.UserAuthenticator"/>.
    /// </summary>
    public static class UserProfileService
    {
        /// <summary>
        /// <c>ALVA.EDISON.Main.LoadUser</c> (řádky <c>personDir~příznak~cesta</c>, <c>form~id</c>)
        /// + <see cref="LoadCentralExportDir"/>. Řádek <c>form</c> server pošle jen když ≠ 0.
        /// </summary>
        public static UserProfileData Load(IMLRest mlRest)
        {
            var user = mlRest.QueryOrThrow("ALVA.EDISON.Main", "LoadUser", new object[] { });

            bool usePersonDir = false;
            string personDir = "";
            int defaultWindowId = 0;

            foreach (var row in ParseRows(user, mlRest.Delimiter))
            {
                switch (row[0])
                {
                    case "personDir" when row.Length >= 3:
                        // Příznak legacy ukládal i jako -1 (VB CInt(True)) → nenulové = true.
                        usePersonDir = int.TryParse(row[1], out var flag) && flag != 0;
                        personDir = row[2];
                        break;
                    case "form" when row.Length >= 2:
                        int.TryParse(row[1], out defaultWindowId);
                        break;
                }
            }

            return new UserProfileData(usePersonDir, personDir, LoadCentralExportDir(mlRest), defaultWindowId);
        }

        /// <summary>
        /// Centrální export adresář z <c>ALVA.EDISON.Main.LoadSettings</c> (řádek
        /// <c>system~export~cesta</c>). Prázdný řetězec = nenastaven.
        /// </summary>
        public static string LoadCentralExportDir(IMLRest mlRest)
        {
            var settings = mlRest.QueryOrThrow("ALVA.EDISON.Main", "LoadSettings", new object[] { });
            foreach (var row in ParseRows(settings, mlRest.Delimiter))
                if (row.Length >= 3 && row[0] == "system" && row[1] == "export")
                    return row[2];
            return "";
        }

        /// <summary>
        /// <c>ALVA.EDISON.Main.SaveUser</c> — uloží osobní export adresář jako řádek
        /// <c>personDir~0/1~cesta</c> (příznak ukládáme kanonicky 0/1).
        /// </summary>
        public static void SaveExportDir(IMLRest mlRest, bool usePersonDir, string personDir)
        {
            var row = string.Join(mlRest.Delimiter, "personDir", usePersonDir ? "1" : "0", personDir);
            mlRest.QueryOrThrow("ALVA.EDISON.Main", "SaveUser", new object[] { row });
        }

        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.GetUserDefaultForm</c> — id výchozího okna (0 = žádný),
        /// per-uživatel × aktuální IDFa. Dedikovaná metoda pro startup-form logiku shellu.
        /// </summary>
        public static int LoadDefaultWindow(IMLRest mlRest)
        {
            var output = mlRest.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", new object[] { });
            return output is { Length: > 1 } && int.TryParse(output[1]?.ToString(), out var id) ? id : 0;
        }

        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.SetUserDefaultForm</c> — nastaví (0 = zruší) výchozí okno
        /// pro aktuální IDFa.
        /// </summary>
        public static void SetDefaultWindow(IMLRest mlRest, int formId) =>
            mlRest.QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm", new object[] { formId });

        // OUT: index 0 = errorState (přeskoč), 1.. = datové řádky "klíč~v1~v2".
        private static IEnumerable<string[]> ParseRows(object[] output, char delimiter)
        {
            for (int i = 1; i < output.Length; i++)
            {
                var s = output[i]?.ToString();
                if (!string.IsNullOrEmpty(s)) yield return s.Split(delimiter);
            }
        }
    }
}
