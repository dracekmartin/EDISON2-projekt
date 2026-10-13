using EDControls;
using EDISONMAIN2;
using EDISONMAIN2.Windows.UserProfileWindow;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Legacy endpointy bez vedlejších efektů: <c>LoadUser</c> (profil uživatele), <c>LoadDatabaseList</c> (průvodce
/// nastavením) a <c>ALVA.LICENCE.Activation.GetList</c> (detekce změny licence). Zápisové protějšky se nevolají.
/// </summary>
[Trait("Category", "Integration")]
public class UserAndLicenceEndpointTests
{
    // Do zpráv jen tagy a počty: LoadUser vrací adresář a e-mail, LoadSettings uvnitř Load jméno a heslo admina.
    // Profil testovacího účtu: uživatel test má výchozí hodnoty, se kterými by prošel i parser, který řádek nenajde.
    [TestAccountFact]
    public void LoadUser_RawRows_MatchParserLayout_ProfileServiceReadsPersonDir()
    {
        var rest = DevServer.Connect(loginName: DevServer.Account!.Login);

        var rows = LegacyOutput.Rows(rest.QueryOrThrow("ALVA.EDISON.Main", "LoadUser", new object[] { }), rest.Delimiter).ToList();

        foreach (var row in rows)
        {
            switch (row[0])
            {
                case "personDir":
                    Assert.True(row.Length == 3 && int.TryParse(row[1], out _), "Řádek personDir nemá 3 pole s číselným příznakem.");
                    break;
                case "email":
                    Assert.True(row.Length == 2, "Řádek email nemá 2 pole.");
                    break;
                case "form":
                    Assert.True(row.Length == 2 && int.TryParse(row[1], out _), "Řádek form nemá 2 pole s číselným id okna.");
                    break;
                default:
                    // Bez obsahu: po posunu polí by na místě tagu stál e-mail nebo cesta.
                    Assert.Fail($"Neznámý řádek LoadUser ({row.Length} polí), parser by ho zahodil.");
                    break;
            }
        }
        var personDir = rows.Where(row => row[0] == "personDir").ToList();
        Assert.True(personDir.Count == 1, $"LoadUser vrátil {personDir.Count} řádků personDir, čekán 1.");
        Assert.True(personDir[0][1] != "0" && personDir[0][2].Length > 0,
            "Testovací účet nemá osobní adresář se zaškrtnutým použitím (EDISON2.IntegrationTests/CLAUDE.md).");

        var profile = UserProfileService.Load(rest);

        Assert.True(profile.PersonDir == personDir[0][2] && profile.UsePersonDir == (int.Parse(personDir[0][1]) != 0),
            "UserProfileService nečte adresář a příznak z polí 2 a 1 řádku personDir.");
    }

    // Průvodce volá LoadDatabaseList před výběrem firmy; metoda ze sys čte jen oddělovač, identita testu výsledek nemění.
    [DevServerFact]
    public void LoadDatabaseList_ProductionParser_ListsEveryNamespace()
    {
        var rest = DevServer.Connect();

        var output = rest.QueryOrThrow("ALVA.EDISON.Main", "LoadDatabaseList", new object[] { });

        var rows = LegacyOutput.Rows(output, rest.Delimiter).ToList();
        Assert.All(rows, row => Assert.True(row.Length == 2 && row[0] == "database", $"Řádek '{row[0]}' není database{{D}}název."));
        var databases = PreLoginConnection.LoadDatabases(rest);
        Assert.Equal(rows.Select(row => row[1]), databases);
        Assert.Contains("MLINE", databases);
        Assert.Contains(DevServer.ReadDataNamespace, databases);
    }

    // Výpis čtou dva konzumenti: EDLicence Activation.GetActiveLic podle pozic (řádek 1 = klíč aktivní licence, pole
    // datum, šifrovaná licence, koš) a LicenceService.SyncFromMline, který pozná novou licenci podle rozdílu dvou výpisů.
    // Obsah licencí do zpráv nedávat.
    [DevServerFact]
    public void ActivationGetList_RowsMatchLayout_RepeatedCallReturnsSameList()
    {
        var rest = DevServer.Connect();

        var first = rest.QueryOrThrow("ALVA.LICENCE.Activation", "GetList", new object[] { });
        var second = rest.QueryOrThrow("ALVA.LICENCE.Activation", "GetList", new object[] { });

        var values = LegacyOutput.Values(first).ToList();
        Assert.True(values.Count >= 2, "GetList nevrátil klíč aktivní licence a aspoň jednu licenci.");
        var keys = new List<string>();
        for (int i = 1; i < values.Count; i++)
        {
            var fields = values[i].Split(rest.Delimiter);
            Assert.True(fields.Length == 3, $"Řádek {i + 1} nemá 3 pole.");
            Assert.True(LicenceKey.IsMatch(fields[0]), $"Řádek {i + 1}: pole 1 není datum vystavení licence.");
            Assert.True(fields[1].Length > 0 && !LicenceKey.IsMatch(fields[1]), $"Řádek {i + 1}: pole 2 není šifrovaná licence.");
            Assert.True(fields[2] is "0" or "1", $"Řádek {i + 1}: pole 3 není příznak koše 0/1.");
            keys.Add(fields[0]);
        }
        Assert.True(values[0].Length == 0 || keys.Contains(values[0]), "Řádek 1 není klíč (datum) jedné z licencí.");
        Assert.True(string.Join("\n", first) == string.Join("\n", second), "Dvě volání GetList po sobě vrátila jiný výpis.");
    }

    private static readonly System.Text.RegularExpressions.Regex LicenceKey = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");
}
