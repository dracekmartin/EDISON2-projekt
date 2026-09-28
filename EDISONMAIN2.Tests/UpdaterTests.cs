using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
        public string StampSeen { get; set; } = "";
        public bool ServerInstallation { get; set; }
        public string? SourceDirSeen { get; private set; }

        protected override string GetLocalVersion() => LocalVersion;

        protected override bool IsServerInstallation(string sourceDir) => ServerInstallation;

        protected override string GetSourceVersion(string sourceDir)
        {
            SourceDirSeen = sourceDir;
            return SourceVersion;
        }

        protected override string GetForcedStampSeen() => StampSeen;
    }

    private static IMLRest MockSettings(params string[] rows)
    {
        var mock = MLRestMocks.Create();
        mock.Delimiter.Returns('~');
        mock.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", Arg.Any<object[]>())
            .Returns(MLRestMocks.Output(rows));
        return mock;
    }

    [Fact]
    public void LoadSettings_AllFields_ParsedCorrectly()
    {
        var mlRest = MockSettings(
            "update~1~0",
            "version~20260526.1.0",
            "edisonPath~\\\\server\\edison",
            "localSourcePath~\\\\share\\local",
            "sourceServer~release.m-line.cz",
            "sourcePort~59993");

        var settings = new Updater().LoadSettings(mlRest);

        Assert.Equal("20260526.1.0", settings.CacheVersion);
        Assert.Equal("\\\\server\\edison", settings.EdisonPath);
        Assert.Equal("\\\\share\\local", settings.LocalSourcePath);
        Assert.Equal("release.m-line.cz", settings.SourceServer);
        Assert.Equal(59993, settings.SourcePort);
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

        Assert.Equal("2.0", settings.CacheVersion);
    }

    [Fact]
    public void LoadSettings_Pending_Parsed()
    {
        Assert.True(new Updater().LoadSettings(MockSettings("pending~1")).Pending);
        Assert.False(new Updater().LoadSettings(MockSettings("pending~0")).Pending);
    }

    [Fact]
    public void Check_Pending_Broken_EvenWithoutInstalledRelease()
    {
        // Parita s EDLOADER UpdateIsRunning: během serverové instalace se klient nepřihlásí.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings("", @"\\edison", "", Pending: true));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("aktualizace systému", result.Message);
    }

    [Fact]
    public void SourceDir_LocalSourcePathPreferred_FallbackEdisonPath()
    {
        Assert.Equal(@"\\share", new UpdateSettings("", @"\\edison", @"\\share").SourceDir);
        Assert.Equal(@"\\edison", new UpdateSettings("", @"\\edison", "").SourceDir);
    }

    [Fact]
    public void Check_UpdateFlagOff_StillUpdates()
    {
        // Legacy parita: klient flag "update" ignoruje - stanice pozadu by jinak po FULL shodila verzová brána.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1" };
        var settings = updater.LoadSettings(MockSettings("update~0~1", "version~20260601.1"));

        var result = updater.Check(settings);

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
    }

    [Fact]
    public void Check_DevVersion_None_EvenWithCacheMismatch()
    {
        // Dev guard: 999999.9 nesmí spadnout ani do update (zdroj "novější") ani do Broken (Caché mismatch).
        var updater = new TestableUpdater { LocalVersion = VerzeInf.DevVersion, SourceVersion = "260612.1" };

        var result = updater.Check(new UpdateSettings("260612.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_MissingLocalVersion_Broken()
    {
        var updater = new TestableUpdater { LocalVersion = "", SourceVersion = "2.0" };

        var result = updater.Check(new UpdateSettings("2.0", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("VERZE.INF", result.Message);
    }

    [Fact]
    public void Check_UnreadableSourceVersion_Broken()
    {
        var updater = new TestableUpdater { LocalVersion = "1.0", SourceVersion = "" };

        var result = updater.Check(new UpdateSettings("1.0", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains(@"\\edison", result.Message);
    }

    [Fact]
    public void Check_NewerSourceVersion_UpdateRequired()
    {
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1" };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", @"\\share"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.Equal("20260601.1", result.Version);
        Assert.Equal(@"\\share", updater.SourceDirSeen);
    }

    [Fact]
    public void Check_SameVersions_None()
    {
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_CacheMismatchWithoutNewerSource_Broken()
    {
        // Server instalace == lokál, ale Caché verze jiná → klient to updatem nespraví.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260526.1" };

        var result = updater.Check(new UpdateSettings("20260601.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("20260526.1 -> 20260601.1", result.Message);
    }

    [Fact]
    public void Check_CacheMismatchButNewerSource_UpdateWins()
    {
        // Nová verze na zdroji řeší i Caché mismatch - update má přednost před Broken.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1" };

        var result = updater.Check(new UpdateSettings("20260601.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
    }

    [Fact]
    public void Check_NoInstalledRelease_None()
    {
        // Dev server má version~ prázdné: bez releasu se nic nenabízí ani nevynucuje, i když zdroj vypadá novější.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "20260601.1", StampSeen = "" };

        var result = updater.Check(new UpdateSettings("", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void LoadSettings_NucenyUpdatClentu_ParsedAsForcedStamp()
    {
        // Překlep v klíči je součást wire kontraktu (legacy ALVA.SETUP.Main.LoadSettings).
        var settings = new Updater().LoadSettings(MockSettings("update~1~0", "NucenyUpdatClentu~66234,60449"));

        Assert.Equal("66234,60449", settings.ForcedStamp);
    }

    [Fact]
    public void Check_ForcedStampDiffers_UpdateRequiredForced()
    {
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260526.1", StampSeen = "65995,1",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.True(result.Forced);
        Assert.Equal("20260526.1", result.Version);
    }

    [Fact]
    public void Check_ForcedStampMatches_None()
    {
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260526.1", StampSeen = "66234,2",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_ForcedStampButShellRunsFromServerInstallation_None()
    {
        // Legacy parita: shell na serveru / stanice ze share by kopíroval updater sám na sebe
        // a při každém startu skončil na výjimce.
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260526.1", StampSeen = "65995,1",
            ServerInstallation = true,
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    private static string NewInstallDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, VerzeInf.FileName), "LAST=260901.1\n");
        return dir;
    }

    [Fact]
    public void IsSameInstallation_SameDirectoryWrittenDifferently_True()
    {
        var dir = NewInstallDir();
        try
        {
            Assert.True(Updater.IsSameInstallation(dir, dir.ToUpperInvariant() + Path.DirectorySeparatorChar));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsSameInstallation_JunctionToSameDirectory_True()
    {
        // Jiná cesta k témuž adresáři, jako D:\Edison proti \\SERVER\Edison na serveru - textově
        // se neshodují, pozná se to podle ID souboru VERZE.INF.
        var dir = NewInstallDir();
        var junction = dir + "-junction";
        try
        {
            using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe")
                   {
                       ArgumentList = { "/c", "mklink", "/J", junction, dir },
                       UseShellExecute = false,
                       CreateNoWindow = true,
                   })!)
            {
                mklink.WaitForExit();
                Assert.Equal(0, mklink.ExitCode);
            }

            Assert.True(Updater.IsSameInstallation(dir, junction));
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsSameInstallation_DifferentInstallationsOrEmptySource_False()
    {
        var a = NewInstallDir();
        var b = NewInstallDir();
        try
        {
            Assert.False(Updater.IsSameInstallation(a, b));
            Assert.False(Updater.IsSameInstallation(a, ""));
            Assert.False(Updater.IsSameInstallation(a, Path.Combine(a, "neexistuje")));
        }
        finally
        {
            Directory.Delete(a, recursive: true);
            Directory.Delete(b, recursive: true);
        }
    }

    [Fact]
    public void Check_EmptyForcedStamp_NoEffect()
    {
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260526.1", StampSeen = "cokoliv",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", ""));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_DevVersion_IgnoresForcedStamp()
    {
        // Dev stanice nesmí vynucená vlna zákazníka nikdy trefit.
        var updater = new TestableUpdater
        {
            LocalVersion = VerzeInf.DevVersion, SourceVersion = "20260526.1", StampSeen = "",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_NewerVersionAndPendingForcedStamp_ForcedFullCopy()
    {
        // Diff jde podle manifestu vydání - hotfix nahraný na server po instalaci by stanici pozadu
        // vynechal a razítko by vlnu přesto označilo za splněnou.
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260601.1", StampSeen = "",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.True(result.Forced);
        Assert.Equal("20260601.1", result.Version);
    }

    [Fact]
    public void Check_NewerVersionAndSatisfiedForcedStamp_DiffUpdate()
    {
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260601.1", StampSeen = "66234,2",
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.False(result.Forced);
    }

    [Fact]
    public void Check_ForcedButSourceUnreadable_Broken()
    {
        // Nedosažitelný zdroj shodí check dřív, než by vynucená vlna spustila updater do prázdna.
        var updater = new TestableUpdater { LocalVersion = "20260526.1", SourceVersion = "", StampSeen = "" };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
    }

    [Theory]
    [InlineData(ClientUpdaterKind.LegacyEdupdate, "EDUPDATE.exe", "CliUp.ini")]
    [InlineData(ClientUpdaterKind.Edupdate2, "EDUPDATE2.exe", "CliUp2.ini")]
    public void UpdaterKind_PairsExeWithItsStampFile(ClientUpdaterKind kind, string exe, string stampFile)
    {
        Assert.Equal(exe, Updater.UpdaterExeFor(kind));
        Assert.Equal(stampFile, Updater.StampFileFor(kind));
    }

    [Fact]
    public void BuildArguments_Legacy_ComposesLegacyEdupdateContract()
    {
        var args = Updater.BuildArguments(ClientUpdaterKind.LegacyEdupdate, @"\\server\Edison", "260901.1",
            forced: true, forcedStamp: "66234,2");

        var expectedTarget = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Assert.Equal(
        [
            "FormMode=1",
            "Async=1",
            "RunEdison=1",
            @"SourcePath=\\server\Edison",
            $"TargetPath={expectedTarget}",
            "Version=260901.1",
            "NucenyUpdate=True",
            "NucenyUpdateTime=66234,2",
            "WaitProcess=EDISONMAIN2",
            "RunExe=EDISON.exe",
        ], args);
    }

    [Fact]
    public void BuildArguments_Legacy_NotForced_SendsFalseAndStamp()
    {
        // Razítko jde i s běžným updatem - úspěch splní čekající vlnu bez druhého kola.
        var args = Updater.BuildArguments(ClientUpdaterKind.LegacyEdupdate, @"\\s", "1", forced: false,
            forcedStamp: "66234,2");

        Assert.Contains("NucenyUpdate=False", args);
        Assert.Contains("NucenyUpdateTime=66234,2", args);
    }

    /// <summary>Čtení jako legacy UPDATERCL <c>CommandLine.GetArgument</c>: argv spojené přes NUL,
    /// první výskyt <c>Key=</c>, hodnota do dalšího NUL.</summary>
    private static string LegacyGetArgument(IReadOnlyList<string> argv, string key)
    {
        var joined = string.Join('\0', argv);
        int start = joined.IndexOf(key + "=", StringComparison.Ordinal);
        if (start < 0) return "";
        start += key.Length + 1;
        int end = joined.IndexOf('\0', start);
        return end < 0 ? joined[start..] : joined[start..end];
    }

    [Fact]
    public void BuildArguments_Legacy_ParsedLikeLegacyEdupdate_EveryKeyGetsItsOwnValue()
    {
        // Legacy parser bere první výskyt "Key=" - klíč schovaný v jiném tokenu (např. OldVersion
        // před Version) by si vzal cizí hodnotu; mezera v cestě musí projít celá.
        const string source = @"\\server\EDISON data\Účetní";
        var argv = new List<string> { @"C:\Edison\EDUPDATE.exe" };
        argv.AddRange(Updater.BuildArguments(ClientUpdaterKind.LegacyEdupdate, source, "260901.1",
            forced: true, forcedStamp: "66234,2"));

        Assert.Equal("1", LegacyGetArgument(argv, "FormMode"));
        Assert.Equal("1", LegacyGetArgument(argv, "Async"));
        Assert.Equal("1", LegacyGetArgument(argv, "RunEdison"));
        Assert.Equal(source, LegacyGetArgument(argv, "SourcePath"));
        Assert.Equal(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), LegacyGetArgument(argv, "TargetPath"));
        Assert.Equal("260901.1", LegacyGetArgument(argv, "Version"));
        Assert.Equal("True", LegacyGetArgument(argv, "NucenyUpdate"));
        Assert.Equal("66234,2", LegacyGetArgument(argv, "NucenyUpdateTime"));
        Assert.Equal("EDISONMAIN2", LegacyGetArgument(argv, "WaitProcess"));
        Assert.Equal("EDISON.exe", LegacyGetArgument(argv, "RunExe"));
    }

    [Fact]
    public void BuildArguments_Edupdate2_ComposesEdupdate2Contract()
    {
        var args = Updater.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\server\Edison", "260901.1",
            forced: true, forcedStamp: "66234,2");

        var expectedTarget = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Assert.Equal(
        [
            @"SourcePath=\\server\Edison",
            $"TargetPath={expectedTarget}",
            "Version=260901.1",
            "WaitProcess=EDISONMAIN2",
            "RunExe=EDISON.exe",
            "NucenyUpdate=1",
            "NucenyUpdateTime=66234,2",
        ], args);
    }

    [Fact]
    public void BuildArguments_Edupdate2_NotForced_SendsZeroAndStamp()
    {
        var args = Updater.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\s", "1", forced: false,
            forcedStamp: "66234,2");

        Assert.Contains("NucenyUpdate=0", args);
        Assert.Contains("NucenyUpdateTime=66234,2", args);
    }

    [Fact]
    public void BuildArguments_Edupdate2_ParsedByUpdaterArgs_RoundTripsWithoutUnknownTokens()
    {
        // Drift guard obou stran kontraktu: přejmenování klíče na jedné straně by prošlo
        // oběma sadami zeleně - tady se producent a konzument (source-link) potkají.
        var unknown = new List<string>();

        var parsed = EDUPDATE2.UpdaterArgs.Parse(
            Updater.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\server\Edison", "260901.1",
                forced: true, forcedStamp: "66234,2"),
            unknown.Add);

        Assert.Empty(unknown);
        Assert.Equal(@"\\server\Edison", parsed.SourcePath);
        Assert.Equal(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), parsed.TargetPath);
        Assert.Equal("260901.1", parsed.Version);
        Assert.Equal("EDISONMAIN2", parsed.WaitProcess);
        Assert.Equal("EDISON.exe", parsed.RunExe);
        Assert.True(parsed.NucenyUpdate);
        Assert.Equal("66234,2", parsed.NucenyUpdateTime);
    }

    [Fact]
    public void ReadForcedStampSeen_EachUpdaterReadsItsOwnStampFile()
    {
        // Legacy EDUPDATE píše CliUp.ini, EDUPDATE2 CliUp2.ini - čtení cizího souboru by vlnu
        // nikdy nesplnilo a každý start by spustil plnou kopii.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "CliUp.ini"), "Spuštěn=66234,2\r\n");
            File.WriteAllText(Path.Combine(dir, "CliUp2.ini"), "Spuštěn=11111,1\r\n");

            Assert.Equal("66234,2", Updater.ReadForcedStampSeen(dir, ClientUpdaterKind.LegacyEdupdate));
            Assert.Equal("11111,1", Updater.ReadForcedStampSeen(dir, ClientUpdaterKind.Edupdate2));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private class NoticeUpdater : Updater
    {
        public string? Content { get; set; }
        public string? UrlSeen { get; private set; }

        protected override string DownloadReleaseVersionFile(string url)
        {
            UrlSeen = url;
            return Content ?? throw new HttpRequestException("offline");
        }
    }

    private static readonly UpdateSettings NoticeSettings =
        new("", @"\\edison", "", "release.m-line.cz", 59993);

    [Fact]
    public void ReleaseNotice_NewerRemote_NewVersionAvailable()
    {
        var updater = new NoticeUpdater { Content = "LAST=20260701.1\nTYPE=1\nBlokovatAktualizace=0" };

        var result = updater.GetReleaseNotice(NoticeSettings, "20260526.1");

        Assert.Equal(ReleaseNoticeOutcome.NewVersionAvailable, result.Outcome);
        Assert.Equal("20260701.1", result.Version);
        Assert.Equal("http://release.m-line.cz:59993/EDISON/RELEASE/VERZE.INF", updater.UrlSeen);
    }

    [Fact]
    public void ReleaseNotice_SameOrOlderRemote_UpToDate()
    {
        Assert.Equal(ReleaseNoticeOutcome.UpToDate,
            Updater.EvaluateReleaseNotice("LAST=20260526.1", "20260526.1").Outcome);
        Assert.Equal(ReleaseNoticeOutcome.UpToDate,
            Updater.EvaluateReleaseNotice("LAST=20260101.1", "20260526.1").Outcome);
    }

    [Fact]
    public void ReleaseNotice_DistributionBlocked_ActsAsIfNoNewVersionExists()
    {
        // Pozastavená distribuce se navenek tváří, jako by novější verze nebyla publikovaná.
        Assert.Equal(ReleaseNoticeOutcome.UpToDate,
            Updater.EvaluateReleaseNotice("LAST=20260701.1\nBlokovatAktualizace=1", "20260526.1").Outcome);
        Assert.Equal(ReleaseNoticeOutcome.UpToDate,
            Updater.EvaluateReleaseNotice("LAST=20260526.1\nBlokovatAktualizace=1", "20260526.1").Outcome);
    }

    [Fact]
    public void ReleaseNotice_MissingLastKey_Unknown()
    {
        Assert.Equal(ReleaseNoticeOutcome.Unknown,
            Updater.EvaluateReleaseNotice("TYPE=1", "20260526.1").Outcome);
    }

    [Fact]
    public void ReleaseNotice_DownloadFails_Unknown()
    {
        // Notice-only - nedostupný release server (stanice bez internetu) nesmí nic rozbít.
        var updater = new NoticeUpdater { Content = null };

        Assert.Equal(ReleaseNoticeOutcome.Unknown, updater.GetReleaseNotice(NoticeSettings, "20260526.1").Outcome);
    }

    [Fact]
    public void ReleaseNotice_MissingSourceServerOrDevVersion_SkipsDownload()
    {
        var updater = new NoticeUpdater { Content = "LAST=20260701.1" };

        Assert.Equal(ReleaseNoticeOutcome.Unknown,
            updater.GetReleaseNotice(NoticeSettings with { SourceServer = "" }, "20260526.1").Outcome);
        Assert.Equal(ReleaseNoticeOutcome.Unknown,
            updater.GetReleaseNotice(NoticeSettings with { SourcePort = 0 }, "20260526.1").Outcome);
        Assert.Equal(ReleaseNoticeOutcome.Unknown,
            updater.GetReleaseNotice(NoticeSettings, VerzeInf.DevVersion).Outcome);
        Assert.Equal(ReleaseNoticeOutcome.Unknown,
            updater.GetReleaseNotice(NoticeSettings, "").Outcome);
        Assert.Null(updater.UrlSeen);
    }
}
