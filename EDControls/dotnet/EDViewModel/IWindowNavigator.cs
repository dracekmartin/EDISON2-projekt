namespace EDControls.EDViewModel;

public interface IWindowNavigator
{
    /// <summary>Otevře okno modeless v entry assembly.</summary>
    void Show(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Otevře okno modeless v zadané assembly.</summary>
    void Show(string windowTypeName, string assemblyName, EDWindowInput? input = null);

    /// <summary>Otevře okno jako modální dialog v entry assembly.</summary>
    bool? ShowDialog(string windowTypeName, EDWindowInput? input = null);

    /// <summary>Otevře okno jako modální dialog v zadané assembly.</summary>
    bool? ShowDialog(string windowTypeName, string assemblyName, EDWindowInput? input = null);

    /// <summary>
    /// Spustí legacy WinForms formu z jiného .exe přes EDISONMAIN2API.FormToRunGetter kontrakt.
    /// Fire-and-forget. Default <paramref name="shareShellSession"/>=true → modul sdílí shellův
    /// ACTIVITY zámek (CacheLocks=1, vázáno na shell GUID).
    /// </summary>
    void ShowLegacy(string exeName, string formFqn, bool shareShellSession = true);

    /// <summary>
    /// Stejné jako <see cref="ShowLegacy"/>, ale blokuje až do zavření okna.
    /// Pre-login admin override (LicenceVerifier) volá s shareShellSession=false — shell ještě
    /// nemá VerifyLic zámek a CacheLocks=1 by každý request modulu padal na LockExist.
    /// </summary>
    void ShowLegacyAndWait(string exeName, string formFqn, bool shareShellSession = true);

    /// <summary>
    /// Modální varianta <see cref="ShowLegacy"/>: vlastník okna se na dobu běhu child procesu
    /// deaktivuje (uživatel s ním nemůže interagovat), po skončení child procesu se znovu aktivuje.
    /// Calling thread se nezablokuje (jiné než <see cref="ShowLegacyAndWait"/>).
    /// </summary>
    void ShowLegacyAsDialog(string exeName, string formFqn, bool shareShellSession = true);
}
