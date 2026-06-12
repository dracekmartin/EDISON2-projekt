using EDControls;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class UpdaterTests
{
    private class TestableUpdater : Updater
    {
        public string LocalVersion { get; set; } = "";
        public string SourceVersion { get; set; } = "";
        public string? SourceDirSeen { get; private set; }

        protected override string GetLocalVersion() => LocalVersion;

        protected override string GetSourceVersion(string sourceDir)
        {
            SourceDirSeen = sourceDir;
            return SourceVersion;
        }
    }

    private static IMLRest MockSettings(params string[] rows)
    {
        var mock = MLRestMocks.Create();
        mock.Delimiter.Returns('~');
        mock.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output(rows));
        return mock;
    }

    // ---------- LoadSettings ----------

    [Fact]
    public void LoadSettings_AllFields_ParsedCorrectly()
    {
        var mlRest = MockSettings(
            "update~1~0",
            "version~20260526.1.0",
            "edisonPath~\\\\server\\edison",
            "localSourcePath~\\\\share\\local");

        var settings = new Updater().LoadSettings(mlRest);

        Assert.True(settings.Enabled);
        Assert.Equal("20260526.1.0", settings.CacheVersion);
        Assert.Equal("\\\\server\\edison", settings.EdisonPath);
        Assert.Equal("\\\\share\\local", settings.LocalSourcePath);
    }

    [Fact]
    public void LoadSettings_UpdateDisabled_EnabledFalse()
    {
        var settings = new Updater().LoadSettings(MockSettings("update~0~1"));

        Assert.False(settings.Enabled);
    }

    [Fact]
    public void LoadSettings_UnknownAndMalformedRows_Ignored()
    {
        var settings = new Updater().LoadSettings(MockSettings(
            "update~1~0",
            "pending~",
            "garbage-no-delimiter",
            "sourceServer~release.m-line.cz",
            "version~2.0"));

        Assert.True(settings.Enabled);
        Assert.Equal("2.0", settings.CacheVersion);
    }

    [Fact]
    public void SourceDir_LocalSourcePathPreferred_FallbackEdisonPath()
    {
        Assert.Equal(@"\\share", new UpdateSettings(true, "", @"\\edison", @"\\share").SourceDir);
        Assert.Equal(@"\\edison", new UpdateSettings(true, "", @"\\edison", "").SourceDir);
    }

    // ---------- Check — decision matrix ----------

    [Fact]
    public void Check_Disabled_None()
    {
        // Divergence od legacy ForceYes: Enabled=false znamená skip i na stanici (dev prostředí).
        var updater = new TestableUpdater { LocalVersion = "1.0", SourceVersion = "2.0" };

        var result = updater.Check(new UpdateSettings(false, "9.9", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_DevVersion_None_EvenWithCacheMismatch()
    {
        // Dev guard: 999999.9 nesmí spadnout ani do update (zdroj "novější") ani do Broken (Caché mismatch).
        var updater = new TestableUpdater { LocalVersion = VerzeInf.DevVersion, SourceVersion = "260612.1" };

        var result = updater.Check(new UpdateSettings(true, "260612.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_MissingLocalVersion_Broken()
    {
        var updater = new TestableUpdater { LocalVersion = "", SourceVersion = "2.0" };

        var result = updater.Check(new UpdateSettings(true, "", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("VERZE.INF", result.Message);
    }

    [Fact]
    public void Check_UnreadableSourceVersion_Broken()
    {
        var updater = new TestableUpdater { LocalVersion = "1.0", SourceVersion = "" };

        var result = updater.Check(new UpdateSettings(true, "", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains(@"\\edison", result.Message);
    }

    [Fact]
    public void Check_NewerSourceVersion_UpdateRequired()
    {
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1" };

        var result = updater.Check(new UpdateSettings(true, "", @"\\edison", @"\\share"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.Equal("20260601.1", result.Version);
        Assert.Equal(@"\\share", updater.SourceDirSeen);
    }

    [Fact]
    public void Check_SameVersions_None()
    {
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings(true, "20260526.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_CacheMismatchWithoutNewerSource_Broken()
    {
        // Server instalace == lokál, ale Caché verze jiná → klient to updatem nespraví.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings(true, "20260601.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("20260526.1 -> 20260601.1", result.Message);
    }

    [Fact]
    public void Check_CacheMismatchButNewerSource_UpdateWins()
    {
        // Nová verze na zdroji řeší i Caché mismatch — update má přednost před Broken.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1" };

        var result = updater.Check(new UpdateSettings(true, "20260601.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
    }

    [Fact]
    public void Check_EmptyCacheVersion_NoMismatchCheck()
    {
        // Dev server má version~ prázdné — mismatch check se přeskakuje.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings(true, "", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }
}
