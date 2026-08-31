using System;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.LoginWindow;

namespace EDISONMAIN2;

/// <summary>Connection info - vstup do <see cref="SessionFactory.Establish"/>. Sdílí initial bootstrap a user switch.</summary>
public record ConnectionInfo(string Server, int Port, string Database, string Idfa);

/// <summary>Postlogin kontext (real MLRest + licence loader factory).</summary>
public record SessionContext(IMLRest MLRest, LoginResult Login, Func<Lic?> LicenceLoader);

/// <summary>
/// Vytvoření session pro přihlášeného uživatele - jediné místo, kde se skládá real MLRest
/// a napojuje do <see cref="EDServices"/>. Sdílí ho startup (<see cref="AppBootstrapper"/>)
/// i přihlášení jiného uživatele (<see cref="SessionSwitcher"/>); drift mezi oběma cestami
/// (UserPrefs cache, LicenceService, IsAdmin) by znamenal službu ukazující na starou session.
/// </summary>
public static class SessionFactory
{
    public static SessionContext Establish(ConnectionInfo conn, LoginResult login, EDServices services)
    {
        var inner = new MLRest(conn.Server, conn.Port, conn.Database, conn.Idfa)
        {
            LoginName = login.UserName,
            FullName = login.FullName,
            Mlp = login.Mlp.ToString(),
#if DEBUG
            // Dev verze 999999.9 nikdy nesedí s Caché → per-request version gate (Locks=1)
            // by shodil každý request. Prázdná verze = gate na serveru neběží.
            EdisonVersion = ""
#else
            // Aktivuje server-side per-request version gate (prázdná verze = gate neběží).
            EdisonVersion = VerzeInf.Current
#endif
        };
#if DEBUG
        // Deterministický GUID v dev: Stop Debugging (hard-kill) ACTIVITY zámek neuvolní, ale
        // příští spuštění reusne stejný klíč (idfa~ip~guid) → zámek se přepíše, neakumuluje se
        // (a neroste falešně licenční počet). V produkci náhodný GUID per session (default).
        inner.GUID = $"DEV-{Environment.MachineName}-{Environment.UserName}";
#endif
        var mlRest = new MLRestAdapter(inner);

        services.IsAdmin = login.IsAdmin;
        services.MayUpdate = login.MayUpdate;
        // services.MLRest se musí nastavit už tady - admin override v LicenceVerifier spouští
        // USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection z EDServices.Current.MLRest.
        services.MLRest = mlRest;
        services.UserPrefs = new CacheUserPrefsService(mlRest);

        var licenceService = new LicenceService(mlRest.Inner);
        services.LicenceLoader = licenceService.GetActiveLicence;
        return new SessionContext(mlRest, login, licenceService.GetActiveLicence);
    }
}
