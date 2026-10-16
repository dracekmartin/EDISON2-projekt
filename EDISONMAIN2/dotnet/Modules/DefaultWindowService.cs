using EDControls;

namespace EDISONMAIN2.Modules
{
    /// <summary>
    /// Výchozí okno (modul auto-otevřený po loginu) per-uživatel × IDFa. Legacy Caché metody čtou
    /// přes numerické subscripty <c>^||ML("IN",N)</c>, proto <c>object[]</c> (ne CVar).
    /// </summary>
    public static class DefaultWindowService
    {
        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.GetUserDefaultForm</c> - id výchozího okna (0 = žádný),
        /// per-uživatel × aktuální IDFa. Dedikovaná metoda pro startup-form logiku shellu.
        /// </summary>
        public static int Load(IMLRest mlRest)
        {
            var output = mlRest.QueryOrThrow("ALVA.USERS.ApiEdison", "GetUserDefaultForm", new object[] { });
            return int.TryParse(LegacyOutput.Values(output).FirstOrDefault(), out var id) ? id : 0;
        }

        /// <summary>
        /// <c>ALVA.USERS.ApiEdison.SetUserDefaultForm</c> - nastaví (0 = zruší) výchozí okno
        /// pro aktuální IDFa.
        /// </summary>
        public static void Set(IMLRest mlRest, int formId) =>
            mlRest.QueryOrThrow("ALVA.USERS.ApiEdison", "SetUserDefaultForm", new object[] { formId });
    }
}
