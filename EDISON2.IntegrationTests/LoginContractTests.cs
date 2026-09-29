using EDISONMAIN2.Windows.LoginWindow;

namespace EDISON2.IntegrationTests;

/// <summary>
/// <c>ALVA.SYS.Login.Login</c> jen chybovou cestou: s reálným účtem se volat nesmí (CLAUDE.md) a vyhrazený
/// IT účet není. Pozice polí úspěšné odpovědi hlídá jen unit test proti <c>Login.cls:69</c>.
/// Na DEV nezapíše data aplikace, nanejvýš záznam o neúspěšném přihlášení do auditu Caché.
/// </summary>
[Trait("Category", "Integration")]
public class LoginContractTests
{
    [DevServerFact]
    public void Login_UnknownUser_ReturnsBareNegativeCode_ParserThrowsLoginException()
    {
        var rest = DevServer.Connect();
        var username = $"edison2_it_neexistuje_{Guid.NewGuid():N}";
        const string password = "neplatne-heslo-it";

        var raw = rest.QueryOrThrow("ALVA.SYS.Login", "Login", new object[] { username, password });

        // Obsah do zpráv nedávat: úspěšná odpověď by nesla heslo (piece 10).
        Assert.True(raw.Length > 1, "Login nevrátil řádek s výsledkem.");
        var data = raw[1]?.ToString() ?? "";
        Assert.False(data.Contains(rest.Delimiter), "Neúspěšný Login vrátil víc polí než samotný kód.");
        Assert.True(int.TryParse(data, out var code) && code < 0, "Neúspěšný Login nevrátil záporný kód.");

        var ex = Assert.Throws<LoginException>(() => UserAuthenticator.Login(rest, username, password));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
