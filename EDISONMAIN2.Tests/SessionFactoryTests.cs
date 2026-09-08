using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class SessionFactoryTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Establish_CopiesUserFlagsToServices(bool isAdmin, bool mayUpdate)
    {
        // Oba flagy jdou legacy hostu jako launch argy [14]/[15] - shell je musí držet nezávisle.
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

        SessionFactory.Establish(
            new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"),
            new LoginResult(7, "bob", "Bob", isAdmin, "", false, mayUpdate, ""), services);

        Assert.Equal(isAdmin, services.IsAdmin);
        Assert.Equal(mayUpdate, services.MayUpdate);
    }

    [Fact]
    public void Establish_MapsConnectionAndLoginToSession()
    {
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

        var ctx = SessionFactory.Establish(
            new ConnectionInfo("test-host", 9999, "CB_DB", "CB_DP"),
            new LoginResult(7, "bob", "Bob Novák", false, "", false, true, ""), services);

        var inner = ((MLRestAdapter)ctx.MLRest).Inner;
        Assert.Equal("test-host", inner.Server);
        Assert.Equal(9999, inner.Port);
        Assert.Equal("CB_DB", inner.DataNamespace);
        Assert.Equal("CB_DP", inner.Idfa);
        Assert.Equal("bob", inner.LoginName);
        Assert.Equal("Bob Novák", inner.FullName);
        Assert.Equal("7", inner.Mlp);
        // Session vzniká s CacheLocks=0 - na 1 ji přepne až LicenceVerifier po Accepted.
        Assert.Equal(0, inner.CacheLocks);
    }

    [Fact]
    public void Establish_WiresServicesToNewSession()
    {
        // Drift mezi bootstrap a switch-user cestou = služba ukazující na starou session.
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

        var ctx = SessionFactory.Establish(
            new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"),
            new LoginResult(7, "bob", "Bob", false, "", false, true, ""), services);

        Assert.Same(ctx.MLRest, services.MLRest);
        Assert.IsType<EDControls.UserPrefs.CacheUserPrefsService>(services.UserPrefs);
        Assert.NotNull(services.LicenceLoader);
        // Method-group konverze dává dvě instance delegátu - identita session = sdílený Target.
        Assert.Same(ctx.LicenceLoader.Target, services.LicenceLoader.Target);
    }

#if DEBUG
    [Fact]
    public void Establish_DebugBuild_DeterministicSessionGuid()
    {
        // Stop Debugging neuvolní ACTIVITY zámek; deterministický klíč ho při dalším
        // spuštění přepíše místo akumulace (a nefalšuje licenční počet).
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
        var conn = new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP");
        var login = new LoginResult(7, "bob", "Bob", false, "", false, true, "");

        var first = SessionFactory.Establish(conn, login, services);
        var second = SessionFactory.Establish(conn, login, services);

        Assert.Equal($"DEV-{Environment.MachineName}-{Environment.UserName}",
            ((MLRestAdapter)first.MLRest).Inner.GUID);
        Assert.Equal(((MLRestAdapter)first.MLRest).Inner.GUID, ((MLRestAdapter)second.MLRest).Inner.GUID);
    }

    [Fact]
    public void Establish_DebugBuild_SendsEmptyEdisonVersion()
    {
        // Dev verze 999999.9 nikdy nesedí s Caché - server-side version gate (Locks=1)
        // by v dev shodil každý request, prázdná verze ho vypíná.
        var services = new EDServices(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

        var ctx = SessionFactory.Establish(
            new ConnectionInfo("test-host", 9999, "CB_DP", "CB_DP"),
            new LoginResult(7, "bob", "Bob", false, "", false, true, ""), services);

        Assert.Equal("", ((MLRestAdapter)ctx.MLRest).Inner.EdisonVersion);
    }
#endif
}
