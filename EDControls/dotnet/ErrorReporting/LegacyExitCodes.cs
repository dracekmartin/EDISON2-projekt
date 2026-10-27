namespace EDControls.ErrorReporting;

internal enum LegacyExitKind { Crash, Hang, LaunchFailed }

/// <summary>
/// Druh konce hostovaného legacy modulu podle exit kódu: text hlášení, typ záznamu a <c>data.kind</c>.
/// Kódy hosta 2 a 3 zrcadlí <c>HostedErrorReporter.ExitCodes</c> v repu EDISON, měnit jen spolu.
/// </summary>
internal static class LegacyExitCodes
{
    internal const int HostLaunchFailed = 2;
    internal const int HostFatalReported = 3;

    /// <summary>Windows zavřel okno, které přestalo odpovídat (událost 1002 Application Hang). Žádný kód v repech ho nevrací.</summary>
    internal const int WindowsHangKill = unchecked((int)0xCFFFFFFF);

    internal static LegacyExitKind Classify(int exitCode) => exitCode switch
    {
        WindowsHangKill => LegacyExitKind.Hang,
        HostLaunchFailed => LegacyExitKind.LaunchFailed,
        _ => LegacyExitKind.Crash,
    };

    internal static string KindName(LegacyExitKind kind) => kind switch
    {
        LegacyExitKind.Hang => "hang",
        LegacyExitKind.LaunchFailed => "launchFailed",
        _ => "crash",
    };

    /// <summary>Zaseknutý modul je problém výkonu, ne pád: varování, ať nezahlcuje pohled na chyby.</summary>
    internal static string ReportType(LegacyExitKind kind) =>
        kind == LegacyExitKind.Hang ? ErrorReportBuilder.TypeWarning : ErrorReportBuilder.TypeError;

    /// <summary>
    /// Text hlášení. Neznámý kód drží dřívější znění „skončil s kódem N (0x…)", ať na něm dál fungují
    /// hledání a filtry v Přehledu telemetrie.
    /// </summary>
    internal static string Describe(string exeName, string formFqn, int exitCode, string? cause)
    {
        var module = $"Modul {exeName} ({formFqn})";
        var code = $"{exitCode} (0x{exitCode:X8})";
        var text = Classify(exitCode) switch
        {
            LegacyExitKind.Hang => $"{module} přestal odpovídat a Windows ho ukončil, kód {code}.",
            LegacyExitKind.LaunchFailed => $"{module} se nepodařilo spustit, kód {code}.",
            // Kód 1 dá ukončení ve Správci úloh i agenda sama (TISK při chybě exportu).
            _ when exitCode == 1 => $"{module} skončil s kódem {code}: chyba modulu, nebo ukončení zvenku.",
            _ when CrashName(exitCode) is { } name => $"{module} spadl ({name}), kód {code}.",
            _ => $"{module} skončil s kódem {code}.",
        };
        return string.IsNullOrEmpty(cause) ? text : $"{text} Příčina: {cause}";
    }

    private static string? CrashName(int exitCode) => unchecked((uint)exitCode) switch
    {
        0xC0000005 => "neplatný přístup do paměti",
        0xC00000FD => "přetečení zásobníku",
        0xC000041D => "výjimka v obsluze okna",
        0xC0000409 => "okamžité ukončení procesu",
        0xE0434352 => "neošetřená výjimka .NET",
        HostFatalReported => "neošetřená chyba po chybovém dialogu",
        _ => null,
    };
}
