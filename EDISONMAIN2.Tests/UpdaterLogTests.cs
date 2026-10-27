using System.IO;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Výsledek posledního běhu legacy EDUPDATE z jeho logu. Fixture jsou logy skutečných běhů (10/2026) nového
/// buildu s řádkem <c>Konec [ExitCode=N]</c> (current-*) i buildu u zákazníků bez něj (legacy-*), cesty neutrální.
/// </summary>
public class UpdaterLogTests
{
    private static UpdaterRun Fixture(string name) =>
        UpdaterLog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "updater-logs", name + ".txt")));

    [Theory]
    [InlineData("current-success", "Succeeded", 0)]
    [InlineData("current-not-needed", "NotNeeded", 0)]
    [InlineData("current-parameters-failed", "Failed", 1)]
    [InlineData("current-backup-failed", "Failed", 1)]
    [InlineData("current-file-list-failed", "Failed", 1)]
    [InlineData("current-restored", "Failed", 100)]
    [InlineData("current-restore-failed", "Failed", 200)]
    [InlineData("legacy-success", "Succeeded", null)]
    [InlineData("legacy-not-needed", "NotNeeded", null)]
    [InlineData("legacy-restored", "Failed", 100)]
    [InlineData("legacy-restored-unc", "Failed", 100)]
    public void Fixture_OutcomeAndExitCode(string name, string outcome, int? exitCode)
    {
        var run = Fixture(name);

        Assert.Equal(Enum.Parse<UpdaterRunOutcome>(outcome), run.Outcome);
        Assert.Equal(exitCode, run.ExitCode);
    }

    [Fact]
    public void LegacyParametersFailed_RunEndsAfterParameters_IsFailed()
    {
        // Starý build chybu parametrů do logu nezapíše, běh skončí hned za řádky Parametr.
        var run = Fixture("legacy-parameters-failed");

        Assert.Equal(UpdaterRunOutcome.Failed, run.Outcome);
        Assert.Equal(1, run.ExitCode);
        Assert.Equal(UpdaterLog.LegacyParametersFailed, run.FailureMessage);
    }

    [Fact]
    public void RunWithParameterCheckButNoOutcome_IsUnknown()
    {
        // Updater po kontrole parametrů čeká na zavření shellu, rozepsaný běh selhání není.
        var run = UpdaterLog.Parse(
            "[21.10.2026 09:00:00] Parametr: UpdateType=Auto\r\n" +
            "[21.10.2026 09:00:00] KontrolaParametru: OK\r\n");

        Assert.Equal(UpdaterRunOutcome.Unknown, run.Outcome);
    }

    [Fact]
    public void CurrentRestored_ReadsReasonParametersAndTimes()
    {
        var run = Fixture("current-restored");

        Assert.StartsWith(@"InstallEdisonFiles.CopyCoreFile: \\SERVER\EDISON\B.dll nelze zkopirovat. (The process cannot access", run.FailureMessage);
        Assert.True(run.Restored);
        Assert.Equal(@"\\SERVER\EDISON", run.SourcePath);
        Assert.Equal(@"C:\EDISON", run.TargetPath);
        Assert.Equal("9.999", run.Version);
        Assert.False(run.Forced);
        Assert.Equal(new DateTime(2026, 10, 26, 12, 39, 35), run.FinishedAt);
        Assert.Equal("26.10.2026 12:39:35", run.RunKey);
        Assert.EndsWith("UPDATER: Konec [ExitCode=100]", run.Tail[^1]);
    }

    [Fact]
    public void CurrentRestoreFailed_RestoredFalse()
    {
        var run = Fixture("current-restore-failed");

        Assert.False(run.Restored);
        Assert.Contains("Z.dll nelze zkopirovat", run.FailureMessage);
    }

    [Fact]
    public void CurrentParametersFailed_ReasonFromParameterCheck()
    {
        Assert.Contains("Neexistující adresář", Fixture("current-parameters-failed").FailureMessage);
    }

    [Fact]
    public void LegacyRestoredUnc_ReasonWithoutCause()
    {
        // Starý build důvod kopie zahodí, zůstane jen „nelze zkopirovat".
        Assert.EndsWith("B.dll nelze zkopirovat.", Fixture("legacy-restored-unc").FailureMessage);
    }

    [Fact]
    public void TwoRuns_OnlyLastCounts()
    {
        // Nepovedené smazání logu nechá nový běh připsat za starý.
        const string text =
            "[20.10.2026 08:00:00] Parametr: UpdateType=Auto\r\n" +
            "[20.10.2026 08:00:05] UPDATER: Chyba při aktualizaci [stará chyba]\r\n" +
            "[21.10.2026 09:00:00] Parametr: UpdateType=Auto\r\n" +
            "[21.10.2026 09:00:09] UPDATER: Aktualizace proběhla úspěšně\r\n";

        var run = UpdaterLog.Parse(text);

        Assert.Equal(UpdaterRunOutcome.Succeeded, run.Outcome);
        Assert.Equal("21.10.2026 09:00:00", run.RunKey);
    }

    [Fact]
    public void ContinuationLine_BelongsToPreviousRecord()
    {
        const string text =
            "[21.10.2026 09:00:00] Parametr: UpdateType=Auto\r\n" +
            "[21.10.2026 09:00:01] RestoreFiles: OK\r\n" +
            "[21.10.2026 09:00:02] UPDATER: Chyba při aktualizaci [první řádek\r\n" +
            "druhý řádek]\r\n" +
            "[21.10.2026 09:00:03] ClearTempData: OK\r\n";

        var run = UpdaterLog.Parse(text);

        Assert.Equal(UpdaterRunOutcome.Failed, run.Outcome);
        Assert.Equal("první řádek druhý řádek", run.FailureMessage);
        Assert.Equal(100, run.ExitCode);
    }

    [Fact]
    public void ForcedRun_ReadsFlag()
    {
        var run = UpdaterLog.Parse(
            "[21.10.2026 09:00:00] Parametr: UpdateType=Auto\r\n" +
            "[21.10.2026 09:00:00] Parametr: Nucenyupdate=True\r\n" +
            "[21.10.2026 09:00:01] UPDATER: Aktualizace proběhla úspěšně\r\n");

        Assert.True(run.Forced);
    }

    [Fact]
    public void EmptyOrForeignText_IsUnknown()
    {
        Assert.Equal(UpdaterRunOutcome.Unknown, UpdaterLog.Parse("").Outcome);
        Assert.Equal(UpdaterRunOutcome.Unknown, UpdaterLog.Parse("nějaký jiný soubor").Outcome);
    }

    [Fact]
    public void Tail_KeepsLastLines()
    {
        var run = UpdaterLog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "updater-logs", "current-success.txt")), tailLines: 3);

        Assert.Equal(3, run.Tail.Count);
        Assert.EndsWith("UPDATER: Konec [ExitCode=0]", run.Tail[^1]);
    }
}
