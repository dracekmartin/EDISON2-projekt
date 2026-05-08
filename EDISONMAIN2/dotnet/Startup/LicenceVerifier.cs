using System;
using System.Diagnostics;
using System.IO;
using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;

namespace EDISONMAIN2;

public enum LicenceAcquireOutcome
{
    /// <summary>VerifyLic vrátil 1 — server vytvořil ACTIVITY zámek pod aktuální IdFa.</summary>
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
        IPopupDisplayer popup)
    {
        Lic? licence;
        while (true)
        {
            licence = licenceLoader();
            int countOfLicenses = licence?.CountsLOGIN ?? 0;
            var output = mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", new object[] { countOfLicenses });
            int result = int.Parse(output[1]?.ToString() ?? "0");
            if (result == 1) return (LicenceAcquireOutcome.Accepted, licence);

            if (result == -1 && isAdmin)
            {
                if (!popup.Confirm(
                    "Byl překročen počet licencí. Otevřu okno aktivity uživatelů — můžete někoho odhlásit. Po zavření okna zkusím znovu. Pokračovat?"))
                    return (LicenceAcquireOutcome.Cancelled, licence);
                LaunchUsersFormAndWait(mlRest, "ALVA.USERS.formUSERSActivity");
                continue;
            }

            return (LicenceAcquireOutcome.Refused, licence);
        }
    }

    // Synchronní spawn legacy .exe přes EDISONMAIN2API.FormToRunGetter kontrakt (10 args).
    // Prázdný shellGuid (10. arg): retry-loop volá formu **před** úspěšným VerifyLic, takže shell
    // nemá ACTIVITY zámek pod aktuální IdFa. FormToRunGetter v takovém případě neaktivuje
    // CacheLocks=1 (jinak by každý server request padal na LockExist). Forma získá vlastní GUID.
    private static void LaunchUsersFormAndWait(IMLRest mlRest, string formName)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = ResolveExecutablePath("USERS.exe"),
            Arguments = $"EDISONMAIN2 \"{mlRest.Server}\" \"{mlRest.Port}\" \"{mlRest.DataNamespace}\" \"{mlRest.Idfa}\" \"{mlRest.LoginName}\" \"{mlRest.FullName}\" \"{mlRest.Mlp}\" \"{formName}\" \"\"",
        };
        using var prc = Process.Start(psi);
        prc?.WaitForExit();
    }

    private static string ResolveExecutablePath(string relativePath)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(localPath)) return localPath;
        return Path.Combine(@"C:\Edison", relativePath);
    }
}
