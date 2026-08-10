using EDControls;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testy parsování login response z ALVA.SYS.Login.Login (delimited object[]). Bezpečnostně
/// relevantní vstup — poziční pole, mapování chybových kódů, fallbacky při krátké odpovědi.
/// </summary>
public class UserAuthenticatorTests
{
    private const char D = '~';

    private static IMLRest MockWithResponse(string data)
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns(D);
        ml.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>())
            .Returns(new object[] { "", data });
        return ml;
    }

    [Fact]
    public void Login_ParsesAllFields()
    {
        var ml = MockWithResponse(string.Join(D,
            "201", "Jan Novák", "1", "C:\\dir", "0", "1", "x", "jan@x.cz", "a", "b", "jan.novak"));

        var r = UserAuthenticator.Login(ml, "typed.name", "pwd");

        Assert.Equal(201, r.Mlp);
        Assert.Equal("jan.novak", r.UserName);       // parts[10]
        Assert.Equal("Jan Novák", r.FullName);        // parts[1]
        Assert.True(r.IsAdmin);                        // parts[2] == "1"
        Assert.Equal("C:\\dir", r.Directory);          // parts[3]
        Assert.False(r.ChangePasswordNeeded);          // parts[4] != "1"
        Assert.True(r.MayUpdate);                       // parts[5] == "1"
        Assert.Equal("jan@x.cz", r.Email);             // parts[7]
    }

    // Legacy USERS ukládá boolean flagy jako VB True = -1; server je posílá surově ("-1" i "1").
    [Theory]
    [InlineData("-1", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData("x", false)]
    public void Login_VbStyleBooleanFlags_NonZeroIsTrue(string flag, bool expected)
    {
        var ml = MockWithResponse(string.Join(D,
            "201", "Jan Novák", flag, "", flag, flag, "", "", "", "", "jan"));

        var r = UserAuthenticator.Login(ml, "jan", "pwd");

        Assert.Equal(expected, r.IsAdmin);               // parts[2]
        Assert.Equal(expected, r.ChangePasswordNeeded);  // parts[4]
        Assert.Equal(expected, r.MayUpdate);             // parts[5]
    }

    [Fact]
    public void Login_ShortResponse_FallsBackToTypedUsername_AndDefaults()
    {
        var ml = MockWithResponse("201");   // jen mlp, žádná další pole

        var r = UserAuthenticator.Login(ml, "typed.name", "pwd");

        Assert.Equal(201, r.Mlp);
        Assert.Equal("typed.name", r.UserName);   // fallback na zadané jméno
        Assert.Equal("", r.FullName);
        Assert.False(r.IsAdmin);
        Assert.Equal("", r.Email);
    }

    [Theory]
    [InlineData(-1, "Neplatné jméno nebo heslo")]
    [InlineData(-101, "neexistuje")]
    [InlineData(-107, "heslo")]
    [InlineData(-105, "deaktivován")]
    [InlineData(-999, "kód -999")]   // neznámý kód → generická hláška s kódem
    public void Login_NonPositiveMlp_ThrowsMappedError(int code, string fragment)
    {
        var ml = MockWithResponse(code.ToString());

        var ex = Assert.Throws<LoginException>(() => UserAuthenticator.Login(ml, "u", "p"));
        Assert.Contains(fragment, ex.Message);
    }

    // HD 13132/1: -1 je sběrný kód (i selhání LDAP bindu), neexistenci hlásí jedině -101.
    // Tvrzení „uživatel neexistuje" u -1 poslalo hledání příčiny úplně jiným směrem.
    [Fact]
    public void Login_GenericFailure_DoesNotClaimUserIsMissing()
    {
        var ex = Assert.Throws<LoginException>(
            () => UserAuthenticator.Login(MockWithResponse("-1"), "u", "p"));

        Assert.DoesNotContain("neexistuje", ex.Message);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public void Login_NonNumericStatus_ThrowsLoginException(string response)
    {
        // Poškozená / prázdná odpověď serveru → LoginException, ne syrová FormatException.
        var ml = MockWithResponse(response);

        var ex = Assert.Throws<LoginException>(() => UserAuthenticator.Login(ml, "u", "p"));
        Assert.Contains("Neplatná odpověď serveru", ex.Message);
    }

    [Fact]
    public void Login_EmptyUsername_ThrowsBeforeServerCall()
    {
        var ml = Substitute.For<IMLRest>();

        Assert.Throws<LoginException>(() => UserAuthenticator.Login(ml, "", "p"));
        ml.DidNotReceive().QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public void Login_SendsUsernameAndPasswordToServer()
    {
        var ml = MockWithResponse(string.Join(D, "201", "Jan", "0", "", "0", "0", "", "", "", "", "jan"));

        UserAuthenticator.Login(ml, "jan", "secret");

        ml.Received().QueryOrThrow("ALVA.SYS.Login", "Login",
            Arg.Is<object[]>(a => a.Length == 2 && (string)a[0] == "jan" && (string)a[1] == "secret"));
    }

    private static IMLRest MockChangePassword(string statusResponse)
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns(D);
        ml.QueryOrThrow("ALVA.SYS.Login", "ChangePassword", Arg.Any<object[]>())
            .Returns(new object[] { "", statusResponse });
        return ml;
    }

    [Fact]
    public void ChangePassword_Status1_Succeeds()
    {
        var r = UserAuthenticator.ChangePassword(MockChangePassword("1"), "jan", "old", "new");

        Assert.True(r.Success);
    }

    [Theory]
    [InlineData("2", "Původní heslo")]                 // fallback text (server prázdný)
    [InlineData("3", "nesplňuje")]
    [InlineData("4", "neexistuje")]
    [InlineData("99", "selhala")]
    public void ChangePassword_NonOkStatus_ReturnsMessage(string status, string fragment)
    {
        var r = UserAuthenticator.ChangePassword(MockChangePassword(status), "jan", "old", "new");

        Assert.False(r.Success);
        Assert.Contains(fragment, r.Message);
    }

    [Fact]
    public void ChangePassword_ServerText_PreferredOverFallback()
    {
        var r = UserAuthenticator.ChangePassword(MockChangePassword($"2{D}Heslo vyprselo pred 3 dny"), "jan", "old", "new");

        Assert.False(r.Success);
        Assert.Equal("Heslo vyprselo pred 3 dny", r.Message);
    }

    [Fact]
    public void ChangePassword_Status4_IgnoresServerText_FixedMessageWins()
    {
        // Asymetrie vůči kódům 2/3: u 4 se serverText ignoruje, hláška je pevná.
        var r = UserAuthenticator.ChangePassword(
            MockChangePassword($"4{D}vlastní text serveru"), "jan", "old", "new");

        Assert.False(r.Success);
        Assert.Equal("Změna hesla není možná (uživatel neexistuje).", r.Message);
    }

    [Fact]
    public void ChangePassword_NonNumericStatus_FallsBackTo99()
    {
        // Nečíselný status → 99 (generická chyba), stejná větev jako explicitní 99.
        var r = UserAuthenticator.ChangePassword(MockChangePassword("xyz"), "jan", "old", "new");

        Assert.False(r.Success);
        Assert.Contains("selhala", r.Message);
    }

    [Fact]
    public void ChangePassword_SendsUsernameOldNewToServer()
    {
        var ml = MockChangePassword("1");

        UserAuthenticator.ChangePassword(ml, "jan", "staré", "nové");

        ml.Received().QueryOrThrow("ALVA.SYS.Login", "ChangePassword",
            Arg.Is<object[]>(a => a.Length == 3
                && (string)a[0] == "jan" && (string)a[1] == "staré" && (string)a[2] == "nové"));
    }
}
