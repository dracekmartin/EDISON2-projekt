namespace EDISON2.E2ETests;

/// <summary>Okna před vznikem shellu: jejich zavření nesmí aplikaci ukončit.</summary>
[Trait("Category", "E2E")]
public class ShellStartupTests
{
    // Kopie TestRunOptions.PreShellPopupText.
    private const string PreShellPopupText = "E2E: hláška před vznikem shellu.";

    [E2EFact]
    public void PopupBeforeShell_Closed_ShellStartsAndKeepsRunning()
    {
        // DevLogin přihlásí bez login okna jako auto-login, popup je tak jediné okno procesu.
        using var shell = ShellSession.StartBeforeShell("preshellpopup=1");

        var popup = shell.WaitForStartupWindow("Informace");
        Assert.Equal(PreShellPopupText, shell.PopupMessage(popup));
        shell.Button(popup, "PositiveBtn").Invoke();
        shell.WaitUntilClosed("Informace");

        shell.WaitUntilReady();
        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }
}
