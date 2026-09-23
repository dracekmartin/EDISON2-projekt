using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>Poll odhlášení: bez řádku <c>ukoncit</c> LogoutWatcher signál nikdy neuvidí a nic nenahlásí.</summary>
[Trait("Category", "Integration")]
public class LogoutWatcherTests
{
    [DevServerFact]
    public void Poll_FreshSession_ParserMatchesUkoncitRow()
    {
        // Čerstvá instance má náhodný GUID; příznak se porovnává, protože signál „všem" může na serveru zrovna platit.
        var rest = DevServer.Connect();
        var raw = rest.With(cacheLocks: 2).QueryOrThrow("ALVA.SYSTEM.Main", "maSeUkoncit", new object[] { });

        var rows = WireRows.ByKey(raw, rest.Delimiter);
        Assert.True(rows.TryGetValue("ukoncit", out var ukoncit), "maSeUkoncit nevrátil řádek 'ukoncit' rozdělitelný oddělovačem MLRest.");
        Assert.Contains(ukoncit[1], new[] { "0", "1" });

        var signal = LogoutWatcher.Poll(rest);

        Assert.Equal(ukoncit[1] == "1", signal.ShouldClose);
    }
}
