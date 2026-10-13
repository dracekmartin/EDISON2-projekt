using System;
using System.Collections.Generic;
using System.Linq;
using ALWA.SYS;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Spojení se serverem před loginem, bez uživatele a firmy. Start z něj čte seznam firem serveru,
/// setup wizard jím testuje zadaný server a načítá seznam databází.
/// </summary>
public static class PreLoginConnection
{
    internal static IMLRest Open(string server, int port) => new MLRestAdapter(new MLRest(server, port, "", ""));

    /// <summary>Připojení projde, když server bez chyby vrátí seznam firem.</summary>
    internal static bool Probe(string server, int port)
    {
        try
        {
            LegacyApi.CompanyList(Open(server, port), checkAccess: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static IReadOnlyList<string> LoadDatabases(string server, int port) => LoadDatabases(Open(server, port));

    public static IReadOnlyList<string> LoadDatabases(IMLRest rest) =>
        // Legacy delimited API: object[] výstup, řádky "database~<název>" od indexu 1.
        ParseDatabaseRows(rest.QueryOrThrow("ALVA.EDISON.Main", "LoadDatabaseList", new object[] { }), rest.Delimiter);

    internal static List<string> ParseDatabaseRows(object[] output, char delimiter) =>
        LegacyOutput.Rows(output, delimiter).Where(r => r.Length > 1 && r[0] == "database").Select(r => r[1]).ToList();
}
