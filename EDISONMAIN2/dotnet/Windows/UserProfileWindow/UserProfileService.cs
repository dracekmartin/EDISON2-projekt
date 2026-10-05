using System.Collections.Generic;
using EDControls;

namespace EDISONMAIN2.Windows.UserProfileWindow
{
    /// <summary>
    /// Profil uživatele ze serveru: osobní export adresář (+ příznak použití) a centrální
    /// export adresář. Výchozí okno jen přes <see cref="UserProfileService.LoadDefaultWindow"/>.
    /// </summary>
    public record UserProfileData(bool UsePersonDir, string PersonDir, string CentralExportDir);

    /// <summary>
    /// Wire vrstva okna „Vlastnosti uživatele". Legacy Caché metody čtou/píšou přes numerické
    /// subscripty <c>^||ML("IN",N)</c>, proto <c>object[]</c> (ne CVar) - stejný důvod jako
    /// <see cref="EDISONMAIN2.Windows.LoginWindow.UserAuthenticator"/>.
    /// </summary>
    public static class UserProfileService
    {
        /// <summary>
        /// <c>ALVA.EDISON.Main.LoadUser</c> (řádek <c>personDir~příznak~cesta</c>) + <see cref="LoadCentralExportDir"/>.
        /// Řádek <c>form</c> téže odpovědi čte jen legacy shell; výchozí okno je <see cref="LoadDefaultWindow"/>.
        /// </summary>
        public static UserProfileData Load(IMLRest mlRest)
        {
            var user = mlRest.QueryOrThrow("ALVA.EDISON.Main", "LoadUser", new object[] { });

            bool usePersonDir = false;
            string personDir = "";

            foreach (var row in ParseRows(user, mlRest.Delimiter))
            {
                if (row[0] == "personDir" && row.Length >= 3)
                {
                    // Příznak legacy ukládal i jako -1 (VB CInt(True)) → nenulové = true.
                    usePersonDir = int.TryParse(row[1], out var flag) && flag != 0;
                    personDir = row[2];
                }
            }

            return new UserProfileData(usePersonDir, personDir, LoadCentralExportDir(mlRest));
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
        /// <c>ALVA.EDISON.Main.SaveUser</c> - uloží osobní export adresář jako řádek
        /// <c>personDir~0/1~cesta</c> (příznak ukládáme kanonicky 0/1).
        /// </summary>
        public static void SaveExportDir(IMLRest mlRest, bool usePersonDir, string personDir)
        {
            var row = string.Join(mlRest.Delimiter, "personDir", usePersonDir ? "1" : "0", personDir);
            mlRest.QueryOrThrow("ALVA.EDISON.Main", "SaveUser", new object[] { row });
        }

        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.GetUserDefaultForm</c> - id výchozího okna (0 = žádný),
        /// per-uživatel × aktuální IDFa. Dedikovaná metoda pro startup-form logiku shellu.
        /// </summary>
        public static int LoadDefaultWindow(IMLRest mlRest)
        {
            var output = mlRest.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", new object[] { });
            return output is { Length: > 1 } && int.TryParse(output[1]?.ToString(), out var id) ? id : 0;
        }

        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.SetUserDefaultForm</c> - nastaví (0 = zruší) výchozí okno
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
