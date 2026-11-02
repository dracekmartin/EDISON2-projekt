using ALWA.SYS;
using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>Poll odhlášení: bez řádku <c>ukoncit</c> LogoutWatcher signál nikdy neuvidí a nic nenahlásí.</summary>
[Trait("Category", "Integration")]
public class LogoutWatcherTests
{
    private const string Fixture = "ALVA.EDTesting.ITSession";

    [DevServerFact]
    public void Poll_FreshSession_ParserMatchesUkoncitRow()
    {
        // Čerstvá instance má náhodný GUID; příznak se porovnává, protože signál „všem" může na serveru zrovna platit.
        var rest = DevServer.Connect();
        AssertPollMatchesRawRow(rest);
    }

    // Bez požadavku vrací server většinou ukoncit=0, který parser vrátí i bez řádku. Test proto požádá o ukončení
    // jen své session a druhá session (jiný GUID) ho dostat nesmí.
    [DevServerFact]
    public void Poll_EndRequestedForSession_ParserReturnsCloseAndSeconds()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        // maSeUkoncit bere $h jako desetinné číslo a s malým počtem sekund by čerstvý požadavek na hranici sekundy považoval za starý.
        const int seconds = 100000;
        var input = new CVar();
        input["sec"] = new CVar(seconds.ToString());
        try
        {
            rest.QueryOrThrow(Fixture, "RequestEnd", input);
            var rows = WireRows.ByKey(rest.With(cacheLocks: 2).QueryOrThrow("ALVA.SYSTEM.Main", "maSeUkoncit", new object[] { }), rest.Delimiter);
            Assert.True(rows.TryGetValue("ukoncit", out var ukoncit) && ukoncit.Length == 3,
                "maSeUkoncit nevrátil řádek ukoncit se třemi poli.");
            Assert.Equal(new[] { "1", seconds.ToString() }, ukoncit[1..]);

            var signal = LogoutWatcher.Poll(rest);

            Assert.Equal(new LogoutSignal(true, seconds), signal);
            var other = AssertPollMatchesRawRow(DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp));
            Assert.NotEqual(new[] { "1", seconds.ToString() }, other[1..]);
        }
        finally
        {
            rest.QueryOrThrow(Fixture, "CleanEndRequest", new CVar());
        }
    }

    private static string[] AssertPollMatchesRawRow(EDControls.IMLRest rest)
    {
        var raw = rest.With(cacheLocks: 2).QueryOrThrow("ALVA.SYSTEM.Main", "maSeUkoncit", new object[] { });

        var rows = WireRows.ByKey(raw, rest.Delimiter);
        Assert.True(rows.TryGetValue("ukoncit", out var ukoncit), "maSeUkoncit nevrátil řádek 'ukoncit' rozdělitelný oddělovačem MLRest.");
        Assert.Contains(ukoncit[1], new[] { "0", "1" });

        var signal = LogoutWatcher.Poll(rest);

        Assert.Equal(ukoncit[1] == "1", signal.ShouldClose);
        return ukoncit;
    }
}
