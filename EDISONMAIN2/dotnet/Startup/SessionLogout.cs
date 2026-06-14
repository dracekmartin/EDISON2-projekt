using System;
using System.Diagnostics;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Uvolnění ACTIVITY zámku přes <c>ALVA.SYS.Login.Logout</c> (idfa/ip/guid ze sys kontextu).
/// Zámek je v temp globálu — se zánikem spojení se neuvolní, musí se explicitně odhlásit.
/// Best-effort: selhání logoutu nesmí blokovat zavření ani přepnutí.
/// </summary>
internal static class SessionLogout
{
    /// <summary>
    /// Odhlásí session. Volá se na klonu (<see cref="IMLRest.With"/>) s <c>CacheLocks=0</c> (request
    /// nesmí padnout na LockExist) — sdílenou instanci nemutuje. <paramref name="idfa"/> /
    /// <paramref name="dataNamespace"/> přepíší cílovou firmu (company switch uvolňuje zámek staré
    /// firmy mid-session); null = aktuální session (close / switch-user).
    /// </summary>
    internal static void Logout(IMLRest? mlRest, string? idfa = null, string? dataNamespace = null)
    {
        if (mlRest is null) return;
        try
        {
            mlRest.With(cacheLocks: 0, idfa: idfa, dataNamespace: dataNamespace)
                  .QueryOrThrow("ALVA.SYS.Login", "Logout", new object[] { });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SessionLogout failed: {ex.Message}");
        }
    }
}
