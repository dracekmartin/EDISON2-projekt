using ALWA.SYS;
using EDControls;
using EDTesting;
using NSubstitute;

namespace EDISON2.IntegrationTests;

/// <summary>Testovací účet DEV (<see cref="TestAccount"/>): přepis proměnnými a kontrakt <c>ALVA.EDTesting.ITAccount.Get</c>.</summary>
[Trait("Category", "Integration")]
public class TestAccountTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static Func<string, string?> Env(Dictionary<string, string> values) => name => values.GetValueOrDefault(name);

    private static TestAccount FromVariables() =>
        TestAccount.TryGet(Env(new() { [TestAccount.LoginVariable] = "it.ucet", [TestAccount.PasswordVariable] = "tajne-heslo" }),
            () => null, Timeout)!;

    [Fact]
    public void TryGet_BothVariables_OverrideServer()
    {
        var env = Env(new() { [TestAccount.LoginVariable] = "it.ucet", [TestAccount.PasswordVariable] = "tajne-heslo" });

        var account = TestAccount.TryGet(env, () => throw new InvalidOperationException("Server se nemá volat."), Timeout);

        Assert.NotNull(account);
        Assert.Equal("it.ucet", account.Login);
        Assert.True(account.Password == "tajne-heslo", "Heslo z proměnné se nepřevzalo.");
    }

    [Fact]
    public void TryGet_OnlyLoginVariable_FallsBackToServer()
    {
        var env = Env(new() { [TestAccount.LoginVariable] = "it.ucet" });
        var serverAsked = false;

        var account = TestAccount.TryGet(env, () => { serverAsked = true; return null; }, Timeout);

        Assert.True(serverAsked);
        Assert.Null(account);
    }

    // Server přijme spojení a neodpoví: MLRest by čekal 8 h a discovery testů s ním.
    [Fact]
    public void TryGet_ServerDoesNotAnswer_ThrowsTimeout()
    {
        using var release = new ManualResetEventSlim();
        var rest = Substitute.For<IMLRest>();
        rest.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CVar>()).Returns(_ => { release.Wait(); return new CVar(); });
        try
        {
            Assert.Throws<TimeoutException>(() => TestAccount.TryGet(_ => null, () => rest, TimeSpan.FromMilliseconds(200)));
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public void ToString_DoesNotContainPassword() =>
        Assert.DoesNotContain("tajne-heslo", FromVariables().ToString());

    [Fact]
    public void SkipReason_ServerUnreachable_SkipsUnlessRequired()
    {
        Func<TestAccount?> account = () => throw new InvalidOperationException("Bez serveru se účet nečte.");

        Assert.Equal(DevServer.Unreachable, TestAccountFactAttribute.SkipReason(false, false, account));
        Assert.Null(TestAccountFactAttribute.SkipReason(false, true, account));
    }

    [Fact]
    public void SkipReason_MissingAccount_Skips() =>
        Assert.Equal(TestAccountFactAttribute.MissingAccount, TestAccountFactAttribute.SkipReason(true, false, () => null));

    // Chyba Caché (třída nenasazená) se nesmí schovat do Skipped.
    [Fact]
    public void SkipReason_AccountThrows_RunsTest() =>
        Assert.Null(TestAccountFactAttribute.SkipReason(true, false, () => throw new InvalidOperationException("Caché")));

    [Fact]
    public void SkipReason_AccountPresent_RunsTest() =>
        Assert.Null(TestAccountFactAttribute.SkipReason(true, false, FromVariables));

    // Na stavu globálu nezávisí: prázdný účet musí dát null, vyplněný účet (jméno i heslo) instanci.
    [DevServerFact]
    public void Get_DeployedMethod_ReturnsAccountKeysFromMline()
    {
        var rest = DevServer.Connect();

        CVar raw = rest.QueryOrThrow("ALVA.EDTesting.ITAccount", "Get", new CVar());

        var keys = new HashSet<string>();
        foreach (KeyValuePair<string, CVar> kvp in raw) keys.Add(kvp.Key);
        Assert.Superset(new HashSet<string> { "login", "password", "namespace" }, keys);
        // CLAUDE.md říká, kam global zapsat - jiný namespace by návod zneplatnil.
        Assert.Equal("MLINE", raw["namespace"]?.Value?.ToString());

        bool complete = !string.IsNullOrEmpty(raw["login"]?.Value?.ToString())
                        && !string.IsNullOrEmpty(raw["password"]?.Value?.ToString());
        Assert.True(complete == (TestAccount.FromServer(rest) is not null),
            "FromServer neodpovídá výstupu Get (úplný účet = instance, jinak null).");
    }
}
