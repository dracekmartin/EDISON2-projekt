using System.Windows;

namespace EDControls.EDViewModel;

public interface IWindowNavigator
{
    /// <summary>
    /// Otevře okno modeless. Okno se předává jako instance — caller vytvoří přes <c>new</c>
    /// (compile-time type safety, Find Usages funguje). Navigator nastaví Owner, append
    /// <c>[Idfa]</c> na Title a zavolá <c>AttachToWindow</c> na VM (pokud je <c>EDViewModel</c>).
    /// </summary>
    void Show(Window window);

    /// <summary>Otevře okno jako modální dialog. Stejné wire-up jako <see cref="Show"/>.</summary>
    bool? ShowDialog(Window window);

    /// <summary>
    /// Spustí legacy WinForms formu z jiného .exe přes EDISONMAIN2API.FormToRunGetter kontrakt.
    /// Fire-and-forget. Default <paramref name="shareShellSession"/>=true → modul sdílí shellův
    /// ACTIVITY zámek (CacheLocks=1, vázáno na shell GUID).
    /// <paramref name="inputParam"/> = volitelný string předaný formě jako <c>EDParamIn</c>
    /// (např. "AdminMode=1" — legacy navbar input atribut).
    /// </summary>
    void ShowLegacy(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "");

    /// <summary>
    /// Stejné jako <see cref="ShowLegacy"/>, ale blokuje až do zavření okna.
    /// Pre-login admin override (LicenceVerifier) volá s shareShellSession=false — shell ještě
    /// nemá VerifyLic zámek a CacheLocks=1 by každý request modulu padal na LockExist.
    /// </summary>
    void ShowLegacyAndWait(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "");

    /// <summary>
    /// Modální varianta <see cref="ShowLegacy"/>: vlastník okna se na dobu běhu child procesu
    /// deaktivuje (uživatel s ním nemůže interagovat), po skončení child procesu se znovu aktivuje.
    /// Calling thread se nezablokuje (jiné než <see cref="ShowLegacyAndWait"/>).
    /// </summary>
    void ShowLegacyAsDialog(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "");
}
