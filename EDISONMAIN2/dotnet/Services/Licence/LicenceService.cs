using ALVA.LICENCE.EDLicence;
using ALWA.SYS;

namespace EDISONMAIN2.Services.Licence;

/// <summary>
/// Tenký wrapper kolem legacy VB třídy <see cref="Activation"/> z EDLicence.dll.
/// V této minimální verzi pouze načítá aktuální licenci ze serveru.
/// </summary>
/// <remarks>
/// <para>
/// Záměrně bere přímo <see cref="MLRest"/>, ne <see cref="EDControls.IMLRest"/> —
/// EDISONMAIN2 už <c>MLRest.dll</c> referencuje a abstrakce není teď k ničemu.
/// </para>
/// <para>
/// <b>Single-threaded usage only.</b> <see cref="Activation"/> uvnitř přepisuje
/// modul-level globál <c>ALVA.LICENCE.EDLicence.Api.myMLREDL</c>, takže paralelní
/// volání ze dvou threadů by se navzájem rušilo. Dokud je service volaný jen
/// z jednoho callsite (login flow), je to bezpečné. Lock přidáme až bude potřeba.
/// </para>
/// </remarks>
public class LicenceService(MLRest mlRest)
{
    /// <summary>
    /// Načte aktuální licenci ze serveru.
    /// </summary>
    /// <returns>
    /// Aktivní <see cref="Lic"/>, nebo <c>null</c> pokud server žádnou neeviduje.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Server vrátil chybu (typicky problém spojení nebo dekryptování).
    /// </exception>
    public Lic? GetActiveLicence()
    {
        // Activation.New(ByRef mlr) i GetActiveLic(ByRef err, ByRef info) jsou ByRef
        // (i když je VB nepřepisuje); C# vyžaduje 'ref' a místní proměnné.
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
