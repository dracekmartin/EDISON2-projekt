namespace EDISON2.E2ETests;

/// <summary>Přihlašovací okno při startu (<c>login=1</c>): předání shellu po přihlášení, konec bez shellu po zrušení.</summary>
[Trait("Category", "E2E")]
public class ShellLoginTests
{
    private const string LoginTitle = "Přihlášení EDISON";

    // Kopie StartupOptions.Development (DevLogin): s login=1 se pod ním shell objevit nesmí.
    private const string DebugUser = "martin.hubata";

    [E2EAccountFact]
    public void Login_TestAccount_LoginWindowHandsOverToShell()
    {
        var account = E2EAccountFactAttribute.Account.Value
                      ?? throw new InvalidOperationException("Testovací účet zmizel po discovery.");
        // Skutečný Login nuluje uživatelské flagy - s účtem vývojáře by test poškodil jeho data.
        Assert.False(string.Equals(account.Login, DebugUser, StringComparison.OrdinalIgnoreCase),
                     "Testovací účet nesmí být DEBUG uživatel.");

        using var shell = ShellSession.StartWithLogin();
        var login = shell.WaitForStartupWindow(LoginTitle);
        shell.SetValue(login, "UsernameBox", account.Login);
        shell.SetValue(login, "PasswordBox", account.Password);
        shell.Button(login, "OkBtn").Invoke();

        // Login okno zůstává jako podklad průběhu a zavře se až po vykreslení shellu.
        shell.WaitUntilReady(requireTree: false);
        shell.WaitUntilClosed(LoginTitle);
        shell.WaitFor(() => shell.StatusBarTexts() is { Count: > 0 } now && !now.Contains(DebugUser) ? now : null,
                      $"status bar pod uživatelem {account.Login}");

        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }

    [E2EFact]
    public void Login_UnknownUserThenCancel_ExitsWithoutShell()
    {
        using var shell = ShellSession.StartWithLogin();
        var login = shell.WaitForStartupWindow(LoginTitle);
        // Neexistující jméno server odmítne a nic nezapíše; přípona brání shodě se skutečným účtem.
        shell.SetValue(login, "UsernameBox", $"e2e.neexistuje.{Random.Shared.Next(100000, 1000000)}");
        shell.SetValue(login, "PasswordBox", "e2e");
        shell.Button(login, "OkBtn").Invoke();

        shell.WaitFor(() => login.FindFirstDescendant(cf => cf.ByAutomationId("ErrorText")) is { Name.Length: > 0 } error
                          ? error : null, "chyba přihlášení v okně");
        Assert.False(shell.HasMainWindow, "Shell vznikl bez přihlášení.");
        shell.Button(login, "CancelBtn").Invoke();
        shell.WaitForExit();

        Assert.Equal(0, shell.ExitCode);
    }
}
