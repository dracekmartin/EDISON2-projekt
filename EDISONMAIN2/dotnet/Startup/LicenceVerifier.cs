using System;
using System.Diagnostics;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

public enum LicenceAcquireOutcome
{
    /// <summary>VerifyLic vrátil 1 - server vytvořil ACTIVITY zámek pod aktuální IdFa.</summary>
    Accepted,
    /// <summary>Admin zrušil retry-loop přes Confirm dialog.</summary>
    Cancelled,
    /// <summary>Non-admin nad limit, nebo server vrátil neznámou chybu. Caller zobrazí kontextovou zprávu.</summary>
    Refused,
}

/// <summary>
/// Volá ALVA.SYS.Login.VerifyLic v retry-loopu. Na out-of-license nabídne adminovi
/// otevřít okno aktivity uživatelů (legacy USERS.exe formUSERSActivity) a po zavření
/// retry. Použito jak při loginu, tak při přepnutí firmy v hlavním okně.
/// </summary>
public static class LicenceVerifier
{
    /// <summary>
    /// Získání session nad sdílenými službami (start, přihlášení jiného uživatele, přepnutí firmy):
    /// loader a závislosti dialogů bere z <paramref name="services"/> a licenci zapíše do
    /// <see cref="EDServices.ActiveLicence"/> hned při <see cref="LicenceAcquireOutcome.Accepted"/>,
    /// aby od té chvíle odpovídala <c>MLRest.Idfa</c> (čtou ji launch argumenty agend).
    /// </summary>
    public static LicenceAcquireOutcome TryAcquireSession(IMLRest mlRest, EDServices services)
    {
        var loader = services.LicenceLoader
            ?? throw new InvalidOperationException("LicenceLoader není nastavený - session nezaložila SessionFactory.");
        var (outcome, licence) = TryAcquireSession(mlRest, loader, services.IsAdmin,
                                                   services.PopupDisplayer, services.WindowNavigator);
        if (outcome == LicenceAcquireOutcome.Accepted) services.ActiveLicence = licence;
        return outcome;
    }

    /// <summary>Znovu načte licenci beze změny session (po stažení licence z M-line).</summary>
    public static void ReloadActiveLicence(EDServices services) =>
        services.ActiveLicence = services.LicenceLoader?.Invoke();

    internal static (LicenceAcquireOutcome Outcome, Lic? Licence) TryAcquireSession(
        IMLRest mlRest,
        Func<Lic?> licenceLoader,
        bool isAdmin,
        IPopupDisplayer popup,
        IWindowNavigator windowNavigator)
    {
        // Jednou za tok, ne per retry: loader ukazuje hlášku licence (prošlá, neplatný klíč) a okno
        // aktivity licenci nezmění - jen odhlašuje uživatele.
        var licence = licenceLoader();
        // Parita s EDUsers.VerifyLic: podpora licenci nezabírá a jede bez zámku s CacheLocks=0 - server
        // pak nekontroluje LockExist ani verzi, agendy nedostanou shell GUID (WindowNavigator).
        if (IsSupportLogin(mlRest)) return (LicenceAcquireOutcome.Accepted, licence);

        int countOfLicenses = licence?.CountsLOGIN ?? 0;
        while (true)
        {
            // object[]: VerifyLic je legacy endpoint s numerickými subscripty (IN(1), OUT(1)).
            var output = mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", new object[] { countOfLicenses });
            int result = int.Parse(output[1]?.ToString() ?? "0");
            if (result == 1)
            {
                // Parita s legacy shellem (EDUsers.VerifyLic): ACTIVITY zámek existuje → od teď
                // každý request session obnovuje idle timer (UpdateHistory) a detekuje zaniklý
                // zámek / verzi (EdisonActivityFailed / EdisonVersionFailed → shutdown).
                mlRest.CacheLocks = 1;
                return (LicenceAcquireOutcome.Accepted, licence);
            }

            if (result == -1 && isAdmin)
            {
                // Bez hosta modulů okno aktivity nenaběhne a nabídka by se točila dokola: důvod, pak odmítnutí jako bez admina.
                if (windowNavigator is WindowNavigator nav && nav.MissingLegacyHostMessage() is { } hostMissing)
                {
                    popup.Info(hostMissing);
                    return (LicenceAcquireOutcome.Refused, licence);
                }
                if (!popup.ConfirmSystem(
                    "Byl překročen počet licencí. Otevřu okno aktivity uživatelů - můžete někoho odhlásit. Po zavření okna zkusím znovu. Pokračovat?"))
                    return (LicenceAcquireOutcome.Cancelled, licence);
                // shareShellSession=false: retry-loop běží před úspěšným VerifyLic, shell ještě
                // nemá ACTIVITY zámek pod aktuální IdFa. CacheLocks=1 by každý request modulu padal na LockExist.
                windowNavigator.ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
                continue;
            }

            return (LicenceAcquireOutcome.Refused, licence);
        }
    }

    /// <summary>
    /// Znovu vytvoří ACTIVITY zámek pro aktuální IdFa session a při úspěchu vrátí sdílenou
    /// instanci na <c>CacheLocks=1</c>. Nutné po GUID-wide úklidu business zámků
    /// (<c>LockClearExt</c>): serverový <c>Lock.LockClear(guid)</c> jde přes všechny objekty,
    /// takže smete i ACTIVITY zámek téhož GUIDu - tedy firmy, na kterou se právě přepnulo.
    /// <paramref name="licence"/> se předává hotová: <c>LicenceLoader</c> je sám request na
    /// sdílené instanci a pod chybějícím zámkem by shodil session ještě před VerifyLic.
    /// Best-effort: při selhání zůstane <c>CacheLocks=0</c> - session neobnovuje idle timer,
    /// ale nezhroutí se na LockExist a už dokončené přepnutí přežije.
    /// </summary>
    public static bool ReacquireLock(IMLRest mlRest, Lic? licence)
    {
        if (IsSupportLogin(mlRest)) return true;
        try
        {
            // Klon s Locks=0: request pod chybějícím zámkem by padl na LockExist ještě před
            // tělem metody. Server nepočítá do limitu vlastní IP (Activity.CountIdfa), takže
            // druhé VerifyLic pod týmž GUIDem projde, kdykoli prošlo první.
            var output = mlRest.With(cacheLocks: 0)
                               .QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
                                             new object[] { licence?.CountsLOGIN ?? 0 });
            if (int.Parse(output[1]?.ToString() ?? "0") != 1) return false;
            mlRest.CacheLocks = 1;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LicenceVerifier.ReacquireLock failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Záznam do sestavy USERS „Přehled přihlášení" (<c>ALVA.SYS.Login.LogLogin</c>, legacy
    /// <c>EDUsers.LogLogin</c>). Jen po startu a přihlášení jiného uživatele, i u podpory - legacy
    /// ho při přepnutí firmy nevolá. Best-effort: legacy chybu jen uložil, přihlášení na ní nestojí.
    /// </summary>
    public static void LogLogin(IMLRest mlRest)
    {
        try
        {
            // object[]: LogLogin je legacy endpoint s numerickým subscriptem IN(1)=název stanice.
            mlRest.QueryOrThrow("ALVA.SYS.Login", "LogLogin", new object[] { Environment.MachineName });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LicenceVerifier.LogLogin failed: {ex.Message}");
        }
    }

    /// <summary>Přihlášení technickým účtem spojení (legacy <c>LoginName = UserID</c>, typicky <c>_mline</c>).</summary>
    public static bool IsSupportLogin(IMLRest mlRest) =>
        !string.IsNullOrEmpty(mlRest.LoginName) && string.Equals(mlRest.LoginName, mlRest.UserID, StringComparison.Ordinal);
}
