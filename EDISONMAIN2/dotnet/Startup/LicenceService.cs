using ALVA.LICENCE.EDLicence;
using ALWA.SYS;

namespace EDISONMAIN2;

/// <summary>
/// Wrapper kolem legacy VB <see cref="Activation"/> z EDLicence.dll.
/// Single-threaded - Activation přepisuje modul-level globál v EDLicence.
/// </summary>
public class LicenceService(MLRest mlRest)
{
    /// <summary>
    /// Vrátí aktivní licenci; bez licence na serveru vrátí fallback (<see cref="CreateFallbackLicence"/>).
    /// </summary>
    public Lic? GetActiveLicence()
    {
        var lic = LoadActiveLic(out string err);
        if (!string.IsNullOrEmpty(err))
            throw new InvalidOperationException(err);
        return lic ?? CreateFallbackLicence();
    }

    /// <summary>Seam kolem legacy <see cref="Activation"/> (EDLicence.dll) - reálná DLL chce
    /// živou session, testy hook přepisují.</summary>
    protected virtual Lic? LoadActiveLic(out string err)
    {
        // Activation.New a GetActiveLic jsou v VB ByRef → potřebujeme lokální proměnné.
        var ml = mlRest;
        var activation = new Activation(ref ml);
        err = "";
        string info = "";
        return activation.GetActiveLic(ref err, ref info);
    }

    /// <summary>
    /// Default licence "Nezadána" - legacy frmNoMDI.GetActiveLicense. Bez ní by VerifyLic
    /// s CountsLOGIN=0 vždy vrátil -1 (nikdo se nepřihlásí) a instalace bez licence by se
    /// zamkla; takhle se jeden uživatel dostane do shellu a admin přes Správa systému >
    /// Společná evidence > EDISON licenci naimportuje.
    /// </summary>
    internal static Lic CreateFallbackLicence() => new()
    {
        Version = "1.003",
        Type = "Nezadána",
        CountsLOGIN = 1,
        Elements = [AccessLoader.SpolecnaEvidence, AccessLoader.FormEdisonmainSettingsShow],
    };
}
