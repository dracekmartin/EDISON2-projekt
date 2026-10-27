using System.IO;
using System.Runtime.InteropServices;

namespace EDControls.Tests;

/// <summary>Rozdělí argumenty procesu tak, jak je dostane <c>Main(string[] args)</c> hosta (Win32 pravidla argv).</summary>
internal static class ProcessArgv
{
    public static string[] Split(string arguments)
    {
        // Prvek 0 je jméno programu s vlastními pravidly parseru - argumenty začínají až za ním.
        var argv = CommandLineToArgvW("host.exe " + arguments, out var argc);
        try
        {
            var result = new string[argc - 1];
            for (var i = 1; i < argc; i++)
                result[i - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!;
            return result;
        }
        finally { LocalFree(argv); }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argc);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}

public class EdisonLaunchArgsTests
{
    private static string[] ValidArgs(params string[] extra) =>
    [
        "EDISONMAIN2", "192.168.16.1", "57770", "CB_DP", "CB_DP",
        "jan.novak", "Jan Novák", "167", "ALVA.TISKJR.formTISKJRSeznamSestav", "guid-123",
        .. extra
    ];

    /// <summary>Record odpovídající fixture - neprázdná pole mají každé jinou hodnotu, posun o pozici test pozná.</summary>
    private static EdisonLaunchArgs FixtureRecord() =>
        new("192.168.16.1", 57770, "MLINE", "CB_DP", "jan.novak", "Jan Novák", "157",
            "ALVA.USERS.formUSERSLocks", "SHELLGUID-1", "AdminMode=1")
        {
            AgendaPath = @"C:\Edison\USERS.exe",
            LicensedElements = "26001,6001",
            ShortcutPayload = "",
            IsAdmin = true,
            MayUpdate = false,
            UserDirectory = @"C:\Mline\EdisonData",
            UserEmail = "jan.novak@firma.cz",
            LicenceVersion = "2.001",
            LicenceState = "3",
            LicenceValidTo = "31.12.2026",
            ShellMonitor = @"\\.\DISPLAY2",
        };

    /// <summary>Sdílená fixture s hostem (kopie vedle FormToRunGetter.cs v repu EDISON).</summary>
    private static string FixtureArgv() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "launch-args.fixture.txt"))
            .Single(l => l.Length > 0 && !l.StartsWith('#'));

    private static EdisonLaunchArgs RoundTrip(EdisonLaunchArgs launch) =>
        EdisonLaunchArgs.TryParse(ProcessArgv.Split(launch.Format()))!;

    // --- sdílená fixture ---

    [Fact]
    public void Format_FixtureRecord_MatchesSharedFixture()
    {
        Assert.Equal(FixtureArgv(), FixtureRecord().Format());
    }

    [Fact]
    public void TryParse_SharedFixtureThroughProcessArgv_MapsEveryField()
    {
        var args = ProcessArgv.Split(FixtureArgv());

        Assert.Equal(EdisonLaunchArgs.Field.Count, args.Length);
        Assert.Equal(FixtureRecord(), EdisonLaunchArgs.TryParse(args));
    }

    // --- round-trip přes argv hosta ---

    [Fact]
    public void RoundTrip_QuoteInFullName_KeepsFollowingFieldsInPlace()
    {
        // Uvozovka ve jménu ze serveru by dřív posunula Mlp, formu i shell GUID o pozici.
        var launch = FixtureRecord() with { FullName = "Jan \"Boss\" Novák" };

        var parsed = RoundTrip(launch);

        Assert.Equal(launch with { FullName = "Jan Boss Novák" }, parsed);
    }

    [Fact]
    public void RoundTrip_TrailingBackslashInDirectory_KeepsFollowingFieldsInPlace()
    {
        // "C:\dir\" čte parser jako escapovanou uvozovku a slil by adresář s e-mailem.
        var launch = FixtureRecord() with { UserDirectory = @"C:\Mline\EdisonData\" };

        var parsed = RoundTrip(launch);

        Assert.Equal(launch with { UserDirectory = @"C:\Mline\EdisonData" }, parsed);
    }

    public static TheoryData<string> TextFields()
    {
        var data = new TheoryData<string>();
        foreach (var p in typeof(EdisonLaunchArgs).GetProperties().Where(p => p.PropertyType == typeof(string)))
            data.Add(p.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public void RoundTrip_QuoteAndTrailingBackslashInAnyTextField_KeepOtherFieldsInPlace(string field)
    {
        // Sanitizaci musí mít každé textové pole, i to, které kontrakt získá příště (proto výčet reflexí).
        var property = typeof(EdisonLaunchArgs).GetProperty(field)!;
        var launch = FixtureRecord();
        property.SetValue(launch, "1\"2\\");
        var expected = FixtureRecord();
        property.SetValue(expected, "12");

        Assert.Equal(expected, RoundTrip(launch));
    }

    [Fact]
    public void RoundTrip_EmptyOptionalFields_StayEmpty()
    {
        var launch = new EdisonLaunchArgs("srv", 57770, "NS", "IDFA", "login", "Jméno", "1", "F", "", "");

        Assert.Equal(launch, RoundTrip(launch));
    }

    // --- pravidla formátu ---

    [Fact]
    public void Format_QuotesEveryFieldAfterBareMarker()
    {
        var args = new EdisonLaunchArgs("srv", 57770, "NS", "IDFA", "login", "Jméno", "1", "F", "G", "").Format();

        Assert.Equal(
            "EDISONMAIN2 \"srv\" \"57770\" \"NS\" \"IDFA\" \"login\" \"Jméno\" \"1\" \"F\" \"G\" \"\" \"\" \"\" \"\" \"0\" \"0\" \"\" \"\" \"\" \"\" \"\" \"\"",
            args);
    }

    [Theory]
    [InlineData("2.001", "2.001")]
    [InlineData("1.003 beta", "1.003")]
    [InlineData("2\" \"x", "2")]
    [InlineData("", "")]
    public void Format_LicenceVersion_KeepsOnlyDigitsAndDots(string version, string expected)
    {
        // Verze jde do legacy Val() hosta.
        var parsed = RoundTrip(FixtureRecord() with { LicenceVersion = version });

        Assert.Equal(expected, parsed.LicenceVersion);
    }

    [Theory]
    [InlineData("31.12.2026 0:00:00", "31.12.2026 0:00:00")]   // mezera v uvozovkách pole nerozdělí
    [InlineData("31.12.\"2026", "31.12.2026")]
    [InlineData("31.12.2026\\", "31.12.2026")]
    public void Format_LicenceValidTo_KeepsFieldBoundaries(string validTo, string expected)
    {
        var parsed = RoundTrip(FixtureRecord() with { LicenceValidTo = validTo });

        Assert.Equal(expected, parsed.LicenceValidTo);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Format_UserFlags_AreIndependent(bool isAdmin, bool mayUpdate)
    {
        // Admin neimplikuje právo update - host bez pole defaultuje na false.
        var parsed = RoundTrip(FixtureRecord() with { IsAdmin = isAdmin, MayUpdate = mayUpdate });

        Assert.Equal((isAdmin, mayUpdate), (parsed.IsAdmin, parsed.MayUpdate));
    }

    // --- TryParse ---

    [Fact]
    public void TryParse_ValidArgs_MapsAllFields()
    {
        var launch = EdisonLaunchArgs.TryParse(ValidArgs());

        Assert.NotNull(launch);
        Assert.Equal("192.168.16.1", launch.Server);
        Assert.Equal(57770, launch.RestPort);
        Assert.Equal("CB_DP", launch.Namespace);
        Assert.Equal("CB_DP", launch.Idfa);
        Assert.Equal("jan.novak", launch.LoginName);
        Assert.Equal("Jan Novák", launch.FullName);
        Assert.Equal("167", launch.Mlp);
        Assert.Equal("ALVA.TISKJR.formTISKJRSeznamSestav", launch.FormName);
        Assert.Equal("guid-123", launch.ShellGuid);
        Assert.Equal("", launch.InputParam);
    }

    [Fact]
    public void TryParse_OlderShellWithoutTrailingFields_DefaultsThem()
    {
        // Starší shell posílá jen [1]-[10]: pole hosta zůstanou prázdná, flagy false.
        var launch = EdisonLaunchArgs.TryParse(ValidArgs("AdminMode=1"));

        Assert.NotNull(launch);
        Assert.Equal("AdminMode=1", launch.InputParam);
        Assert.Equal("", launch.AgendaPath);
        Assert.False(launch.IsAdmin);
        Assert.Equal("", launch.LicenceValidTo);
        Assert.Equal("", launch.ShellMonitor);
    }

    [Fact]
    public void RoundTrip_ShellMonitor_KeepsLeadingBackslashes()
    {
        // Jméno zařízení začíná zpětnými lomítky; před uvozovkou by je argv parser zdvojil či spolkl.
        var parsed = RoundTrip(FixtureRecord() with { ShellMonitor = @"\\.\DISPLAY12" });

        Assert.Equal(@"\\.\DISPLAY12", parsed.ShellMonitor);
    }

    [Fact]
    public void TryParse_MissingLaunchMarker_ReturnsNull()
    {
        var args = ValidArgs();
        args[0] = "NECO_JINEHO";

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void TryParse_EmptyArgs_ReturnsNull()
    {
        Assert.Null(EdisonLaunchArgs.TryParse([]));
    }

    [Fact]
    public void TryParse_TooFewArgs_ReturnsNull()
    {
        // LaunchMarker + 8 hodnot = 9 argů - kontrakt vyžaduje minimálně 10.
        var args = ValidArgs()[..9];

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParse_InvalidPort_ReturnsNull(string port)
    {
        var args = ValidArgs();
        args[2] = port;

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    public void TryParse_InvalidMlp_ReturnsNull(string mlp)
    {
        var args = ValidArgs();
        args[7] = mlp;

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void TryParse_MlpWithLeadingZeros_Normalized()
    {
        // Mlp jde přes int round-trip (parse → ToString), takže "0167" se normalizuje na "167"
        // - a v této podobě se posílá dál do launcher argů.
        var args = ValidArgs();
        args[7] = "0167";

        var launch = EdisonLaunchArgs.TryParse(args);

        Assert.NotNull(launch);
        Assert.Equal("167", launch.Mlp);
    }

    [Fact]
    public void TryParse_EmptyServer_ReturnsNull()
    {
        var args = ValidArgs();
        args[1] = "  ";

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void IsEdisonLaunch_DetectsLaunchMarkerEvenWhenArgsIncomplete()
    {
        // Rozliší "spuštěno ze shellu s vadnými argy" (→ varování) od čistého standalone.
        Assert.True(EdisonLaunchArgs.IsEdisonLaunch(["EDISONMAIN2"]));
        Assert.False(EdisonLaunchArgs.IsEdisonLaunch([]));
        Assert.False(EdisonLaunchArgs.IsEdisonLaunch(["TemplateDesigner"]));
    }
}
