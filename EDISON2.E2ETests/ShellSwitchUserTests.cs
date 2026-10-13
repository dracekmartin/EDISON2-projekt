using EDTesting;

namespace EDISON2.E2ETests;

/// <summary>„Přihlásit jiného uživatele" testovacím účtem DEV: shell po přepnutí načte session nového uživatele.</summary>
[Trait("Category", "E2E")]
public class ShellSwitchUserTests
{
    // Kopie StartupOptions.Development (DevLogin): celé jméno DEBUG uživatele, pod kterým E2E shell startuje.
    private const string DebugUser = "martin.hubata";

    // Kopie EdisonMainViewModel.LoginAsAnotherUserAsync.
    private const string SwitchConfirmText = "Přihlásit jiného uživatele zavře všechna otevřená okna. Pokračovat?";

    private const string LoginTitle = "Přihlášení EDISON";

    [E2EAccountFact]
    public void SwitchUser_TestAccount_ShellLoadsSessionOfTestUser()
    {
        // Skip podle účtu rozhodl atribut, výjimka serveru tady projde jako chyba testu.
        var account = E2EAccountFactAttribute.Account.Value
                      ?? throw new InvalidOperationException("Testovací účet zmizel po discovery.");
        // Skutečný Login nuluje uživatelské flagy - s účtem vývojáře by test poškodil jeho data.
        Assert.False(string.Equals(account.Login, DebugUser, StringComparison.OrdinalIgnoreCase),
                     "Testovací účet nesmí být DEBUG uživatel.");

        using var shell = ShellSession.Start();
        Assert.Contains(DebugUser, shell.StatusBarTexts());
        var debugTree = shell.FirstTreeItemId() ?? throw new InvalidOperationException("Strom DEBUG uživatele je prázdný.");

        shell.InvokeMenu("TopMenu", "Uživatel", "Přihlásit jiného uživatele");
        var confirm = shell.WaitForWindow("Potvrzení");
        // Titulek „Potvrzení" a „Ano" má i brána přihlášení - její Ano by povolilo přihlašování celému serveru.
        Assert.Equal(SwitchConfirmText, shell.PopupMessage(confirm));
        shell.Button(confirm, "PositiveBtn").Invoke();

        // Login okno naskočí hned po potvrzení, čekání na zavření potvrzení by ho vzalo za nečekané.
        var login = shell.WaitForWindow(LoginTitle);
        shell.WaitUntilClosed("Potvrzení");
        shell.SetValue(login, "UsernameBox", account.Login);
        shell.SetValue(login, "PasswordBox", account.Password);
        shell.Button(login, "OkBtn").Invoke();
        shell.WaitUntilClosed(LoginTitle);

        // Status bar ukazuje celé jméno, bez něj přihlašovací. Mění se hned po přihlášení, strom až po načtení session.
        shell.WaitFor(() => shell.StatusBarTexts() is { Count: > 0 } now && !now.Contains(DebugUser) ? now : null,
                      $"status bar pod uživatelem {account.Login}");
        shell.WaitForTreeReload(debugTree);

        // Konec přes menu: logout uvolní zámek nové session pod E2E GUID.
        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }
}
