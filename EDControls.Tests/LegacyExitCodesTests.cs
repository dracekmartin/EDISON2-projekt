using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Konec hostovaného modulu podle exit kódu: zaseknutí zavřené Windows není pád, nespuštěný modul taky ne.
/// Neznámý kód drží dřívější text, ať v Přehledu telemetrie fungují hledání a filtry.
/// </summary>
public class LegacyExitCodesTests
{
    private const string Exe = "ISYJR2.exe";
    private const string Form = "ALVA.ISY.ISYJR2.formISYJR2HlavniOkno";

    [Theory]
    [InlineData(-805306369, "hang", "warning", "přestal odpovídat a Windows ho ukončil")]
    [InlineData(2, "launchFailed", "error", "se nepodařilo spustit")]
    [InlineData(1, "crash", "error", "chyba modulu, nebo ukončení zvenku")]
    [InlineData(3, "crash", "error", "neošetřená chyba po chybovém dialogu")]
    [InlineData(-1073741571, "crash", "error", "přetečení zásobníku")]
    [InlineData(-1073740771, "crash", "error", "výjimka v obsluze okna")]
    [InlineData(-1073741819, "crash", "error", "neplatný přístup do paměti")]
    [InlineData(-532462766, "crash", "error", "neošetřená výjimka .NET")]
    [InlineData(-1073740791, "crash", "error", "okamžité ukončení procesu")]
    public void KnownCode_KindTypeAndWording(int code, string kind, string type, string wording)
    {
        var classified = LegacyExitCodes.Classify(code);

        Assert.Equal(kind, LegacyExitCodes.KindName(classified));
        Assert.Equal(type, LegacyExitCodes.ReportType(classified));
        var text = LegacyExitCodes.Describe(Exe, Form, code, null);
        Assert.Contains(wording, text);
        Assert.Contains($"Modul {Exe} ({Form})", text);
        Assert.Contains($"0x{code:X8}", text);
    }

    [Fact]
    public void UnknownCode_KeepsPreviousWording()
    {
        Assert.Equal($"Modul {Exe} ({Form}) skončil s kódem 42 (0x0000002A).", LegacyExitCodes.Describe(Exe, Form, 42, null));
    }

    [Fact]
    public void Cause_IsAppended()
    {
        Assert.EndsWith(" Příčina: System.InvalidOperationException: boom",
            LegacyExitCodes.Describe(Exe, Form, 3, "System.InvalidOperationException: boom"));
    }

    [Fact]
    public void Exception_CarriesSameText()
    {
        Assert.Equal(LegacyExitCodes.Describe(Exe, Form, -805306369, null),
            new LegacyHostCrashedException(Exe, Form, -805306369).Message);
    }
}
