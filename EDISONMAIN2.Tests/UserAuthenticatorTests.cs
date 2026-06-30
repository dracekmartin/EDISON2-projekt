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
    [InlineData(-1, "neexistuje")]
    [InlineData(-107, "heslo")]
    [InlineData(-105, "deaktivován")]
    [InlineData(-999, "kód -999")]   // neznámý kód → generická hláška s kódem
    public void Login_NonPositiveMlp_ThrowsMappedError(int code, string fragment)
    {
        var ml = MockWithResponse(code.ToString());

        var ex = Assert.Throws<LoginException>(() => UserAuthenticator.Login(ml, "u", "p"));
        Assert.Contains(fragment, ex.Message);
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
}
