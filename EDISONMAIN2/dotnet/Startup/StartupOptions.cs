using System;
using System.Collections.Generic;
using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.Modules;
using EDISONMAIN2.SupportStrip;

namespace EDISONMAIN2;

/// <summary>
/// Čím se liší start ve vývoji od stanice zákazníka. Sestavuje se jednou v <see cref="App"/>
/// (jediný <c>#if DEBUG</c> shellu kromě <see cref="VerzeInf.Current"/>, sdíleného s EDUPDATE2)
/// a předává se bootstrapperu i <see cref="SessionFactory"/>; výchozí hodnoty jsou chování
/// Release, takže zapomenutá volba neudělá z Release dev build.
/// </summary>
public sealed record StartupOptions
{
    /// <summary><c>EDISON2_DEVLOGIN=0</c> vypne v DEBUG syntetický login - login dialog pak jde ověřit bez Release buildu.</summary>
    public const string DevLoginVariable = "EDISON2_DEVLOGIN";

    /// <summary>Přihlášení bez dialogu. Zároveň neukládá přihlašovací jméno: jméno v local.json
    /// patří poslednímu ostrému přihlášení (předvyplnění a zapamatované heslo exe na témže stroji).</summary>
    public LoginResult? DevLogin { get; init; }

    /// <summary>Prefix deterministického session GUID; null = náhodný GUID per session.</summary>
    public string? SessionGuidTag { get; init; }

    /// <summary>Posílat verzi stanice v session = serverový per-request version gate (prázdná verze ho vypíná).</summary>
    public bool SendsVersion { get; init; } = true;

    public bool DevMiniApps { get; init; }

    /// <summary>Předvyplnění setup wizardu dev serverem (<see cref="SettingsLoader.Load"/>).</summary>
    public bool UseDevDefaults { get; init; }

    /// <summary>Menu „Test funkcí" (konstruktor VM shellu) a diagnostické značky stromu (<see cref="InternalDiagnostics"/>).</summary>
    public bool InternalDiagnosticsEnabled { get; init; }

    /// <summary>UI E2E běh (<c>e2e=1</c>); null = běžný start.</summary>
    public TestRunOptions? TestRun { get; init; }

    /// <summary>Hláška po přihlášení a před vznikem shellu; null = žádná. E2E jí ověřuje,
    /// že zavření okna před shellem aplikaci neukončí.</summary>
    public string? PreShellNotice { get; init; }

    public static StartupOptions Development(IReadOnlyList<string> args, string? devLoginSwitch)
    {
        var testRun = TestRunOptions.Parse(args);
        return new StartupOptions
        {
            // E2E přepínač ignoruje: proměnnou zdědí z vývojářova terminálu a login okno by test shodilo.
            // Login okno chce E2E jen výslovně tokenem login=1.
            DevLogin = (devLoginSwitch == "0" && testRun is null) || testRun is { RealLogin: true }
                ? null
                : new LoginResult(201, "martin.hubata", "martin.hubata", true, "", false, true, ""),
            // Stop Debugging (hard-kill) ACTIVITY zámek neuvolní, deterministický klíč (idfa~ip~guid) ho
            // při dalším spuštění přepíše místo akumulace. E2E vlastní: logout maže zámky podle GUID.
            SessionGuidTag = testRun is null ? "DEV" : TestRunOptions.WindowMarker,
            // Dev verze 999999.9 nikdy nesedí s Caché, version gate (Locks=1) by shodil každý request.
            SendsVersion = false,
            DevMiniApps = true,
            UseDevDefaults = true,
            InternalDiagnosticsEnabled = true,
            TestRun = testRun,
            PreShellNotice = testRun is { PreShellPopup: true } ? TestRunOptions.PreShellPopupText : null,
        };
    }

    /// <summary>Release nečte argumenty ani prostředí - stanice zákazníka nesmí dostat skrytý vypínač.</summary>
    public static StartupOptions Production() =>
        // Kolegové M-line validují Release build, proto diagnostika i na stanicích jejich domény.
        new() { InternalDiagnosticsEnabled = ModuleTreeStation.IsMlineDomainStation };

    /// <summary>Procesové statiky; jediné místo, které je nastavuje. Volat jako první krok startupu.</summary>
    public void ApplyToProcess()
    {
        InternalDiagnostics.Enabled = InternalDiagnosticsEnabled;
        if (TestRun is null) return;

        // E2E ovládá shell na vývojářově stroji: nesmí hlásit chyby na podporu ani psát telemetrii.
        ErrorReports.Enabled = false;
        EDUsageTelemetry.Enabled = false;
        // Widget podpory by pod vývojářovou identitou volal podpora.m-line.cz; čte se až při zobrazení.
        Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, "1");
        // Výchozí téma a písmo, vývojářovy preference zůstanou netknuté. Složku maže harness, ne shell.
        EDAppearance.UseCommonFolder(TestRunOptions.CommonFolder);
    }
}
