using ALVA.LICENCE.EDLicence;
using ALVA.USERS;
using ALWA.SYS;
using EDControls;
using EDISONMAIN2.Modules;

namespace EDISONMAIN2;

/// <summary>
/// Wrapper kolem legacy VB <see cref="Activation"/> z EDLicence.dll.
/// Single-threaded - Activation přepisuje modul-level globál v EDLicence.
/// </summary>
/// <param name="showInfo">Hláška licence uživateli (popup session).</param>
public class LicenceService(MLRest mlRest, Action<string> showInfo)
{
    /// <summary>
    /// Vrátí aktivní licenci; bez licence na serveru vrátí fallback (<see cref="CreateFallbackLicence"/>).
    /// Hlášku licence (prošlá, neplatný klíč, neaktivovaná) ukáže při každém načtení jako legacy
    /// <c>frmNoMDI.GetActiveLicense</c> - proto se v jednom toku načítá jen jednou.
    /// </summary>
    public Lic? GetActiveLicence()
    {
        var lic = LoadActiveLic(out string err, out string info);
        if (!string.IsNullOrEmpty(err))
            throw new InvalidOperationException(err);
        if (!string.IsNullOrEmpty(info))
            showInfo(info);
        return lic ?? CreateFallbackLicence();
    }

    /// <summary>Seam kolem legacy <see cref="Activation"/> (EDLicence.dll) - reálná DLL chce
    /// živou session, testy hook přepisují.</summary>
    protected virtual Lic? LoadActiveLic(out string err, out string info)
    {
        // Activation.New a GetActiveLic jsou v VB ByRef → potřebujeme lokální proměnné.
        var ml = mlRest;
        var activation = new Activation(ref ml);
        err = "";
        info = "";
        return activation.GetActiveLic(ref err, ref info);
    }

    /// <summary>
    /// Default licence "Nezadána" - legacy frmNoMDI.GetActiveLicense. Bez ní by VerifyLic
    /// s CountsLOGIN=0 vždy vrátil -1 (nikdo se nepřihlásí) a instalace bez licence by se
    /// zamkla; takhle se jeden uživatel dostane do shellu a admin přes Správa systému >
    /// EDISON > EDISON licenci naimportuje (složka = package 0, viz rename v Tree.cls).
    /// </summary>
    internal static Lic CreateFallbackLicence() => new()
    {
        Version = "1.003",
        Type = "Nezadána",
        CountsLOGIN = 1,
        Elements = [AccessLoader.SpolecnaEvidence, AccessLoader.FormEdisonmainSettingsShow],
    };

    /// <param name="Changed">Změnil se seznam licencí aktuální firmy.</param>
    /// <param name="Error">Chyba stažení nebo feedbacku; licence mohla být i tak uložena.</param>
    public sealed record MlineSyncResult(bool Changed, string Error);

    /// <summary>
    /// Stáhne vystavené licence z M-line do zákaznického serveru a pošle feedback (legacy
    /// „Aktualizovat licenci" i login admina).
    /// Bezpečné na klonu mimo UI thread: EDLicence <c>Update</c> na globál <c>Activation</c> nesahá.
    /// </summary>
    public static MlineSyncResult SyncFromMline(MLRest mlRest)
    {
        var before = LicenceListFingerprint(mlRest);
        var ml = mlRest;
        string err = "";
        // allUsers=true: EDUsers._admin/_canUpdate plní jen EDUsers.Login, který shell nevolá - právo hlídá volající.
        new EDUsers(ref ml).UpdateLicFromMlineServer(ref err, allUsers: true);
        // Porovnat i po chybě: EDUsers licenci uloží dřív, než může selhat feedback (EDUsers.vb:324-327).
        return new(LicenceListFingerprint(mlRest) != before, err ?? "");
    }

    private static string LicenceListFingerprint(MLRest mlRest) =>
        string.Join("\n", mlRest.QueryOrThrow("ALVA.LICENCE.Activation", "GetList", new object[] { }));
}
