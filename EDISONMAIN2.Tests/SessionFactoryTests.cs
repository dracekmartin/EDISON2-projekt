using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class SessionFactoryTests
{
    private static EDServices NewServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static readonly ConnectionInfo Conn = new("test-host", 9999, "CB_DP", "CB_DP");

    private static LoginResult Bob() => new(7, "bob", "Bob", false, "", false, true, "");

    private static MLRest Inner(SessionContext ctx) => ((MLRestAdapter)ctx.MLRest).Inner;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Establish_CopiesUserFlagsToServices(bool isAdmin, bool mayUpdate)
    {
        // Oba flagy jdou legacy hostu jako launch argy [14]/[15] - shell je musí držet nezávisle.
        var services = NewServices();

        SessionFactory.Establish(Conn, new LoginResult(7, "bob", "Bob", isAdmin, "", false, mayUpdate, ""),
                                 services, new StartupOptions());

        Assert.Equal(isAdmin, services.IsAdmin);
        Assert.Equal(mayUpdate, services.MayUpdate);
    }

    [Fact]
    public void Establish_CopiesUserDirectoryAndEmailToServices()
    {
        // Legacy host je dostane jako launch argy [16]/[17] → MLProps.UserDirectory / UserEmail
        // (agendy do adresáře exportují přílohy, e-mail je výchozí odesílatel).
        var services = NewServices();

        SessionFactory.Establish(Conn,
            new LoginResult(7, "bob", "Bob", false, @"C:\Mline\EdisonData", false, false, "bob@firma.cz"),
            services, new StartupOptions());

        Assert.Equal(@"C:\Mline\EdisonData", services.UserDirectory);
        Assert.Equal("bob@firma.cz", services.UserEmail);
    }

    [Fact]
    public void Establish_MapsConnectionAndLoginToSession()
    {
        var ctx = SessionFactory.Establish(
            new ConnectionInfo("test-host", 9999, "CB_DB", "CB_DP"),
            new LoginResult(7, "bob", "Bob Novák", false, "", false, true, ""), NewServices(), new StartupOptions());

        var inner = Inner(ctx);
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
        var services = NewServices();

        var ctx = SessionFactory.Establish(Conn, Bob(), services, new StartupOptions());

        Assert.Same(ctx.MLRest, services.MLRest);
        Assert.IsType<EDControls.UserPrefs.CacheUserPrefsService>(services.UserPrefs);
        // Loader nese jen služby - kontext session ho nevrací (jediný kanál).
        Assert.IsType<LicenceService>(services.LicenceLoader?.Target);
    }

    [Fact]
    public void Establish_SwitchUser_ReplacesLicenceLoader()
    {
        var services = NewServices();
        SessionFactory.Establish(Conn, Bob(), services, new StartupOptions());
        var first = services.LicenceLoader?.Target;

        SessionFactory.Establish(Conn, Bob(), services, new StartupOptions());

        Assert.NotSame(first, services.LicenceLoader?.Target);
    }

    [Fact]
    public void Establish_Development_DeterministicSessionGuid()
    {
        // Stop Debugging neuvolní ACTIVITY zámek; deterministický klíč ho při dalším
        // spuštění přepíše místo akumulace (a nefalšuje licenční počet).
        var options = StartupOptions.Development([], devLoginSwitch: null);

        var first = SessionFactory.Establish(Conn, Bob(), NewServices(), options);
        var second = SessionFactory.Establish(Conn, Bob(), NewServices(), options);

        Assert.Equal($"DEV-{Environment.MachineName}-{Environment.UserName}", Inner(first).GUID);
        Assert.Equal(Inner(first).GUID, Inner(second).GUID);
    }

    [Fact]
    public void Establish_Development_SendsEmptyEdisonVersion()
    {
        // Dev verze 999999.9 nikdy nesedí s Caché - server-side version gate (Locks=1)
        // by v dev shodil každý request, prázdná verze ho vypíná.
        var ctx = SessionFactory.Establish(Conn, Bob(), NewServices(), StartupOptions.Development([], null));

        Assert.Equal("", Inner(ctx).EdisonVersion);
    }

    [Fact]
    public void Establish_TestRun_GetsOwnSessionGuid()
    {
        // Logout maže zámky podle GUID - E2E instance nesmí sdílet GUID s vývojářovým shellem.
        var ctx = SessionFactory.Establish(Conn, Bob(), NewServices(), StartupOptions.Development(["e2e=1"], null));

        Assert.Equal($"E2E-{Environment.MachineName}-{Environment.UserName}", Inner(ctx).GUID);
    }

    [Fact]
    public void Establish_Production_RandomGuidPerSessionAndStationVersion()
    {
        // Náhodný GUID = každá session vlastní ACTIVITY zámek; verze aktivuje version gate serveru.
        var options = StartupOptions.Production();

        var first = SessionFactory.Establish(Conn, Bob(), NewServices(), options);
        var second = SessionFactory.Establish(Conn, Bob(), NewServices(), options);

        Assert.NotEqual(Inner(first).GUID, Inner(second).GUID);
        Assert.Equal(VerzeInf.Current, Inner(first).EdisonVersion);
    }
}
