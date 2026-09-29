namespace EDISON2.IntegrationTests;

/// <summary>
/// Záznam do sestavy USERS „Přehled přihlášení". Produkční <c>LicenceVerifier.LogLogin</c> chybu spolkne
/// (parita s legacy), proto test volá endpoint přímo se stejným vstupem. Zapisuje
/// <c>^ALVA.USERS("%","reports","login",...)</c> pod <c>TEST_EDISON2_IT</c> a úklid přes MLRest neexistuje.
/// </summary>
[Trait("Category", "Integration")]
public class LoginLogTests
{
    [DevServerFact]
    public void LogLogin_RealServer_AcceptsMachineName()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);

        rest.QueryOrThrow("ALVA.SYS.Login", "LogLogin", new object[] { Environment.MachineName });
    }
}
