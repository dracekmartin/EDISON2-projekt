using System.Diagnostics;
using System.IO;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>
/// Testy `FormatLegacyLabel` — fallback chain (MainWindowTitle → FormFqn last segment → ExeName).
/// MainWindowTitle nelze v unit-testu spolehlivě nastavit (Process je sealed-ish, vlastní proces
/// nemáme), takže testujeme jen scénář prázdného titulu skrz aktuální `Process.GetCurrentProcess()`
/// — ten v xUnit kontextu obvykle MainWindowTitle nemá (testhost.exe bez okna).
/// </summary>
public class WindowNavigatorTests
{
    private static WindowNavigator.LegacyProcessEntry MakeEntry(string formFqn, string exeName = "USERS.exe") =>
        new(Process.GetCurrentProcess(), exeName, formFqn);

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

    // ---------- IsInternalNamespace ----------

    [Fact]
    public void IsInternalNamespace_MsPrefix_True()
    {
        Assert.True(WindowNavigator.IsInternalNamespace("MS.Win32"));
    }

    [Fact]
    public void IsInternalNamespace_SystemPrefix_True()
    {
        Assert.True(WindowNavigator.IsInternalNamespace("System.Windows"));
    }

    [Fact]
    public void IsInternalNamespace_MicrosoftPrefix_True()
    {
        // WebView2 + jiné Microsoft tooling pluginy.
        Assert.True(WindowNavigator.IsInternalNamespace("Microsoft.Web.WebView2.Wpf"));
    }

    [Fact]
    public void IsInternalNamespace_UserApp_False()
    {
        Assert.False(WindowNavigator.IsInternalNamespace("EDISONMAIN2.Windows.EdisonMainWindow"));
    }

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
