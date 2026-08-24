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
    public static (LicenceAcquireOutcome Outcome, Lic? Licence) TryAcquireSession(
        IMLRest mlRest,
        Func<Lic?> licenceLoader,
        bool isAdmin,
        IPopupDisplayer popup,
        IWindowNavigator windowNavigator)
    {
        Lic? licence;
        while (true)
        {
            licence = licenceLoader();
            int countOfLicenses = licence?.CountsLOGIN ?? 0;
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
                if (!popup.Confirm(
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
}
