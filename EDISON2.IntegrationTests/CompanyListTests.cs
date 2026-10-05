using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Seznam firem shellu po loginu jde přes filtr serveru (<c>checkAccess</c> + MLP v session).
/// MLP testovací identity na DEV nemá žádné omezení, takže samotné filtrování tu vidět není.
/// </summary>
[Trait("Category", "Integration")]
public class CompanyListTests
{
    [DevServerFact]
    public void LoadAllowed_RealServer_ParsesDatabaseRowsOfFilteredList()
    {
        var rest = DevServer.Connect();
        var del = rest.Delimiter;
        // Jen řádky databází - LoadSettings posílá i jméno a heslo admina.
        var databases = rest.QueryOrThrow("ALVA.EDISON.Main", "LoadSettings", new object[] { $"checkAccess{del}1" })
            .Skip(1)
            .Select(row => (row?.ToString() ?? "").Split(del))
            .Where(parts => parts.Length >= 6 && parts[0] == "system" && parts[1] == "database")
            .ToList();
        Assert.NotEmpty(databases);

        var allowed = CompanySwitcher.LoadAllowed(rest);

        Assert.Equal(databases.Select(p => p[2]).Order(), allowed.Select(c => c.Database).Order());
        Assert.Equal(databases.Select(p => p[3]).Order(), allowed.Select(c => c.Id).Order());
        Assert.Contains(allowed, c => c.Id == DevServer.ReadIdfa);
    }
}
