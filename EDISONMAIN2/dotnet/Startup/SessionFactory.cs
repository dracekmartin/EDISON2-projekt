using System;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDControls.UserPrefs;

namespace EDISONMAIN2;

/// <summary>Connection info - vstup do <see cref="SessionFactory.Establish"/>. Sdílí initial bootstrap a user switch.</summary>
public record ConnectionInfo(string Server, int Port, string Database, string Idfa);

/// <summary>Postlogin kontext. Loader licence nese jen <see cref="EDServices.LicenceLoader"/>.</summary>
public record SessionContext(IMLRest MLRest, LoginResult Login);

/// <summary>
/// Vytvoření session pro přihlášeného uživatele - jediné místo, kde se skládá real MLRest
/// a napojuje do <see cref="EDServices"/>. Sdílí ho startup (<see cref="AppBootstrapper"/>)
/// i přihlášení jiného uživatele (<see cref="SessionSwitcher"/>); drift mezi oběma cestami
/// (UserPrefs cache, LicenceService, IsAdmin) by znamenal službu ukazující na starou session.
/// </summary>
public static class SessionFactory
{
    /// <param name="options">Verze stanice a session GUID - stejné pro start i přihlášení jiného uživatele.</param>
    public static SessionContext Establish(ConnectionInfo conn, LoginResult login, EDServices services,
                                           StartupOptions options)
    {
        var inner = new MLRest(conn.Server, conn.Port, conn.Database, conn.Idfa)
        {
            LoginName = login.UserName,
            FullName = login.FullName,
            Mlp = login.Mlp.ToString(),
            EdisonVersion = options.SendsVersion ? VerzeInf.Current : ""
        };
        // Bez tagu zůstává náhodný GUID per session (default MLRest).
        if (options.SessionGuidTag is { } tag)
            inner.GUID = $"{tag}-{Environment.MachineName}-{Environment.UserName}";
        var mlRest = new MLRestAdapter(inner);

        services.IsAdmin = login.IsAdmin;
        services.MayUpdate = login.MayUpdate;
        services.UserDirectory = login.Directory;
        services.UserEmail = login.Email;
        // services.MLRest se musí nastavit už tady - admin override v LicenceVerifier spouští
        // USERS.exe přes WindowNavigator.ShowLegacyAndWait, ten čte connection z EDServices.Current.MLRest.
        services.MLRest = mlRest;
        services.UserPrefs = new CacheUserPrefsService(mlRest);

        services.LicenceLoader = LegacyApi.ActiveLicenceLoader(mlRest, message => services.PopupDisplayer.InfoSystem(message));
        return new SessionContext(mlRest, login);
    }
}
