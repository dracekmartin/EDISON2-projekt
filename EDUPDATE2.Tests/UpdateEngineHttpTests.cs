using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace EDUPDATE2.Tests;

/// <summary>Http zdroj (legacy PathIsUri): adresář _src servíruje harness jako http://srv/edison.</summary>
public class UpdateEngineHttpTests : IDisposable
{
    private const string Source = "http://srv/edison";
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _src;
    private readonly string _tgt;

    public UpdateEngineHttpTests()
    {
        _src = Path.Combine(_root, "src");
        _tgt = Path.Combine(_root, "tgt");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_tgt);
        File.WriteAllText(Path.Combine(_src, "VERZE.INF"), "LAST=260825.2\n");
        File.WriteAllText(Path.Combine(_tgt, "VERZE.INF"), "LAST=260825.1\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private RecordingEngine Engine(bool forced = false, string source = Source, string stampTime = "",
        CancellationToken cancellationToken = default) =>
        new(new UpdaterArgs(SourcePath: source, TargetPath: _tgt, NucenyUpdate: forced, NucenyUpdateTime: stampTime),
            new UpdateLog(_tgt), cancellationToken: cancellationToken)
        {
            HttpSourceUrl = source.TrimEnd('/'),
            HttpSourceDir = _src,
        };

    private void WriteSrc(string relative, string content) => Write(_src, relative, content);

    private void WriteTgt(string relative, string content) => Write(_tgt, relative, content);

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string ReadTgt(string relative) => File.ReadAllText(Path.Combine(_tgt, relative));

    /// <summary>EDISONCRC.bin s větou pro každý zadaný soubor zdroje (cesta "EDFiles/x" = podadresář).</summary>
    private void WriteManifest(params string[] relativePaths)
    {
        var records = relativePaths.Select(relative =>
        {
            var bytes = File.ReadAllBytes(Path.Combine(_src, relative));
            var subPath = Path.GetDirectoryName(relative) ?? "";
            return new CrcRecord(Path.GetFileName(relative), subPath, Convert.ToHexString(SHA256.HashData(bytes)));
        }).ToArray();
        using var stream = File.Create(Path.Combine(_src, CrcFile.FileName));
        CrcFile.Write(stream, records);
    }

    private static string Url(string relative) => Source + "/" + relative;

    [Fact]
    public void Run_DiffByManifest_DownloadsOnlyChangedFiles()
    {
        WriteSrc("A.dll", "same");
        WriteTgt("A.dll", "same");
        WriteSrc("B.dll", "newB");
        WriteTgt("B.dll", "oldB");
        WriteSrc("EDFiles/C.txt", "newC");
        WriteManifest("A.dll", "B.dll", "EDFiles/C.txt");
        var engine = Engine();

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("newB", ReadTgt("B.dll"));
        Assert.Equal("newC", ReadTgt("EDFiles/C.txt"));
        Assert.Equal("LAST=260825.2\n", ReadTgt("VERZE.INF"));
        Assert.Contains(Url("B.dll"), engine.Downloads);
        Assert.Contains(Url("EDFiles/C.txt"), engine.Downloads);
        Assert.DoesNotContain(Url("A.dll"), engine.Downloads);
    }

    [Theory]
    [InlineData("http://srv/edison")]
    [InlineData("https://srv/edison/")]
    [InlineData("HTTP://srv/edison")]
    public void Run_HttpAndHttpsSource_PassValidationAndInstall(string source)
    {
        // Shell posílá updateru každý zdroj, který jeho Check pustí k updatu (UpdaterTests v EDISONMAIN2).
        WriteSrc("B.dll", "newB");
        WriteManifest("B.dll");

        var result = Engine(source: source).Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("newB", ReadTgt("B.dll"));
    }

    [Fact]
    public void Run_WithoutManifest_Exit1_TargetUntouched()
    {
        // Bez výpisu adresáře je manifest jediný seznam souborů zdroje.
        WriteSrc("B.dll", "newB");
        WriteTgt("B.dll", "oldB");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains(CrcFile.FileName, result.Message);
        Assert.Equal("oldB", ReadTgt("B.dll"));
        Assert.Equal("LAST=260825.1\n", ReadTgt("VERZE.INF"));
    }

    [Fact]
    public void Run_CorruptManifest_Exit1_NoFullCopyFallback()
    {
        File.WriteAllText(Path.Combine(_src, CrcFile.FileName), "tohle není CRC soubor");
        WriteSrc("B.dll", "newB");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "B.dll")));
    }

    [Fact]
    public void Run_Forced_DownloadsWholeManifestRegardlessOfHash()
    {
        WriteSrc("A.dll", "MZsame");
        WriteTgt("A.dll", "MZsame");
        WriteManifest("A.dll");
        var engine = Engine(forced: true);

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Contains(Url("A.dll"), engine.Downloads);
    }

    [Fact]
    public void Run_ForcedHotfixWithStaleManifest_InstallsServerFileAndSatisfiesWave()
    {
        // Hotfix: opravená binárka nahraná na server, manifest EDISONCRC.bin zůstal z instalace verze.
        WriteSrc("A.dll", "MZpůvodní");
        WriteManifest("A.dll");
        WriteSrc("A.dll", "MZopravená");
        WriteTgt("A.dll", "MZpůvodní");

        var result = Engine(forced: true, stampTime: "66234,7").Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("MZopravená", ReadTgt("A.dll"));
        Assert.Equal("66234,7", ForcedStamp.Read(_tgt));
    }

    [Theory]
    [InlineData("A.dll")]
    [InlineData("EDISONMAIN2.EXE")]
    public void Run_ForcedErrorPageInsteadOfBinary_Exit1_WaveNotSatisfied(string binary)
    {
        // Bez hashe pozná chybovou stránku proxy jen hlavička PE; jinak by razítko splnilo vlnu nad rozbitou stanicí.
        WriteSrc(binary, "MZpůvodní");
        WriteManifest(binary);
        WriteSrc(binary, "<html>přihlášení do sítě</html>");
        WriteTgt(binary, "MZpůvodní");

        var result = Engine(forced: true, stampTime: "66234,7").Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains(binary, result.Message);
        Assert.Equal("MZpůvodní", ReadTgt(binary));
        Assert.Equal("", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_ForcedNonBinaryWithoutPeHeader_Installed()
    {
        WriteSrc("EDFiles/sestava.rpt", "x");
        WriteManifest("EDFiles/sestava.rpt");

        var result = Engine(forced: true).Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("x", ReadTgt("EDFiles/sestava.rpt"));
    }

    [Fact]
    public void Run_ManifestFileMissingOnSource_Exit1_TargetUntouched()
    {
        WriteSrc("B.dll", "newB");
        WriteTgt("B.dll", "oldB");
        WriteSrc("X.dll", "x");
        WriteManifest("B.dll", "X.dll");
        File.Delete(Path.Combine(_src, "X.dll"));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("X.dll", result.Message);
        Assert.Equal("oldB", ReadTgt("B.dll"));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void Run_OptionalConfigMissingOnSource_SkippedAndLocalCopyKept()
    {
        // Legacy InstallEdisonFiles ignoruje chybu stažení EDISONini.xml a MLServerConfig-data.xml.
        WriteTgt("EDISONini.xml", "lokální");
        WriteSrc("MLServerConfig-data.xml", "ze serveru");
        WriteSrc("B.dll", "newB");
        WriteManifest("B.dll");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("lokální", ReadTgt("EDISONini.xml"));
        Assert.Equal("ze serveru", ReadTgt("MLServerConfig-data.xml"));
    }

    [Fact]
    public void Run_OptionalConfigServerError_SkippedAndLocalCopyKept()
    {
        // Legacy přeskočí jakoukoli chybu přenosu těchto dvou souborů, nejen 404.
        WriteTgt("EDISONini.xml", "lokální");
        WriteSrc("EDISONini.xml", "ze serveru");
        WriteSrc("B.dll", "newB");
        WriteManifest("B.dll");
        var engine = Engine();
        engine.DownloadFailure = url => url.EndsWith("EDISONini.xml") ? new HttpRequestException("403") : null;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("lokální", ReadTgt("EDISONini.xml"));
        Assert.Equal("newB", ReadTgt("B.dll"));
    }

    [Fact]
    public void Run_TimeoutOnEveryAttempt_ReportedAsDownloadFailureNotAbort()
    {
        WriteSrc("B.dll", "newB");
        WriteManifest("B.dll");
        var engine = Engine();
        engine.DownloadFailure = url => url.EndsWith("B.dll") ? new TaskCanceledException("timeout") : null;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("Stažení souborů selhalo", result.Message);
        Assert.DoesNotContain("přerušena", result.Message);
    }

    [Fact]
    public void Run_ErrorPageInsteadOfFile_HashMismatch_Exit1()
    {
        // Portál/proxy s HTTP 200 vrátí stránku místo binárky - zachytí ji hash z manifestu.
        WriteSrc("B.dll", "newB");
        WriteTgt("B.dll", "oldB");
        WriteManifest("B.dll");
        WriteSrc("B.dll", "<html>přihlášení</html>");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Equal("oldB", ReadTgt("B.dll"));
    }

    [Fact]
    public void Run_StaleDiffInf_DeletedUnlessManifestCarriesIt()
    {
        WriteSrc("EDISONMAIN.exe", "new");
        WriteTgt("EDISONMAIN.exe", "old");
        WriteTgt("EDISONMAIN_DIFF.INF", "staré novinky");
        WriteSrc("USERS.exe", "new");
        WriteSrc("USERS_DIFF.INF", "nové novinky");
        WriteTgt("USERS_DIFF.INF", "nové novinky");
        WriteManifest("EDISONMAIN.exe", "USERS.exe", "USERS_DIFF.INF");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "EDISONMAIN_DIFF.INF")));
        Assert.Equal("nové novinky", ReadTgt("USERS_DIFF.INF"));
    }

    [Fact]
    public void Run_TransientFailureAndTimeout_Retried()
    {
        WriteSrc("B.dll", "newB");
        WriteSrc("C.dll", "newC");
        WriteManifest("B.dll", "C.dll");
        var failed = new HashSet<string>();
        var engine = Engine();
        // Timeout HttpClientu přichází jako TaskCanceledException bez zrušeného tokenu.
        engine.DownloadFailure = url => !failed.Add(url) ? null
            : url.EndsWith("B.dll") ? new HttpRequestException("connection reset")
            : url.EndsWith("C.dll") ? new TaskCanceledException("timeout")
            : null;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("newB", ReadTgt("B.dll"));
        Assert.Equal("newC", ReadTgt("C.dll"));
    }

    [Fact]
    public void Run_SourceUnreachable_Exit1_ReportsVersionFile()
    {
        var engine = Engine();
        engine.DownloadFailure = _ => new HttpRequestException("no route to host");

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("VERZE.INF", result.Message);
        Assert.Equal("LAST=260825.1\n", ReadTgt("VERZE.INF"));
    }

    [Fact]
    public void Run_WindowClosedDuringDownload_AbortedNotRetried()
    {
        WriteSrc("B.dll", "newB");
        WriteManifest("B.dll");
        using var cts = new CancellationTokenSource();
        var engine = Engine(cancellationToken: cts.Token);
        engine.DownloadFailure = url =>
        {
            if (!url.EndsWith("B.dll")) return null;
            cts.Cancel();
            return new OperationCanceledException(cts.Token);
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("přerušena", result.Message);
        Assert.Single(engine.Downloads, Url("B.dll"));
        Assert.False(File.Exists(Path.Combine(_tgt, "B.dll")));
    }
}
