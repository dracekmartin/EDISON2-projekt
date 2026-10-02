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

    [DevServerFact]
    public void UpdaterLoadSettings_RealServer_ParserReadsForcedStampRow()
    {
        // Řádek posílá server, který má uzel razítka; ten po uložení nastavení updatu nebo první vlně zůstává
        // (SetSettings ho založí i prázdný a přenáší přes clear) a DEV ho má, takže chybějící řádek = přejmenovaný klíč.
        var rest = DevServer.Connect();
        var rows = WireRows.ByKey(rest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { }), rest.Delimiter);

        Assert.True(rows.TryGetValue("NucenyUpdatClentu", out var stamp),
            "LoadSettings nevrátil řádek 'NucenyUpdatClentu' (překlep je kontrakt), Updater by vynucenou vlnu tiše přestal spouštět.");
        Assert.True(stamp.Length > 1, "Řádek 'NucenyUpdatClentu' nemá pole hodnoty.");
        // Prázdné razítko je legitimní (vlna ještě neproběhla); vyplněné je $h, jinak jde o posunuté pole.
        if (stamp[1].Length > 0) Assert.Matches(@"^\d+,\d+$", stamp[1]);

        Assert.Equal(stamp[1], new Updater().LoadSettings(rest).ForcedStamp);
    }

    [DevServerFact]
    public void UpdaterLoadSettings_RealServer_LocalSourcePathRowMatchesSystemSettings()
    {
        // Řádek je podmíněný uloženým lokálním zdrojem stanic. ALVA.EDISON.Main.LoadSettings posílá
        // tentýž údaj za stejné podmínky (GetSettings uzel 11) pod vlastním klíčem, takže
        // přejmenování je vidět bez ohledu na to, jestli ho DEV zrovna má nastavený.
        var rest = DevServer.Connect();
        var del = rest.Delimiter;
        var rows = WireRows.ByKey(rest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { }), del);
        var reference = rest.QueryOrThrow("ALVA.EDISON.Main", "LoadSettings", new object[] { })
            .Skip(1)
            .Select(row => (row?.ToString() ?? "").Split(del))
            .FirstOrDefault(parts => parts.Length >= 3 && parts[0] == "system" && parts[1] == "updateLocalSourcePath");

        var hasRow = rows.ContainsKey("localSourcePath");
        Assert.True(hasRow || reference is null,
            "LoadSettings nevrátil řádek 'localSourcePath', ač server lokální zdroj stanic má - Updater by ho tiše nahradil edisonPath.");
        Assert.True(!hasRow || reference is not null,
            "ALVA.EDISON.Main.LoadSettings nevrátil řádek 'system/updateLocalSourcePath', test přišel o referenci.");

        Assert.Equal(reference?[2] ?? "", new Updater().LoadSettings(rest).LocalSourcePath);
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
