using System;
using System.Collections.Generic;

namespace EDISONMAIN2;

/// <summary>
/// Testovací běh pro UI E2E test (argument <c>e2e=1</c>, parsuje jen <see cref="StartupOptions.Development"/>).
/// Shell tak ovládá UI Automation na vývojářově stroji: nesmí sdílet session s jeho shellem, hlásit chyby
/// na podporu, přepisovat jeho <c>local.json</c> ani otevírat okna, na která test nečeká.
/// </summary>
public sealed class TestRunOptions
{
    private const string Token = "e2e=1";
    private const string IdfaKey = "idfa=";
    private const string PreShellPopupToken = "preshellpopup=1";

    /// <summary>ItemStatus hlavního okna - harness podle něj pozná build, který přepínač zná.</summary>
    public const string WindowMarker = "E2E";

    /// <summary>Text popupu před shellem (<see cref="PreShellPopup"/>); harness podle něj pozná, co zavírá.</summary>
    public const string PreShellPopupText = "E2E: hláška před vznikem shellu.";

    /// <summary>Startovní firma (<c>idfa=</c>): výchozí firma serveru nemusí mít pro DEBUG uživatele žádné moduly.</summary>
    public string? StartIdfa { get; private init; }

    /// <summary>Popup po přihlášení a před shellem (<c>preshellpopup=1</c>), jako hlášky licence při auto-loginu.</summary>
    public bool PreShellPopup { get; private init; }

    /// <summary>Null = běžný start. Cizí tokeny (<c>server=</c>, <c>restport=</c>) ignoruje.</summary>
    public static TestRunOptions? Parse(IReadOnlyList<string> args)
    {
        bool testRun = false, preShellPopup = false;
        string? idfa = null;
        foreach (var arg in args)
        {
            var token = arg.Trim();
            if (string.Equals(token, Token, StringComparison.OrdinalIgnoreCase)) testRun = true;
            else if (string.Equals(token, PreShellPopupToken, StringComparison.OrdinalIgnoreCase)) preShellPopup = true;
            else if (token.StartsWith(IdfaKey, StringComparison.OrdinalIgnoreCase) && token.Length > IdfaKey.Length)
                idfa = token[IdfaKey.Length..];
        }
        return testRun ? new TestRunOptions { StartIdfa = idfa, PreShellPopup = preShellPopup } : null;
    }
}
