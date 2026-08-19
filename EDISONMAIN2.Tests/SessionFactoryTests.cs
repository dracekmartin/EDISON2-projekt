using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class SessionFactoryTests
{
#if DEBUG
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
