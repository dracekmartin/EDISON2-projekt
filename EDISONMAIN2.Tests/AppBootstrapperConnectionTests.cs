namespace EDISONMAIN2.Tests;

/// <summary>Připojení z příkazové řádky (Připojit k databázi) má přednost před EDISONini.xml.</summary>
public class AppBootstrapperConnectionTests
{
    [Fact]
    public void ResolveConnection_Override_WinsWithoutReadingSettings()
    {
        var (server, port) = AppBootstrapper.ResolveConnection(
            new ConnectionOverride("10.0.0.5", 12345),
            () => throw new InvalidOperationException("EDISONini.xml se nemá číst"));

        Assert.Equal(("10.0.0.5", 12345), (server, port));
    }

    [Fact]
    public void ResolveConnection_NoOverride_UsesSettings()
    {
        var (server, port) = AppBootstrapper.ResolveConnection(
            null, () => new ServerSettings { Host = "ini-host", Port = 57770 });

        Assert.Equal(("ini-host", 57770), (server, port));
    }
}
