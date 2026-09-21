using System;
using System.Net;
using EDControls;

namespace EDISONMAIN2;

/// <summary>Stav zákazu přihlašování: <see cref="CanAlter"/> = zákaz zadala tahle stanice a tento
/// Windows uživatel, takže ho smí zase zrušit (parita s EDLOADER <c>CheckLogin</c>).</summary>
public record LoginPermission(bool Allowed, bool CanAlter);

/// <summary>
/// Brána „smí se vůbec přihlašovat" (<c>^EDISON("System","Login","Deny")</c>, zapisuje legacy
/// Nastavení). Legacy ji dělá EDLOADER před startem EDISONMAIN; zástupce přímo na EDISONMAIN2.exe
/// ho obchází, proto si ji shell dělá sám na pre-login MLRest (bez firmy).
/// </summary>
public static class LoginGate
{
    private const string CacheClass = "ALVA.SYSTEM.Main";

    // object[] protokol - legacy endpointy čtou ^||ML("IN",N) jako "klíč{del}hodnota".
    public static LoginPermission Query(IMLRest mlRest, string ip, string loginName)
    {
        char del = mlRest.Delimiter;
        var output = mlRest.QueryOrThrow(CacheClass, "LoginAllowed",
            new object[] { $"ip{del}{ip}", $"loginName{del}{loginName}" });
        return Parse(output, del);
    }

    /// <summary>Řádek <c>login{del}{isAllowed}{del}{allowAlter}</c>; bez řádku = povoleno (server nic nezakázal).</summary>
    public static LoginPermission Parse(object[] output, char delimiter)
    {
        for (int i = 1; i < output.Length; i++)
        {
            var parts = (output[i]?.ToString() ?? "").Split(delimiter);
            if (parts.Length < 3 || parts[0] != "login") continue;
            return new LoginPermission(parts[1] == "1", parts[2] == "1");
        }
        return new LoginPermission(true, false);
    }

    /// <summary>Zruší zákaz (legacy <c>ChangeLoginState</c>); server si zapíše, kdo to udělal.</summary>
    public static void Allow(IMLRest mlRest, string ip, string loginName)
    {
        char del = mlRest.Delimiter;
        mlRest.QueryOrThrow(CacheClass, "ChangeLoginState",
            new object[] { $"ip{del}{ip}", $"loginName{del}{loginName}", $"loginAllowed{del}1" });
    }

    /// <summary>První adresa stanice jako legacy <c>clsStartup.GetIP</c> - server podle ní páruje,
    /// kdo zákaz zadal. Bez adresy jméno stroje, prázdný parametr by server odmítl.</summary>
    public static string StationIp()
    {
        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            if (addresses.Length > 0) return addresses[0].ToString();
        }
        catch
        {
            // DNS nedostupné - fallback níž.
        }
        return Environment.MachineName;
    }
}
