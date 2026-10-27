using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using EDControls.ErrorReporting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Neúspěšný běh legacy updateru stanice jde do telemetrie jednou: shell si pamatuje nahlášený běh per instalace,
/// starý log nehlásí a start na hlášení nesmí stát. Soubory a odeslání jsou v paměti.
/// </summary>
public class UpdaterFailureReportTests
{
    private const string InstallDir = @"C:\EDISON";

    private sealed record Sent(ErrorReportType Type, string Module, string Name, string Message, JsonObject Details);

    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Sent> _sent = [];
    private readonly TransientLocalPrefs _prefs = new();
    private DateTime _now = new(2026, 10, 27, 8, 0, 0);

    private static string FixtureText(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "updater-logs", name + ".txt"));

    private void Log(string fixture, string dir = InstallDir) => _files[Path.Combine(dir, UpdaterLog.FileName)] = FixtureText(fixture);

    private void PreviousLog(string fixture) => _files[Path.Combine(InstallDir, UpdaterLog.PreviousFileName)] = FixtureText(fixture);

    private UpdaterFailureReport Report(string dir = InstallDir, Action? sendThrows = null) => new(
        _prefs, dir, path => _files.GetValueOrDefault(path),
        (type, module, name, message, details) =>
        {
            sendThrows?.Invoke();
            _sent.Add(new Sent(type, module, name, message, details));
        },
        () => _now);

    [Fact]
    public void Failure_SendsOneEventAndRemembersIt()
    {
        Log("current-restored");

        Assert.True(Report().ReportIfNew());

        var sent = Assert.Single(_sent);
        Assert.Equal(ErrorReportType.Warning, sent.Type);
        Assert.Equal(UpdaterFailureReport.Module, sent.Module);
        Assert.Equal(UpdaterFailureReport.Name, sent.Name);
        Assert.StartsWith("Aktualizace stanice selhala: InstallEdisonFiles.CopyCoreFile:", sent.Message);
        Assert.Equal(100, sent.Details["exitCode"]!.GetValue<int>());
        Assert.Equal(@"\\SERVER\EDISON", sent.Details["sourcePath"]!.GetValue<string>());
        Assert.Contains("Konec [ExitCode=100]", sent.Details["logTail"]!.GetValue<string>());
        Assert.NotNull(_prefs.ReportedUpdaterFailures);
    }

    [Fact]
    public void SameFailure_NextStart_SendsNothing()
    {
        Log("current-restored");
        Report().ReportIfNew();

        Assert.False(Report().ReportIfNew());
        Assert.Single(_sent);
    }

    [Fact]
    public void NewerFailure_IsReported()
    {
        Log("current-restored");
        Report().ReportIfNew();
        PreviousLog("current-restored");
        Log("current-restore-failed");

        Assert.True(Report().ReportIfNew());
        Assert.Equal(2, _sent.Count);
    }

    [Theory]
    [InlineData("current-success")]
    [InlineData("current-not-needed")]
    [InlineData("legacy-success")]
    public void NotFailed_SendsNothing(string fixture)
    {
        Log(fixture);

        Assert.False(Report().ReportIfNew());
        Assert.Empty(_sent);
    }

    [Fact]
    public void MissingLog_SendsNothing()
    {
        Assert.False(Report().ReportIfNew());
        Assert.Empty(_sent);
    }

    [Fact]
    public void FailureOlderThanMaxAge_SendsNothing()
    {
        Log("current-restored");
        _now = new DateTime(2026, 10, 26) + UpdaterFailureReport.MaxAge + TimeSpan.FromDays(1);

        Assert.False(Report().ReportIfNew());
    }

    [Fact]
    public void RestoreFailed_IsError()
    {
        Log("current-restore-failed");

        Report().ReportIfNew();

        Assert.Equal(ErrorReportType.Error, Assert.Single(_sent).Type);
    }

    [Fact]
    public void PreviousRunFailedToo_IsError()
    {
        // Druhé selhání po sobě už nebývá přechodné.
        Log("current-restored");
        PreviousLog("legacy-restored");

        Report().ReportIfNew();

        var sent = Assert.Single(_sent);
        Assert.Equal(ErrorReportType.Error, sent.Type);
        Assert.True(sent.Details["previousRunFailed"]!.GetValue<bool>());
    }

    [Fact]
    public void TwoInstallations_DedupeIndependently()
    {
        Log("current-restored");
        Log("current-restored", @"D:\EDISON");

        Assert.True(Report().ReportIfNew());
        Assert.True(Report(@"D:\EDISON").ReportIfNew());
    }

    [Fact]
    public void Module_IsNotInstallEvent()
    {
        // module="EDISON" + name="Instalace verze" by firmě přepsalo verzi legacy EDISONu.
        Assert.False(UpdaterFailureReport.Module == "EDISON" && UpdaterFailureReport.Name == "Instalace verze");
    }

    [Fact]
    public void SendThrows_DoesNotBreakStartup()
    {
        Log("current-restored");

        Assert.False(Report(sendThrows: () => throw new InvalidOperationException("síť")).ReportIfNew());
    }
}
