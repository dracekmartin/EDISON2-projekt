using EDControls;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Brána zákazu přihlašování (parita s EDLOADER CheckLogin): wire k <c>ALVA.SYSTEM.Main.LoginAllowed</c>
/// a <c>ChangeLoginState</c> - pojmenované vstupy oddělené delimiterem, výstup řádek <c>login~allowed~alter</c>.
/// </summary>
public class LoginGateTests
{
    private static IMLRest Mock(params string[] rows)
    {
        var mock = MLRestMocks.Create();
        mock.Delimiter.Returns('~');
        mock.QueryOrThrow("ALVA.SYSTEM.Main", "LoginAllowed", Arg.Any<object[]>()).Returns(MLRestMocks.Output(rows));
        mock.QueryOrThrow("ALVA.SYSTEM.Main", "ChangeLoginState", Arg.Any<object[]>()).Returns(MLRestMocks.Output());
        return mock;
    }

    [Fact]
    public void Parse_NoLoginRow_Allowed()
    {
        var permission = LoginGate.Parse(MLRestMocks.Output("cerpani~(5)"), '~');

        Assert.Equal(new LoginPermission(true, false), permission);
    }

    [Theory]
    [InlineData("login~1~0", true, false)]
    [InlineData("login~0~1", false, true)]
    [InlineData("login~0~0", false, false)]
    public void Parse_LoginRow(string row, bool allowed, bool canAlter)
    {
        var permission = LoginGate.Parse(MLRestMocks.Output(row), '~');

        Assert.Equal(new LoginPermission(allowed, canAlter), permission);
    }

    [Fact]
    public void Query_SendsIpAndLoginNameAsDelimitedRows()
    {
        var mock = Mock("login~0~1");

        var permission = LoginGate.Query(mock, "10.0.0.7", "novak");

        Assert.Equal(new LoginPermission(false, true), permission);
        mock.Received(1).QueryOrThrow("ALVA.SYSTEM.Main", "LoginAllowed",
            Arg.Is<object[]>(a => a.Length == 2 && (string)a[0] == "ip~10.0.0.7" && (string)a[1] == "loginName~novak"));
    }

    [Fact]
    public void Allow_SendsLoginAllowedOne()
    {
        var mock = Mock();

        LoginGate.Allow(mock, "10.0.0.7", "novak");

        mock.Received(1).QueryOrThrow("ALVA.SYSTEM.Main", "ChangeLoginState",
            Arg.Is<object[]>(a => a.Length == 3 && (string)a[0] == "ip~10.0.0.7"
                                  && (string)a[1] == "loginName~novak" && (string)a[2] == "loginAllowed~1"));
    }

    [Fact]
    public void StationIp_NeverEmpty()
    {
        // Prázdný parametr by server odmítl ("Chybi parametr: ip"); bez adresy padá na jméno stroje.
        Assert.NotEmpty(LoginGate.StationIp());
    }
}
