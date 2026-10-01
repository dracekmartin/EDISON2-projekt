namespace EDISON2.E2ETests;

/// <summary>Start → strom ze serveru → okno profilu se serverovým schématem → čisté ukončení.</summary>
[Trait("Category", "E2E")]
public class ShellSmokeTests
{
    [E2EFact]
    public void StartOpenProfileAndExit_E2ERun_ExitsCleanly()
    {
        using var shell = ShellSession.Start();

        shell.InvokeMenu("TopMenu", "Uživatel", "Aktuální uživatel");
        var profile = shell.WaitForWindow("Vlastnosti uživatele");
        // Skupina ze serverového schématu formuláře - důkaz round tripu přes celé UI.
        shell.WaitFor(() => profile.FindFirstDescendant(cf => cf.ByName("Export dokumentů")), "skupina „Export dokumentů\"");
        // Storno, ne OK: OK by uložil osobní adresář přes SaveUser.
        shell.Button(profile, "StornoBtn").Invoke();
        shell.WaitUntilClosed("Vlastnosti uživatele");

        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }
}
