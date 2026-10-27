namespace EDControls.ErrorReporting;

/// <summary>
/// Syntetická výjimka pro hlášení tvrdého pádu hostovaného legacy modulu (proces skončil
/// nenulovým kódem). Nikdy se nehází - vzniká jen jako nosič textu pro <see cref="ErrorReportBuilder"/>.
/// <paramref name="cause"/> je první řádek výjimky, kterou host zapsal před pádem (typ a zpráva).
/// </summary>
public sealed class LegacyHostCrashedException(string exeName, string formFqn, int exitCode, string? cause)
    : Exception(LegacyExitCodes.Describe(exeName, formFqn, exitCode, cause))
{
    /// <summary>Původní signatura pro exe přeložené proti starší samostatně nasazené EDControls.dll.</summary>
    public LegacyHostCrashedException(string exeName, string formFqn, int exitCode)
        : this(exeName, formFqn, exitCode, null) { }

    public string ExeName { get; } = exeName;
    public string FormFqn { get; } = formFqn;
    public int ExitCode { get; } = exitCode;
}
