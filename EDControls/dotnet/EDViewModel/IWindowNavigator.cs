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
    /// Jako <see cref="ShowLegacy"/>, ale vrací Task, který doběhne až se objeví okno modulu
    /// (<c>Process.MainWindowHandle</c> != 0) nebo po timeoutu / předčasném ukončení procesu.
    /// Caller (shell) tím může držet „akce probíhá" overlay po dobu náběhu modulu.
    /// </summary>
    Task ShowLegacyAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "");

    /// <summary>
    /// Spustí zástupce na ploše (tisková sestava, uložený pohled) - host místo formy zavolá
    /// proceduru agendy (<c>ProcedureIO.RunProcedure</c>). <paramref name="shortcutPayload"/> je
    /// neprůhledný argv-safe string; skládá ho shell, formát viz <c>docs/DESKTOP_SHORTCUTS.md</c>.
    /// Bez dedup a bez trackování mezi spuštěné moduly - je to tisk, ne okno modulu.
    /// </summary>
    Task ShowLegacyShortcutAsync(string exeName, string formFqn, string shortcutPayload,
                                 bool shareShellSession = true, string inputParam = "");

    /// <summary>
    /// Skončil legacy modul. Agendy zapisují zástupce na plochu přímo do Caché (nemají na shell
    /// spojení), takže tohle je moment, kdy si je shell má přenačíst.
    /// </summary>
    event Action? LegacyModuleExited;

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

    /// <summary>
    /// Jako <see cref="ShowLegacyAsDialog"/>, ale vrací Task, který doběhne až se objeví okno
    /// child procesu (nebo po timeoutu). Shell zůstane zašedlý až do zavření childu; Task jen
    /// umožní callerovi držet „akce probíhá" overlay po dobu náběhu.
    /// </summary>
    Task ShowLegacyAsDialogAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "");
}
