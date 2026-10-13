using EDControls;
using EDISONMAIN2.Windows.LoginWindow;

namespace EDISON2.IntegrationTests;

/// <summary>
/// <c>ALVA.SYS.Login.Login</c> chybovou cestou (neexistující jméno) a úspěšnou cestou jen s testovacím účtem:
/// s reálným účtem se volat nesmí, Login nuluje uživatelské flagy 17 a 20 (CLAUDE.md).
/// Úspěšná odpověď nese heslo (pole 9, <c>Login.cls:69</c>): hodnoty polí do zpráv nepatří.
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

        Assert.True(raw.Length > 1, "Login nevrátil řádek s výsledkem.");
        var data = raw[1]?.ToString() ?? "";
        Assert.False(data.Contains(rest.Delimiter), "Neúspěšný Login vrátil víc polí než samotný kód.");
        Assert.True(int.TryParse(data, out var code) && code < 0, "Neúspěšný Login nevrátil záporný kód.");

        var ex = Assert.Throws<LoginException>(() => UserAuthenticator.Login(rest, username, password));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    // UserAuthenticator čte pozice 0, 1, 2, 3, 4, 5, 7 a 10. Jen Assert.True s pevnou zprávou:
    // Assert.Equal by po posunu polí vypsal heslo z pole 9.
    [TestAccountFact]
    public void Login_TestAccount_ReturnsElevenFields_ParserReadsProductionPositions()
    {
        var account = DevServer.Account!;
        var rest = DevServer.Connect();

        var raw = rest.QueryOrThrow("ALVA.SYS.Login", "Login", new object[] { account.Login, account.Password });

        Assert.True(raw.Length > 1, "Login nevrátil řádek s výsledkem.");
        var parts = (raw[1]?.ToString() ?? "").Split(rest.Delimiter);
        // Neúspěšná odpověď nese jen záporný kód, ten heslo neobsahuje.
        if (parts.Length == 1) Assert.Fail($"Login testovacího účtu selhal s kódem {parts[0]} (heslo v globálu, zakázaný účet?).");
        Assert.True(parts.Length == 11, $"Úspěšný Login vrátil {parts.Length} polí, čekáno 11.");
        Assert.True(int.TryParse(parts[0], out var mlp) && mlp > 0, "Pole 0 není kladné MLP.");
        foreach (var flag in new[] { 2, 4, 5, 6 })
            Assert.True(int.TryParse(parts[flag], out _), $"Pole {flag} není číselné.");
        Assert.True(parts[8].EndsWith(account.Login.ToUpperInvariant(), StringComparison.Ordinal), "Pole 8 není normalizované jméno účtu.");
        // Heslo na své pozici: jinak by se dostalo do celého jména, adresáře nebo e-mailu, které shell posílá dál.
        Assert.True(parts[9] == account.Password, "Pole 9 není heslo.");
        Assert.True(string.Equals(parts[10], account.Login, StringComparison.OrdinalIgnoreCase), "Pole 10 není jméno účtu.");

        // GetUserInfo čte tytéž vlastnosti uživatele bez hesla: pole se ověří podle významu, ne jen podle tvaru.
        var info = UserInfo(rest, account.Login);
        Assert.True(parts[1] == info["FullName"], "Pole 1 není celé jméno.");
        Assert.True(parts[2] == info["SuperUser"], "Pole 2 není příznak admina.");
        Assert.True(parts[3] == info["Dir"], "Pole 3 není adresář.");
        Assert.True(parts[5] == info["UpdateEdison"], "Pole 5 není právo aktualizovat EDISON.");
        Assert.True(parts[7] == info["Email"], "Pole 7 není e-mail.");

        var result = UserAuthenticator.Login(rest, account.Login, account.Password);

        Assert.True(result.Mlp == mlp, "Parser nevrátil MLP z pole 0.");
        Assert.True(string.Equals(result.UserName, account.Login, StringComparison.OrdinalIgnoreCase), "Parser nevrátil jméno z pole 10.");
        Assert.True(result.FullName == info["FullName"] && result.Directory == info["Dir"] && result.Email == info["Email"],
            "Parser nečte celé jméno, adresář a e-mail z jejich polí.");
        Assert.True(result.IsAdmin == (info["SuperUser"] != "0") && result.MayUpdate == (info["UpdateEdison"] != "0"),
            "Parser nečte příznak admina a právo aktualizace z jejich polí.");
    }

    // Řádky "vlastnost{D}hodnota". Hodnoty do zpráv nedávat, GetUserInfo vrací i telefon a e-mail.
    private static Dictionary<string, string> UserInfo(IMLRest rest, string login) =>
        LegacyOutput.Rows(rest.QueryOrThrow("ALVA.SYS.Login", "GetUserInfo", new object[] { login }), rest.Delimiter)
            .Where(row => row.Length >= 2)
            .ToDictionary(row => row[0], row => row[1]);
}
