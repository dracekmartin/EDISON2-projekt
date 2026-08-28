namespace EDControls.ErrorReporting;

/// <summary>
/// Syntetická výjimka pro hlášení tvrdého pádu hostovaného legacy modulu (proces skončil
/// nenulovým kódem). Nikdy se nehází - vzniká jen jako nosič textu pro <see cref="ErrorReportBuilder"/>.
/// </summary>
public sealed class LegacyHostCrashedException(string exeName, string formFqn, int exitCode)
    : Exception($"Modul {exeName} ({formFqn}) skončil s kódem {exitCode} (0x{exitCode:X8}).")
{
    public string ExeName { get; } = exeName;
    public string FormFqn { get; } = formFqn;
    public int ExitCode { get; } = exitCode;
}
