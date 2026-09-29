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

    private sealed class LocalClientsCapture : Updater
    {
        public IReadOnlyList<string>? Seen { get; private set; }

        protected override bool IsLocalClient(IReadOnlyList<string> localClients)
        {
            Seen = localClients;
            return base.IsLocalClient(localClients);
        }
    }

    [DevServerFact]
    public void UpdaterLoadSettings_RealServer_ParserReadsLocalClientRows()
    {
        // Přejmenovaný řádek nebo posunuté pole by z každého lokálního klienta tiše udělal serverovou instalaci.
        var rest = DevServer.Connect();
        var raw = rest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { });
        var localPaths = raw.Skip(1)
            .Select(row => (row?.ToString() ?? "").Split(rest.Delimiter))
            .Where(parts => parts[0] == "localPath")
            .ToList();
        Assert.True(localPaths.Count > 0,
            "LoadSettings nevrátil žádný řádek 'localPath' - DEV má mít v seznamu lokálních klientů aspoň jeden počítač.");
        Assert.All(localPaths, parts => Assert.Equal(3, parts.Length));

        var updater = new LocalClientsCapture();
        updater.LoadSettings(rest);

        Assert.Equal(localPaths.Select(parts => parts[1]), updater.Seen);
    }
}
