using System;
using System.Collections.Generic;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2;

/// <summary>
/// Testovací běh pro UI E2E test (argument <c>e2e=1</c>, jen DEBUG build). Shell tak ovládá UI
/// Automation na vývojářově stroji: nesmí sdílet session s jeho shellem, hlásit chyby na podporu,
/// přepisovat jeho <c>local.json</c> ani otevírat okna, na která test nečeká.
/// </summary>
public sealed class TestRunOptions
{
    private const string Token = "e2e=1";
    private const string IdfaKey = "idfa=";

    /// <summary>ItemStatus hlavního okna - harness podle něj pozná build, který přepínač zná.</summary>
    public const string WindowMarker = "E2E";

    /// <summary>Startovní firma (<c>idfa=</c>): výchozí firma serveru nemusí mít pro DEBUG uživatele žádné moduly.</summary>
    public string? StartIdfa { get; private init; }

    /// <summary>Null = běžný start. Cizí tokeny (<c>server=</c>, <c>restport=</c>) ignoruje.</summary>
    public static TestRunOptions? Parse(IReadOnlyList<string> args)
    {
        bool testRun = false;
        string? idfa = null;
        foreach (var arg in args)
        {
            var token = arg.Trim();
            if (string.Equals(token, Token, StringComparison.OrdinalIgnoreCase)) testRun = true;
            else if (token.StartsWith(IdfaKey, StringComparison.OrdinalIgnoreCase) && token.Length > IdfaKey.Length)
                idfa = token[IdfaKey.Length..];
        }
        return testRun ? new TestRunOptions { StartIdfa = idfa } : null;
    }

    /// <summary>Nastavení pro celý proces; volat jako první krok startupu.</summary>
    public void ApplyProcessIsolation()
    {
        ErrorReports.Enabled = false;
        EDUsageTelemetry.Enabled = false;
        // Logout maže zámky podle GUID (LockClearExt) - se společným DEV GUID by E2E shodil vývojářův shell.
        SessionFactory.SessionTag = WindowMarker;
        // Widget podpory by pod vývojářovou identitou volal podpora.m-line.cz; čte se až při zobrazení.
        Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, "1");
    }
}
