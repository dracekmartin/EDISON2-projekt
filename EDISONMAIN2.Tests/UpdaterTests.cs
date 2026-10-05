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
        public bool RunsFromSource { get; set; }
        public bool LocalClient { get; set; } = true;
        public string? SourceDirSeen { get; private set; }

        protected override string GetLocalVersion() => LocalVersion;

        protected override bool RunsFromSourceDir(string sourceDir) => RunsFromSource;

        protected override bool IsLocalClient(IReadOnlyList<string> localClients) => LocalClient;

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
    public void LoadSettings_NoLocalClients_EveryInstallationIsServer()
    {
        // Legacy clsUpdate.IsServer: bez seznamu lokálních klientů jsou serverové všechny instalace.
        Assert.True(new Updater().LoadSettings(MockSettings("version~260901.1")).ServerInstallation);
    }

    [Fact]
    public void LoadSettings_OnlyOtherStationsListed_Server()
    {
        // Tvar řádku z DEV: localPath~<Pc>~<poznámka>.
        Assert.True(new Updater().LoadSettings(MockSettings("localPath~XB-NEEXISTUJE~")).ServerInstallation);
    }

    [Fact]
    public void LoadSettings_ThisStationListed_LocalClient()
    {
        var host = System.Net.Dns.GetHostName().ToLowerInvariant();

        Assert.False(new Updater().LoadSettings(MockSettings("localPath~XB~", $"localPath~{host}~účtárna"))
            .ServerInstallation);
        Assert.False(new Updater().LoadSettings(MockSettings($@"localPath~\\{host}\Edison~"))
            .ServerInstallation);
    }

    [Theory]
    [InlineData("PC01", true)]
    [InlineData(@"\\pc01\Edison", true)]
    [InlineData(@"\\PC01", true)]
    [InlineData(@"\\PC02\Edison", false)]
    [InlineData("10.0.0.5", true)]
    [InlineData(@"\\10.0.0.5\Edison", true)]
    [InlineData("10.0.0.6", false)]
    [InlineData("pc01.firma.local", false)] // tečka = IP, FQDN legacy nikdy nespáruje
    [InlineData(@"C:\Edison", false)]
    [InlineData("", false)]
    public void IsListedClient_LegacyMatching(string entry, bool expected) =>
        Assert.Equal(expected, Updater.IsListedClient([entry], "PC01", "10.0.0.5"));

    [Fact]
    public void IsListedClient_EmptyListOrUnknownIp_False()
    {
        Assert.False(Updater.IsListedClient([], "PC01", "10.0.0.5"));
        Assert.False(Updater.IsListedClient(["10.0.0.5"], "PC01", ""));
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
    public void Check_ForcedStampButClientRunsFromSourceDir_None()
    {
        // Legacy parita: klient spuštěný přímo ze zdroje by kopíroval updater sám na sebe
        // a při každém startu skončil na výjimce.
        var updater = new TestableUpdater
        {
            LocalVersion = "20260526.1", SourceVersion = "20260526.1", StampSeen = "65995,1",
            RunsFromSource = true,
        };

        var result = updater.Check(new UpdateSettings("20260526.1", @"\\edison", "", ForcedStamp: "66234,2"));

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_ShareStationWithEmptyLocalSourcePath_StartsWithoutReadingSource()
    {
        // Stanice ze share: zdroj = edisonPath serveru (D:\EDISON), ze stanice nečitelný - dřív
        // „nelze zjistit serverovou verzi" při každém startu.
        var updater = new TestableUpdater { LocalVersion = "260901.1", SourceVersion = "" };

        var result = updater.Check(new UpdateSettings("260901.1", @"D:\EDISON", "") { ServerInstallation = true });

        Assert.Equal(UpdateOutcome.None, result.Outcome);
        Assert.Null(updater.SourceDirSeen);
    }

    [Fact]
    public void Check_ServerInstallationCacheMismatch_Broken()
    {
        // Legacy VersionMismatch: serverová instalace s jinou verzí než Caché se nepřipojí.
        var updater = new TestableUpdater { LocalVersion = "260901.1", SourceVersion = "260915.1" };

        var result = updater.Check(new UpdateSettings("260915.1", @"D:\EDISON", "") { ServerInstallation = true });

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("260901.1 -> 260915.1", result.Message);
        Assert.Null(updater.SourceDirSeen);
    }

    [Fact]
    public void Check_ServerInstallationDevBuild_None()
    {
        // Na DEV je v seznamu jen XB, vývojářský stroj je tedy serverový - DEBUG build nesmí skončit na Caché mismatchi.
        var updater = new TestableUpdater { LocalVersion = VerzeInf.DevVersion };

        var result = updater.Check(new UpdateSettings("260915.1", @"D:\EDISON", "") { ServerInstallation = true });

        Assert.Equal(UpdateOutcome.None, result.Outcome);
    }

    [Fact]
    public void Check_ServerInstallationPending_Broken()
    {
        // I serverová instalace se během instalace verze nepřihlásí (parita EDLOADER UpdateIsRunning).
        var updater = new TestableUpdater { LocalVersion = "260915.1" };

        var result = updater.Check(new UpdateSettings("260915.1", @"D:\EDISON", "", Pending: true)
            { ServerInstallation = true });

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("aktualizace systému", result.Message);
    }

    [Fact]
    public void Check_ServerInstallationWithoutInstalledRelease_None()
    {
        var updater = new TestableUpdater { LocalVersion = "260901.1" };

        var result = updater.Check(new UpdateSettings("", @"D:\EDISON", "") { ServerInstallation = true });

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

    /// <summary>Base <c>GetSourceVersion</c>/<c>CopyUpdaterTo</c> nad falešným HTTP.</summary>
    private class HttpSourceUpdater : Updater
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public List<string> Requested { get; } = [];
        public Exception? Failure { get; set; }

        protected override string GetLocalVersion() => "20260526.1";

        protected override byte[] DownloadSourceFile(string url)
        {
            Requested.Add(url);
            if (Failure != null) throw Failure;
            return Files.TryGetValue(url, out var content) ? content : throw new HttpRequestException("404");
        }
    }

    [Theory]
    [InlineData("http://srv/edison", true)]
    [InlineData("HTTPS://srv/edison", true)]
    [InlineData(@"\\srv\edison", false)]
    [InlineData(@"D:\Edison", false)]
    [InlineData("", false)]
    public void IsUri_LegacyPathIsUriSemantics(string path, bool expected) =>
        Assert.Equal(expected, UpdaterContract.IsUri(path));

    [Theory]
    [InlineData("http://srv/edison", "http://srv/edison/VERZE.INF")]
    [InlineData("http://srv/edison/", "http://srv/edison/VERZE.INF")]
    [InlineData(@"http://srv/edison\release\", "http://srv/edison/release/VERZE.INF")]
    public void FileUrl_JoinsWithSingleSlash(string sourceDir, string expected) =>
        Assert.Equal(expected, UpdaterContract.FileUrl(sourceDir, VerzeInf.FileName));

    [Theory]
    [InlineData("EDFiles", "http://srv/edison/EDFiles/a.txt")]
    [InlineData(@"EDFiles\", "http://srv/edison/EDFiles/a.txt")]
    [InlineData(@"PS\sub", "http://srv/edison/PS/sub/a.txt")]
    public void FileUrl_SubPathOfManifestRecord_JoinedWithForwardSlashes(string subPath, string expected) =>
        Assert.Equal(expected, UpdaterContract.FileUrl("http://srv/edison/", "a.txt", subPath));

    [Fact]
    public void Check_HttpSource_VersionDownloaded()
    {
        var updater = new HttpSourceUpdater();
        updater.Files["http://srv/edison/VERZE.INF"] = System.Text.Encoding.UTF8.GetBytes("TYPE=1\r\nLAST=20260601.1\r\n");

        var result = updater.Check(new UpdateSettings("20260601.1", @"D:\Edison", "http://srv/edison"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.Equal("20260601.1", result.Version);
    }

    [Fact]
    public void Check_HttpSourceUnreachable_Broken()
    {
        var updater = new HttpSourceUpdater();

        var result = updater.Check(new UpdateSettings("20260601.1", @"D:\Edison", "http://srv/edison"));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
        Assert.Contains("http://srv/edison", result.Message);
    }

    [Fact]
    public void CopyUpdater_HttpSource_DownloadsWithoutLeftovers()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var exe = UpdaterContract.ExeFor(Updater.ActiveKind);
            File.WriteAllText(Path.Combine(dir, exe), "old");
            var updater = new HttpSourceUpdater();
            updater.Files[$"http://srv/edison/{exe}"] = [(byte)'M', (byte)'Z', 3];

            updater.CopyUpdaterTo("http://srv/edison", dir);

            Assert.Equal(new byte[] { (byte)'M', (byte)'Z', 3 }, File.ReadAllBytes(Path.Combine(dir, exe)));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CopyUpdater_HttpDownloadFails_LocalUpdaterKept()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var exe = UpdaterContract.ExeFor(Updater.ActiveKind);
            File.WriteAllText(Path.Combine(dir, exe), "old");

            Assert.Throws<HttpRequestException>(() => new HttpSourceUpdater().CopyUpdaterTo("http://srv/edison", dir));

            Assert.Equal("old", File.ReadAllText(Path.Combine(dir, exe)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Check_HttpSourceWithBom_VersionRead()
    {
        // UNC cesta (File.ReadAllText) BOM odstraní, http cesta ho musí zvládnout stejně.
        var updater = new HttpSourceUpdater();
        updater.Files["http://srv/edison/VERZE.INF"] = [0xEF, 0xBB, 0xBF, .. "LAST=20260601.1\r\n"u8.ToArray()];

        var result = updater.Check(new UpdateSettings("20260601.1", @"D:\Edison", "http://srv/edison"));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.Equal("20260601.1", result.Version);
    }

    [Fact]
    public void Check_HttpConnectionDropsMidBody_Broken()
    {
        // HttpIOException dědí z IOException, ne z HttpRequestException.
        var updater = new HttpSourceUpdater { Failure = new IOException("connection reset") };

        var result = updater.Check(new UpdateSettings("20260601.1", @"D:\Edison", "http://srv/edison"));

        Assert.Equal(UpdateOutcome.Broken, result.Outcome);
    }

    [Fact]
    public void Check_ServerOrTerminalServerWithHttpSource_NeverLaunchesUpdater()
    {
        // Server / TS s http zdrojem: vynucená vlna ani novější verze na zdroji nesmí spustit EDUPDATE
        // do instalace, ze které běží ostatní uživatelé.
        var updater = new HttpSourceUpdater();
        updater.Files["http://srv/edison/VERZE.INF"] = "LAST=20260601.1\r\n"u8.ToArray();

        var result = updater.Check(new UpdateSettings("20260526.1", @"D:\Edison", "http://srv/edison",
            ForcedStamp: "66234,2") { ServerInstallation = true });

        Assert.Equal(UpdateOutcome.None, result.Outcome);
        Assert.Empty(updater.Requested);
    }

    [Fact]
    public void CopyUpdater_HttpReturnsNonExecutable_LocalUpdaterKept()
    {
        // Chybová stránka portálu s HTTP 200 nesmí přepsat updater.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var exe = UpdaterContract.ExeFor(Updater.ActiveKind);
            File.WriteAllText(Path.Combine(dir, exe), "old");
            var updater = new HttpSourceUpdater();
            updater.Files[$"http://srv/edison/{exe}"] = "<html>login</html>"u8.ToArray();

            Assert.Throws<InvalidDataException>(() => updater.CopyUpdaterTo("http://srv/edison", dir));

            Assert.Equal("old", File.ReadAllText(Path.Combine(dir, exe)));
            Assert.Single(Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsSameInstallation_HttpSource_False() =>
        Assert.False(Updater.IsSameInstallation(@"D:\Edison", "http://srv/edison"));

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
        Assert.Equal(exe, UpdaterContract.ExeFor(kind));
        Assert.Equal(stampFile, UpdaterContract.StampFileFor(kind));
    }

    private const string Station = @"C:\Program Files\Edison";

    [Fact]
    public void BuildArguments_Legacy_ComposesLegacyEdupdateContract()
    {
        var args = UpdaterContract.BuildArguments(ClientUpdaterKind.LegacyEdupdate, @"\\server\Edison", Station,
            "260901.1", forced: true, forcedStamp: "66234,2");

        Assert.Equal(
        [
            "FormMode=1",
            "Async=1",
            "RunEdison=1",
            @"SourcePath=\\server\Edison",
            $"TargetPath={Station}",
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
        var args = UpdaterContract.BuildArguments(ClientUpdaterKind.LegacyEdupdate, @"\\s", Station, "1",
            forced: false, forcedStamp: "66234,2");

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
        argv.AddRange(UpdaterContract.BuildArguments(ClientUpdaterKind.LegacyEdupdate, source, Station,
            "260901.1", forced: true, forcedStamp: "66234,2"));

        Assert.Equal("1", LegacyGetArgument(argv, "FormMode"));
        Assert.Equal("1", LegacyGetArgument(argv, "Async"));
        Assert.Equal("1", LegacyGetArgument(argv, "RunEdison"));
        Assert.Equal(source, LegacyGetArgument(argv, "SourcePath"));
        Assert.Equal(Station, LegacyGetArgument(argv, "TargetPath"));
        Assert.Equal("260901.1", LegacyGetArgument(argv, "Version"));
        Assert.Equal("True", LegacyGetArgument(argv, "NucenyUpdate"));
        Assert.Equal("66234,2", LegacyGetArgument(argv, "NucenyUpdateTime"));
        Assert.Equal("EDISONMAIN2", LegacyGetArgument(argv, "WaitProcess"));
        Assert.Equal("EDISON.exe", LegacyGetArgument(argv, "RunExe"));
    }

    [Fact]
    public void BuildArguments_Edupdate2_ComposesEdupdate2Contract()
    {
        var args = UpdaterContract.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\server\Edison", Station,
            "260901.1", forced: true, forcedStamp: "66234,2");

        Assert.Equal(
        [
            @"SourcePath=\\server\Edison",
            $"TargetPath={Station}",
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
        var args = UpdaterContract.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\s", Station, "1",
            forced: false, forcedStamp: "66234,2");

        Assert.Contains("NucenyUpdate=0", args);
        Assert.Contains("NucenyUpdateTime=66234,2", args);
    }

    [Fact]
    public void BuildArguments_Edupdate2_ParsedByUpdaterArgs_RoundTripsWithoutUnknownTokens()
    {
        // Producent (shell) a konzument (EDUPDATE2) kontraktu se tu potkají: klíč přidaný jen do
        // jednoho směru by skončil jako neznámý token nebo ztracená hodnota.
        var unknown = new List<string>();

        var parsed = UpdaterArgs.Parse(
            UpdaterContract.BuildArguments(ClientUpdaterKind.Edupdate2, @"\\server\Edison", Station, "260901.1",
                forced: true, forcedStamp: "66234,2"),
            unknown.Add);

        Assert.Empty(unknown);
        Assert.Equal(@"\\server\Edison", parsed.SourcePath);
        Assert.Equal(Station, parsed.TargetPath);
        Assert.Equal("260901.1", parsed.Version);
        Assert.Equal("EDISONMAIN2", parsed.WaitProcess);
        Assert.Equal("EDISON.exe", parsed.RunExe);
        Assert.True(parsed.NucenyUpdate);
        Assert.Equal("66234,2", parsed.NucenyUpdateTime);
    }

    [Theory]
    [InlineData(@"\\server\Edison")]
    [InlineData(@"D:\Edison")]
    [InlineData("http://srv/edison")]
    [InlineData("https://srv/edison")]
    public void Check_SourceLetThroughToUpdate_ReachesEveryUpdaterUnchanged(string source)
    {
        // Updater zdroj, který shell pustí k updatu, musí přijmout: jinak ho shell spouští při každém
        // startu znovu. Legacy EDUPDATE bere adresář i http (ValidateParameters), EDUPDATE2 obojí
        // ověřuje EDUPDATE2.Tests (http sada UpdateEngineHttpTests).
        var updater = new TestableUpdater { LocalVersion = "260825.1", SourceVersion = "260901.1" };

        var result = updater.Check(new UpdateSettings("260825.1", @"D:\Edison", source));

        Assert.Equal(UpdateOutcome.UpdateRequired, result.Outcome);
        Assert.Equal(source, updater.SourceDirSeen);
        foreach (var kind in Enum.GetValues<ClientUpdaterKind>())
        {
            var parsed = LegacyGetArgument(
                UpdaterContract.BuildArguments(kind, source, Station, result.Version, result.Forced, ""), "SourcePath");
            Assert.Equal(source, parsed);
        }
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
