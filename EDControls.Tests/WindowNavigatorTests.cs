using System.Diagnostics;
using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Fallback chain <c>FormatLegacyLabel</c> (MainWindowTitle → FormFqn → ExeName). MainWindowTitle
/// nejde v unit-testu spolehlivě nastavit, takže se ověřuje jen prázdná větev (testhost.exe okno nemá).
/// </summary>
public class WindowNavigatorTests
{
    private static WindowNavigator.LegacyProcessEntry MakeEntry(string formFqn, string exeName = "USERS.exe", string inputParam = "") =>
        new(Process.GetCurrentProcess(), exeName, formFqn, inputParam);

    [Fact]
    public void FormatLegacyLabel_NoTitle_ReturnsLastFqnSegment()
    {
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks");

        var label = WindowNavigator.FormatLegacyLabel(entry);

        // testhost.exe nemá MainWindowTitle → fallback na poslední segment FormFqn.
        Assert.Equal("formUSERSLocks", label);
    }

    [Fact]
    public void FormatLegacyLabel_FqnWithoutDots_ReturnsWholeFqn()
    {
        var entry = MakeEntry("formStandalone");

        var label = WindowNavigator.FormatLegacyLabel(entry);

        Assert.Equal("formStandalone", label);
    }

    [Fact]
    public void FormatLegacyLabel_EmptyFqn_FallsBackToExeName()
    {
        var entry = MakeEntry(formFqn: "", exeName: "MyApp.exe");

        var label = WindowNavigator.FormatLegacyLabel(entry);

        Assert.Equal("MyApp", label);
    }

    [Fact]
    public void FormatLegacyLabel_FqnEndingWithDot_FallsBackToWholeFqn()
    {
        // Edge case: FQN končí tečkou (validní edge - last segment by byl prázdný).
        var entry = MakeEntry("ALVA.");

        var label = WindowNavigator.FormatLegacyLabel(entry);

        Assert.Equal("ALVA.", label);
    }

    [Fact]
    public void MatchesTarget_SameExeFormParam_True()
    {
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe", "AdminMode=1");

        Assert.True(WindowNavigator.MatchesTarget(entry, "USERS.exe", "ALVA.USERS.formUSERSLocks", "AdminMode=1"));
    }

    [Fact]
    public void MatchesTarget_ExeCaseInsensitive_True()
    {
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe");

        Assert.True(WindowNavigator.MatchesTarget(entry, "users.exe", "ALVA.USERS.formUSERSLocks", ""));
    }

    [Fact]
    public void MatchesTarget_DifferentInputParam_False()
    {
        // Stejná forma v jiném módu = jiné okno.
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe", "AdminMode=1");

        Assert.False(WindowNavigator.MatchesTarget(entry, "USERS.exe", "ALVA.USERS.formUSERSLocks", ""));
    }

    [Fact]
    public void MatchesTarget_DifferentForm_False()
    {
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe");

        Assert.False(WindowNavigator.MatchesTarget(entry, "USERS.exe", "ALVA.USERS.formUSERSActivity", ""));
    }

    [Fact]
    public void MatchesTarget_FormCaseSensitive_False()
    {
        // Forma je FQN Caché třídy - case-sensitive.
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe");

        Assert.False(WindowNavigator.MatchesTarget(entry, "USERS.exe", "alva.users.formuserslocks", ""));
    }

    [Theory]
    [InlineData("EDISONMAIN.exe", true)]
    [InlineData(@"C:\Edison\EDISONMAIN.exe", true)]
    [InlineData("edisonmain.exe", true)]
    [InlineData("TemplateDesigner.exe", true)]
    [InlineData(@"C:\Edison\TemplateDesigner.exe", true)]
    [InlineData("USERS.exe", false)]
    [InlineData("MHD2PlanDopravy.exe", false)]
    public void LaunchesDirectly_EdisonMainAndTemplateDesigner(string exeName, bool expected)
    {
        Assert.Equal(expected, WindowNavigator.LaunchesDirectly(exeName));
    }

    private static IMLRest MakeMlRest()
    {
        var m = Substitute.For<IMLRest>();
        m.Server.Returns("192.168.16.1");
        m.Port.Returns(57770);
        m.DataNamespace.Returns("MLINE");
        m.Idfa.Returns("CB_DP");
        m.LoginName.Returns("jan.novak");
        m.FullName.Returns("Jan Novák");
        m.Mlp.Returns("157");
        m.Guid.Returns("SHELLGUID");
        return m;
    }

    [Fact]
    public void BuildLauncherArguments_EmptyInputParam_StillOccupiesPositionalSlot()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin: false, mayUpdate: false);

        // Prázdný inputParam zůstává jako "" na pozici [10] - pořadí polí se neposune.
        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\" \"0\" \"0\" \"\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_EmbeddedQuote_NotEscaped()
    {
        // Známý limit wire formátu: uvozovka uvnitř hodnoty se needukuje ani neescapuje.
        // Legacy parser kontrakt je zmrazený - produkci NEměnit.
        var m = MakeMlRest();
        m.FullName.Returns("Jan \"Boss\" Novák");

        var args = WindowNavigator.BuildLauncherArguments(
            m, "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin: false, mayUpdate: false);

        Assert.Contains("\"Jan \"Boss\" Novák\"", args);
    }

    [Fact]
    public void BuildLauncherArguments_OrderingMatchesContract()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "AdminMode=1", @"C:\Edison\X.exe", "26001,6001", isAdmin: true, mayUpdate: true);

        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"AdminMode=1\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\" \"1\" \"1\" \"\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_ShortcutPayload_FollowsLicensedElements()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", false, false, "UEFZTE9BRA==");

        // Zástupce je [13] - až za licencovanými elementy, aby starší host kontrakt jen ignoroval.
        Assert.EndsWith("\"26001,6001\" \"UEFZTE9BRA==\" \"0\" \"0\" \"\" \"\"", args);
    }

    [Theory]
    [InlineData(true, false, "\"1\" \"0\"")]
    [InlineData(false, true, "\"0\" \"1\"")]
    public void BuildLauncherArguments_UserFlags_FollowShortcutPayload(bool isAdmin, bool mayUpdate, string expectedTail)
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin, mayUpdate);

        // Admin [14] a právo update [15] - host bez nich defaultuje na false, hodnota musí být
        // explicitní a nezávislá (admin neimplikuje update). Za nimi prázdný adresář a e-mail [16]/[17].
        Assert.EndsWith($"\"\" {expectedTail} \"\" \"\"", args);
        Assert.Equal(18, args.Split(" \"").Length);
    }

    [Fact]
    public void BuildLauncherArguments_UserDirectoryAndEmail_AreLastFields()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", false, false,
            userDirectory: @"C:\Mline\EdisonData", userEmail: "jan.novak@firma.cz");

        // [16] osobní adresář a [17] e-mail → MLProps.UserDirectory / UserEmail; host bez polí dá "".
        Assert.EndsWith("\"0\" \"0\" \"C:\\Mline\\EdisonData\" \"jan.novak@firma.cz\"", args);
        Assert.Equal(18, args.Split(" \"").Length);
    }

    [Fact]
    public void BuildLauncherArguments_UserDirectory_TrailingBackslashTrimmed()
    {
        // "C:\dir\" by argv parser hosta četl \" jako escapovanou uvozovku a slil pole [16] s [17].
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "", false, false,
            userDirectory: @"C:\Mline\EdisonData\");

        Assert.EndsWith("\"C:\\Mline\\EdisonData\" \"\"", args);
    }

    [Theory]
    [InlineData(0, false, false)]            // normální konec i Application.Exit
    [InlineData(0, true, false)]
    [InlineData(-1, true, false)]            // náš Process.Kill
    [InlineData(-1, false, true)]            // kill zvenku
    [InlineData(1, false, true)]             // taskkill /F
    [InlineData(2, false, true)]             // host: LaunchFailed
    [InlineData(3, false, true)]             // host: FatalReported
    [InlineData(-532462766, false, true)]    // 0xE0434352 neošetřená CLR výjimka
    [InlineData(-1073741819, false, true)]   // 0xC0000005 access violation
    public void ShouldReportExit_NonZeroCodeNotKilledByShell(int exitCode, bool killedByShell, bool expected)
    {
        Assert.Equal(expected, WindowNavigator.ShouldReportExit(exitCode, killedByShell));
    }

    // Vlastnictví oken (IsInternalWindow / ShouldClose) → WindowOwnershipTests.

    [Fact]
    public void ResolveExecutablePath_FileExistsLocally_ReturnsLocalPath()
    {
        // Test dll je v AppContext.BaseDirectory - File.Exists hit, vrátí local path.
        var existing = Path.GetFileName(typeof(WindowNavigator).Assembly.Location);

        var result = WindowNavigator.ResolveExecutablePath(existing);

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, existing), result);
    }

    [Fact]
    public void ResolveExecutablePath_FileNotExists_FallsBackToEdisonDeployRoot()
    {
        var nonExistent = $"{Guid.NewGuid()}.exe";

        var result = WindowNavigator.ResolveExecutablePath(nonExistent);

        Assert.Equal(Path.Combine(WindowNavigator.EdisonDeployRoot, nonExistent), result);
    }
}
