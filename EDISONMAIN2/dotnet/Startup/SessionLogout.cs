using System;
using System.Diagnostics;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Uvolnění ACTIVITY zámku (<c>ALVA.SYS.Login.Logout</c>, idfa/ip/guid ze sys kontextu)
/// + business zámků session (<c>ALWA.SYS.Lock.LockClearExt</c> dle GUID) - parita s legacy
/// <c>EDUsers.Logout</c>. Zámky se zánikem spojení nezaniknou, musí se explicitně uklidit;
/// pod shell GUID zamykají i hostované legacy moduly (CacheLocks=1), které se zavírají
/// killem, takže jejich zámky uklidí až tenhle logout.
/// Best-effort: selhání nesmí blokovat zavření ani přepnutí.
/// </summary>
internal static class SessionLogout
{
    /// <summary>
    /// Odhlásí session. Volá se na klonu (<see cref="IMLRest.With"/>) s <c>CacheLocks=0</c> (request
    /// nesmí padnout na LockExist). <paramref name="idfa"/> / <paramref name="dataNamespace"/> přepíší
    /// cílovou firmu (company switch uvolňuje zámky staré firmy mid-session, sdílenou instanci nemění);
    /// null = aktuální session (close / switch-user), ta se předem označí <c>CacheLocks=0</c>.
    /// </summary>
    internal static void Logout(IMLRest? mlRest, string? idfa = null, string? dataNamespace = null)
    {
        if (mlRest is null) return;
        try
        {
            var clone = mlRest.With(cacheLocks: 0, idfa: idfa, dataNamespace: dataNamespace);
            // Před requestem: klon téže session, který zrovna běží na pozadí, by zánik zámku po logoutu
            // přeposlal jako konec aplikace (MLRestAdapter.With) - u switch-user by shodil nového uživatele.
            // Až po With(): když klon nevznikne, logout neproběhne a session má dál hlídat zámek.
            if (idfa is null && dataNamespace is null) mlRest.CacheLocks = 0;
            try
            {
                // object[]: legacy endpoint bez vstupních parametrů (identita jde v sys kontextu).
                clone.QueryOrThrow("ALVA.SYS.Login", "Logout", new object[] { });
            }
            catch (Exception ex)
            {
                // Nezávislé na LockClearExt níž - selhání ACTIVITY logoutu nesmí nechat viset
                // business zámky (ty neuklidí ani server-side idle reaper, ten bere jen ACTIVITY).
                Debug.WriteLine($"SessionLogout (ACTIVITY) failed: {ex.Message}");
            }
            clone.QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", new object[] { clone.Guid });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SessionLogout failed: {ex.Message}");
        }
    }
}
