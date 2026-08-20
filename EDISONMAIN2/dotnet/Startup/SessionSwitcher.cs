using System;
using ALVA.LICENCE.EDLicence;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;

namespace EDISONMAIN2;

/// <summary>
/// Výsledek pokusu o přepnutí uživatele. <see cref="Outcome"/> rozlišuje úspěch, zrušení
/// v login dialogu a selhání licence pro nového uživatele.
/// </summary>
public record SwitchUserOutcome(SwitchUserResult Outcome, Lic? Licence);

public enum SwitchUserResult
{
    /// <summary>Uživatel zrušil login dialog, nebo okno s neuloženými daty odmítlo zavření.
    /// Shell pokračuje s původním session beze změny.</summary>
    Cancelled,
    /// <summary>Licence pro nového uživatele přesáhla limit (admin retry / non-admin). Caller obvykle ukončí app.</summary>
    LicenceRefused,
    /// <summary>Admin v LicenceVerifier retry loop zvolil Storno. Caller obvykle ukončí app.</summary>
    LicenceCancelled,
    /// <summary>Nový uživatel přihlášen, MLRest swapnut, licence platná.</summary>
    Success,
}

/// <summary>
/// Přihlášení jiného uživatele do běžícího shellu ("Přihlásit jiného uživatele") - paralela
/// ke <see cref="CompanySwitcher"/>, jen swapuje uživatele místo firmy. Žije po dobu session,
/// takže bootstrap po startu aplikace nemusí zůstat naživu.
///
/// Testovatelnost stejná jako u <see cref="AppBootstrapper"/>: kroky sahající na server / WPF
/// jsou <c>protected virtual</c>.
/// </summary>
/// <param name="services">Session služby; <see cref="EDServices.MLRest"/> se při úspěchu swapne.</param>
/// <param name="ownerWindow">Owner login dialogu - shell okno; čte se lazily, po startu se mění.</param>
/// <param name="wireActivityFailed">Napojení force-logout / version gate handlerů na nový MLRest.</param>
/// <param name="closeAllWindows">Zavření všech oken a legacy modulů staré session;
/// <c>false</c> = okno zavření vetovalo a switch se musí zrušit.</param>
public class SessionSwitcher(
    EDServices services,
    Func<System.Windows.Window?> ownerWindow,
    Action<IMLRest> wireActivityFailed,
    Func<bool> closeAllWindows)
{
    /// <summary>
    /// Prompt → close all → <see cref="SessionFactory.Establish"/> (drží UserPrefs cache
    /// + LicenceService synchronně s MLRest) → wire activity → acquire licence. Při zrušení
    /// v login dialogu nic neudělá; při selhání licence je MLRest už swapnut, caller obvykle
    /// volí shutdown (rollback nelze: closeAllWindows už proběhlo).
    /// </summary>
    public SwitchUserOutcome Switch()
    {
        var current = ((MLRestAdapter)services.RequireMLRest).Inner;
        var login = PromptLogin(current);
        if (login == null) return new(SwitchUserResult.Cancelled, null);

        // Veto okna (neuložená data) ruší switch PŘED logoutem - přeživší okno by jinak
        // pracovalo pod session, která už na serveru neexistuje.
        if (!closeAllWindows()) return new(SwitchUserResult.Cancelled, null);

        // Odhlásit starého uživatele - jinak jeho ACTIVITY zámek (starý GUID) drží licenční slot
        // do server-side timeoutu. Až po confirmu (login != null), před swapem session.
        // Flush telemetrie ještě pod starou session - server bere userID z ní.
        EDUsageTelemetry.Flush(services.MLRest);
        SessionLogout.Logout(services.MLRest);

        var session = EstablishSession(
            new ConnectionInfo(current.Server, current.Port, current.DataNamespace, current.Idfa), login);
        wireActivityFailed(session.MLRest);

        var (outcome, licence) = AcquireSession(session);
        return outcome switch
        {
            LicenceAcquireOutcome.Accepted => new(SwitchUserResult.Success, licence),
            LicenceAcquireOutcome.Refused => new(SwitchUserResult.LicenceRefused, null),
            _ => new(SwitchUserResult.LicenceCancelled, null),
        };
    }

    /// <summary>Login dialog pro user switch - owner je shell okno, prázdné pole (uživatel zadá od začátku).</summary>
    protected virtual LoginResult? PromptLogin(MLRest connection)
    {
        var loginWindow = new Windows.LoginWindow.LoginWindow(connection, showRemember: false)
        {
            Owner = ownerWindow(),
        };
        return loginWindow.ShowDialog() == true ? loginWindow.Result : null;
    }

    protected virtual SessionContext EstablishSession(ConnectionInfo conn, LoginResult login)
        => SessionFactory.Establish(conn, login, services);

    protected virtual (LicenceAcquireOutcome Outcome, Lic? Licence) AcquireSession(SessionContext session)
        => LicenceVerifier.TryAcquireSession(
            session.MLRest, session.LicenceLoader, services.IsAdmin,
            services.PopupDisplayer, services.WindowNavigator);
}
