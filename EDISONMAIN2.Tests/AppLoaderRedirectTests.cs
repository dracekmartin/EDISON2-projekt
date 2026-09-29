namespace EDISONMAIN2.Tests;

/// <summary>
/// Kdy se přímý start shellu přesměruje přes EDLOADER: bez značky <c>reason=2</c> ano, jinak by
/// na připnutý shell nepůsobil uzel NewShell. Výjimky nesmí rozbít VS, E2E ani „Připojit k databázi".
/// </summary>
public class AppLoaderRedirectTests
{
    [Fact]
    public void DirectStart_NextToLoader_Redirects()
    {
        Assert.True(App.ShouldRedirectToLoader([], loaderPresent: true, alreadyRedirected: false));
    }

    [Theory]
    [InlineData("reason=2")]
    [InlineData("REASON=2")]
    [InlineData(" reason=2 ")]
    public void LoaderMarker_DoesNotRedirect(string marker)
    {
        Assert.False(App.ShouldRedirectToLoader([marker], loaderPresent: true, alreadyRedirected: false));
    }

    [Fact]
    public void OtherReason_Redirects()
    {
        // reason=1 je legacy setup, EDLOADER ho novému shellu nepředává.
        Assert.True(App.ShouldRedirectToLoader(["reason=1"], loaderPresent: true, alreadyRedirected: false));
    }

    [Fact]
    public void ConnectionArgs_DoNotRedirect()
    {
        // EDLOADER by argumenty nepředal a instance by skončila na serveru z EDISONini.xml.
        Assert.False(App.ShouldRedirectToLoader(["server=10.0.0.5", "restport=57770"],
                                                loaderPresent: true, alreadyRedirected: false));
    }

    [Fact]
    public void NoLoaderNextToExe_DoesNotRedirect()
    {
        Assert.False(App.ShouldRedirectToLoader([], loaderPresent: false, alreadyRedirected: false));
    }

    [Fact]
    public void AlreadyRedirected_DoesNotRedirectAgain()
    {
        Assert.False(App.ShouldRedirectToLoader([], loaderPresent: true, alreadyRedirected: true));
    }
}
