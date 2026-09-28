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

    /// <summary>ItemStatus hlavního okna - harness podle něj pozná build, který přepínač zná.</summary>
    public const string WindowMarker = "E2E";

    /// <summary>Null = běžný start. Cizí tokeny (<c>server=</c>, <c>restport=</c>) ignoruje.</summary>
    public static TestRunOptions? Parse(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
            if (string.Equals(arg.Trim(), Token, StringComparison.OrdinalIgnoreCase)) return new TestRunOptions();
        return null;
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
