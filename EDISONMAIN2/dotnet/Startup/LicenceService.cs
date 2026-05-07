using ALVA.LICENCE.EDLicence;
using ALWA.SYS;

namespace EDISONMAIN2;

/// <summary>
/// Wrapper kolem legacy VB <see cref="Activation"/> z EDLicence.dll.
/// Single-threaded — Activation přepisuje modul-level globál v EDLicence.
/// </summary>
public class LicenceService(MLRest mlRest)
{
    /// <summary>Vrátí aktivní licenci, nebo <c>null</c> pokud server žádnou neeviduje.</summary>
    public Lic? GetActiveLicence()
    {
        // Activation.New a GetActiveLic jsou v VB ByRef → potřebujeme lokální proměnné.
        var ml = mlRest;
        var activation = new Activation(ref ml);
        string err = "";
        string info = "";
        var lic = activation.GetActiveLic(ref err, ref info);
        if (!string.IsNullOrEmpty(err))
            throw new InvalidOperationException(err);
        return lic;
    }
}
