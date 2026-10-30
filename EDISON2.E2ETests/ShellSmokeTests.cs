using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace EDISON2.E2ETests;

/// <summary>Start → strom ze serveru → okno profilu se serverovým schématem → čisté ukončení menu i křížkem.</summary>
[Trait("Category", "E2E")]
public class ShellSmokeTests
{
    [E2EFact]
    public void StartOpenProfileAndExit_E2ERun_ExitsCleanly()
    {
        using var shell = ShellSession.Start();

        shell.InvokeMenu("TopMenu", "Uživatel", "Vlastnosti uživatele");
        var profile = shell.WaitForWindow("Vlastnosti uživatele");
        // Skupina ze serverového schématu formuláře - důkaz round tripu přes celé UI.
        shell.WaitFor(() => profile.FindFirstDescendant(cf => cf.ByName("Export dokumentů")), "skupina „Export dokumentů\"");
        // Storno, ne OK: OK by uložil osobní adresář přes SaveUser.
        shell.Button(profile, "StornoBtn").Invoke();
        shell.WaitUntilClosed("Vlastnosti uživatele");

        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }

    [E2EFact]
    public void CloseByTitleBar_NoKeepsShell_YesExits()
    {
        using var shell = ShellSession.Start();

        shell.CloseMainWindow();
        shell.AnswerExitConfirm(yes: false);
        Assert.NotNull(shell.FirstTreeItemId());   // odmítnuté zavření shell nechá běžet

        shell.CloseMainWindow();
        shell.AnswerExitConfirm(yes: true);
        shell.WaitForExit();

        Assert.Equal(0, shell.ExitCode);
    }

    [E2EFact]
    public void CounterMiniAppAndVersionInfo_WorkWithoutServerWrites()
    {
        using var shell = ShellSession.Start();

        // Dev miniaplikace žije jen v procesu, rozložení doku drží E2E v paměti.
        shell.InvokeMenu("TopMenu", "Miniaplikace", "Zobrazit miniaplikaci");
        var picker = shell.WaitForWindow("Miniaplikace");
        shell.SelectListItem(picker, "AppList", "Počítadlo");
        shell.Button(picker, "OkBtn").Invoke();
        shell.WaitUntilClosed("Miniaplikace");

        shell.WaitFor(() => shell.Main.FindFirstDescendant(cf => cf.ByName("+1").And(cf.ByControlType(ControlType.Button)))
                          ?.AsButton(), "tlačítko +1 počítadla").Invoke();
        shell.WaitFor(() => shell.Main.FindFirstDescendant(cf => cf.ByName("Počet: 1")), "počet 1 v počítadle");

        shell.InvokeMenu("TopMenu", "Miniaplikace", "Zavřít miniaplikace");
        shell.WaitUntil(() => shell.Main.FindFirstDescendant(cf => cf.ByName("Počet: 1")) is null, "zavření počítadla");

        shell.InvokeMenu("TopMenu", "Nápověda", "Informace o verzi");
        var info = shell.WaitForWindow("Informace");
        var text = shell.PopupMessage(info);
        Assert.Contains($"Firma: {shell.Idfa}", text);
        Assert.Contains($"Server: {shell.ServerAddress}", text);
        shell.Button(info, "PositiveBtn").Invoke();
        shell.WaitUntilClosed("Informace");

        shell.ExitViaMenu();

        Assert.Equal(0, shell.ExitCode);
    }
}
