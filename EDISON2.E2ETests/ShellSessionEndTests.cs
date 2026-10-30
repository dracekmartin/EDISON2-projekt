namespace EDISON2.E2ETests;

/// <summary>Server ukončí session (zámek ACTIVITY zmizel): hláška a konec aplikace bez potvrzení a chybových dialogů.</summary>
[Trait("Category", "E2E")]
public class ShellSessionEndTests
{
    // Kopie ServerSessionEnd.ActivityLostMessage.
    private const string ActivityLostText = "Spojení se serverem bylo přerušeno. Aplikace bude ukončena.";

    // Kopie TestRunOptions.PreShellPopupText.
    private const string PreShellPopupText = "E2E: hláška před vznikem shellu.";

    [E2EFact]
    public void ActivityLockDropped_NextRequest_InformsAndExitsWithoutConfirm()
    {
        using var shell = ShellSession.Start();
        // Hlášku může vyvolat i request na pozadí, ještě než test otevře profil.
        shell.AllowWindow("Informace");
        shell.DropActivityLock();

        // Poll odhlášení jede bez kontroly zámku, konec session pozná až request z UI.
        // Profil se může ukázat dřív, než jeho request se zámkem skončí.
        shell.AllowWindow("Vlastnosti uživatele");
        shell.InvokeMenu("TopMenu", "Uživatel", "Vlastnosti uživatele");
        var popup = shell.WaitForPopup("Informace", ActivityLostText);
        shell.Button(popup, "PositiveBtn").Invoke();
        shell.WaitForExit();

        Assert.Equal(0, shell.ExitCode);
    }

    [E2EFact]
    public void ActivityLockDroppedBeforeShell_InformsAndExitsWithoutShell()
    {
        // Popup stojí po vzniku zámku, další request se zámkem je zápis přihlášení (LogLogin).
        using var shell = ShellSession.StartBeforeShell("preshellpopup=1");
        var notice = shell.WaitForStartupWindow("Informace");
        Assert.Equal(PreShellPopupText, shell.PopupMessage(notice));

        shell.DropActivityLock();
        shell.Button(notice, "PositiveBtn").Invoke();

        var popup = shell.WaitForPopup("Informace", ActivityLostText);
        Assert.False(shell.HasMainWindow, "Shell vznikl nad session, kterou server ukončil.");
        shell.Button(popup, "PositiveBtn").Invoke();
        shell.WaitForExit();

        Assert.Equal(0, shell.ExitCode);
    }
}
