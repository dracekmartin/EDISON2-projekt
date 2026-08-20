using System;
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
}
