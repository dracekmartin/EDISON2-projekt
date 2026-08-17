using System.Diagnostics;
using System.IO;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Testy `FormatLegacyLabel` — fallback chain (MainWindowTitle → FormFqn last segment → ExeName).
/// MainWindowTitle nelze v unit-testu spolehlivě nastavit (Process je sealed-ish, vlastní proces
/// nemáme), takže testujeme jen scénář prázdného titulu skrz aktuální `Process.GetCurrentProcess()`
/// — ten v xUnit kontextu obvykle MainWindowTitle nemá (testhost.exe bez okna).
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

        // Path.GetFileNameWithoutExtension odstraní .exe.
        Assert.Equal("MyApp", label);
    }

    [Fact]
    public void FormatLegacyLabel_FqnEndingWithDot_FallsBackToWholeFqn()
    {
        // Edge case: FQN končí tečkou (validní edge — last segment by byl prázdný).
        var entry = MakeEntry("ALVA.");

        var label = WindowNavigator.FormatLegacyLabel(entry);

        Assert.Equal("ALVA.", label);
    }

    // ---------- MatchesTarget (dedup „už běžícího" modulu) ----------

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
        // Forma je FQN Caché třídy — case-sensitive.
        var entry = MakeEntry("ALVA.USERS.formUSERSLocks", "USERS.exe");

        Assert.False(WindowNavigator.MatchesTarget(entry, "USERS.exe", "alva.users.formuserslocks", ""));
    }

    // ---------- LaunchesDirectly ----------

    [Theory]
    [InlineData("EDISONMAIN.exe", true)]
    [InlineData(@"C:\Edison\EDISONMAIN.exe", true)]
    [InlineData("edisonmain.exe", true)]
    [InlineData("USERS.exe", false)]
    [InlineData("MHD2PlanDopravy.exe", false)]
    public void LaunchesDirectly_OnlyEdisonMain(string exeName, bool expected)
    {
        Assert.Equal(expected, WindowNavigator.LaunchesDirectly(exeName));
    }

    // ---------- BuildLauncherArguments ----------

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
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001");

        // Prázdný inputParam zůstává jako "" na pozici [10] — pořadí polí se neposune.
        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_EmbeddedQuote_NotEscaped()
    {
        // Známý limit wire formátu: uvozovka uvnitř hodnoty se needukuje ani neescapuje.
        // Legacy parser kontrakt je zmrazený — produkci NEměnit.
        var m = MakeMlRest();
        m.FullName.Returns("Jan \"Boss\" Novák");

        var args = WindowNavigator.BuildLauncherArguments(
            m, "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001");

        Assert.Contains("\"Jan \"Boss\" Novák\"", args);
    }

    [Fact]
    public void BuildLauncherArguments_OrderingMatchesContract()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "AdminMode=1", @"C:\Edison\X.exe", "26001,6001");

        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"AdminMode=1\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_ShortcutPayload_IsLastField()
    {
        var args = WindowNavigator.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", "UEFZTE9BRA==");

        // Zástupce je [13] - až za licencovanými elementy, aby starší host kontrakt jen ignoroval.
        Assert.EndsWith("\"26001,6001\" \"UEFZTE9BRA==\"", args);
    }

    // Vlastnictví oken (IsInternalWindow / ShouldClose) → WindowOwnershipTests.

    // ---------- ResolveExecutablePath ----------

    [Fact]
    public void ResolveExecutablePath_FileExistsLocally_ReturnsLocalPath()
    {
        // Test dll je v AppContext.BaseDirectory — File.Exists hit, vrátí local path.
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
