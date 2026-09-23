using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>Kontroly před loginem: parsery chybějící řádek tiše nahradí defaultem, proto se hlídá i surový výstup.</summary>
[Trait("Category", "Integration")]
public class PreLoginTests
{
    [DevServerFact]
    public void LoginGateQuery_RealServer_ParserMatchesLoginRow()
    {
        var rest = DevServer.Connect();
        var ip = LoginGate.StationIp();
        var del = rest.Delimiter;
        var raw = rest.QueryOrThrow("ALVA.SYSTEM.Main", "LoginAllowed",
            new object[] { $"ip{del}{ip}", $"loginName{del}{DevServer.LoginName}" });

        var rows = WireRows.ByKey(raw, del);
        Assert.True(rows.TryGetValue("login", out var login), "LoginAllowed nevrátil řádek 'login', LoginGate by tiše povolil přihlášení.");
        Assert.Equal(3, login.Length);
        Assert.Contains(login[1], new[] { "0", "1" });
        Assert.Contains(login[2], new[] { "0", "1" });

        var permission = LoginGate.Query(rest, ip, DevServer.LoginName);

        Assert.Equal(login[1] == "1", permission.Allowed);
        Assert.Equal(login[2] == "1", permission.CanAlter);
    }

    [DevServerFact]
    public void UpdaterLoadSettings_RealServer_ParserReadsServerRows()
    {
        var rest = DevServer.Connect();
        var rows = WireRows.ByKey(rest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { }), rest.Delimiter);

        // Do zpráv jen klíče, řádek admin nese heslo.
        foreach (var key in new[] { "update", "pending", "version", "edisonPath", "sourceServer", "sourcePort" })
            Assert.True(rows.ContainsKey(key), $"LoadSettings nevrátil řádek '{key}', Updater by tiše vzal default.");
        Assert.True(int.TryParse(rows["update"][1], out _), "Řádek 'update' nemá číselný příznak.");

        var settings = new Updater().LoadSettings(rest);

        Assert.Equal(rows["version"][1], settings.CacheVersion);
        Assert.Equal(rows["edisonPath"][1], settings.EdisonPath);
    }
}
