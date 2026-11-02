using EDISONMAIN2.Windows.LoginWindow;

namespace EDISON2.IntegrationTests;

/// <summary>
/// <c>ALVA.SYS.Login.ChangePassword</c> jen cestami, které skončí před zápisem hesla: neexistující jméno (status 4)
/// a špatné staré heslo (status 2, server jen porovná hash, pokusy nepočítá). Status 3 by s heslem, které server
/// přijme, heslo testovacího účtu změnil. Parser neznámý nebo nečíselný status tiše přečte jako 99.
/// </summary>
[Trait("Category", "Integration")]
public class ChangePasswordTests
{
    [DevServerFact]
    public void ChangePassword_UnknownUser_ReturnsStatus4_ParserReportsMissingUser()
    {
        var rest = DevServer.Connect();
        var username = $"edison2_it_neexistuje_{Guid.NewGuid():N}";

        var raw = rest.QueryOrThrow("ALVA.SYS.Login", "ChangePassword", new object[] { username, "stare-it", "Nove-heslo-it-1" });

        Assert.True(raw.Length > 1 && raw[1]?.ToString() == "4" + rest.Delimiter, "ChangePassword nevrátil status 4 s prázdným textem.");
        var result = UserAuthenticator.ChangePassword(rest, username, "stare-it", "Nove-heslo-it-1");
        Assert.Equal(new ChangePasswordResult(false, "Změna hesla není možná (uživatel neexistuje)."), result);
    }

    [TestAccountFact]
    public void ChangePassword_WrongOldPassword_ReturnsStatus2_ParserShowsServerText()
    {
        var account = DevServer.Account!;
        var rest = DevServer.Connect();
        var wrongOld = account.Password + "-spatne";

        var raw = rest.QueryOrThrow("ALVA.SYS.Login", "ChangePassword", new object[] { account.Login, wrongOld, "Nove-heslo-it-1" });

        var parts = (raw.Length > 1 ? raw[1]?.ToString() ?? "" : "").Split(rest.Delimiter);
        Assert.True(parts.Length == 2 && parts[0] == "2" && parts[1].Length > 0, "ChangePassword nevrátil status 2 s textem serveru.");
        var result = UserAuthenticator.ChangePassword(rest, account.Login, wrongOld, "Nove-heslo-it-1");
        Assert.Equal(new ChangePasswordResult(false, parts[1]), result);
    }
}
