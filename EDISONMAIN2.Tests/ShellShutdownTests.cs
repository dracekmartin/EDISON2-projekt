namespace EDISONMAIN2.Tests;

public class ShellShutdownTests
{
    [Fact]
    public void Force_WithoutAttachedShell_JustShutsDown()
    {
        int shutdowns = 0;
        var shutdown = new ShellShutdown(() => shutdowns++);

        shutdown.Force();

        Assert.Equal(1, shutdowns);
    }

    [Fact]
    public void Force_SuppressesCloseConfirmBeforeShutdown()
    {
        // Pořadí je invariant: potvrzovací dialog shellu se musí vypnout dřív, než Shutdown
        // spustí Closing - jinak force logout uvízne na dialogu, který smí uživatel odmítnout.
        var calls = new List<string>();
        var shutdown = new ShellShutdown(() => calls.Add("shutdown"));
        shutdown.AttachShell(() => calls.Add("suppress"));

        shutdown.Force();

        Assert.Equal(["suppress", "shutdown"], calls);
    }
}
