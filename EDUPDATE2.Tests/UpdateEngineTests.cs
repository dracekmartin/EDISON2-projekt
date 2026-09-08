using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

public class UpdateEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _src;
    private readonly string _tgt;

    public UpdateEngineTests()
    {
        _src = Path.Combine(_root, "src");
        _tgt = Path.Combine(_root, "tgt");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_tgt);
        File.WriteAllText(Path.Combine(_src, "VERZE.INF"), "LAST=260825.2\n");
        File.WriteAllText(Path.Combine(_tgt, "VERZE.INF"), "LAST=260825.1\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class TestEngine : UpdateEngine
    {
        public Func<string, int>? ProcessCountFor;
        public Func<string, string, bool>? MoveShouldFail;
        public Func<string, string, bool>? CopyShouldFail;
        public Action<string>? AfterCopy;

        public TestEngine(UpdaterArgs args, UpdateLog log, CancellationToken cancellationToken = default)
            : base(args, log, progress: null, cancellationToken) { }

        protected override int GetProcessCount(string name) => ProcessCountFor?.Invoke(name) ?? 0;
        protected override void Sleep(int milliseconds) { }
        protected override long GetFreeBytes(string path) => long.MaxValue;

        protected override void MoveFile(string source, string target)
        {
            if (MoveShouldFail?.Invoke(source, target) == true) throw new IOException("testovací lock");
            base.MoveFile(source, target);
        }

        protected override void CopyFile(string source, string target)
        {
            if (CopyShouldFail?.Invoke(source, target) == true) throw new IOException("testovací lock");
            base.CopyFile(source, target);
            AfterCopy?.Invoke(target);
        }
    }

    private TestEngine Engine(bool forced = false, string stampTime = "", string version = "")
    {
        var args = new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, Version: version,
            NucenyUpdate: forced, NucenyUpdateTime: stampTime);
        return new TestEngine(args, new UpdateLog(_tgt));
    }

    private void WriteSrc(string relative, string content)
    {
        var path = Path.Combine(_src, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteTgt(string relative, string content)
    {
        var path = Path.Combine(_tgt, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string Sha256OfContent(string relativeInSrc) =>
        CrcFile.ComputeSha256(Path.Combine(_src, relativeInSrc));

    private void WriteCrc(params CrcRecord[] records)
    {
        using var stream = File.Create(Path.Combine(_src, CrcFile.FileName));
        CrcFile.Write(stream, records);
    }

    [Fact]
    public void Run_DiffMode_CopiesOnlyMismatchPlusKeyFiles()
    {
        WriteSrc("A.dll", "new");
        WriteTgt("A.dll", "old");
        WriteTgt("B.dll", "same");
        // B.dll na zdroji chybí - kdyby ho engine nepřeskočil podle CRC shody, staging spadne
        WriteCrc(
            new CrcRecord("A.dll", "", Sha256OfContent("A.dll")),
            new CrcRecord("B.dll", "", CrcFile.ComputeSha256(Path.Combine(_tgt, "B.dll"))));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("LAST=260825.2\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void Run_DiffMode_SkipsOwnExe()
    {
        // Věta pro vlastní exe s neexistujícím zdrojem - kopie by spadla, skip projde
        WriteCrc(new CrcRecord("EDUPDATE2.exe", "", new string('F', 64)));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
    }

    [Fact]
    public void Run_Forced_IgnoresCrcAndVersion_WritesStamp()
    {
        WriteTgt("VERZE.INF", "LAST=260825.2\n"); // verze shodné - bez forced by byl no-op
        WriteSrc("C.txt", "obsah");
        WriteCrc(new CrcRecord("C.txt", "", Sha256OfContent("C.txt")));
        WriteTgt("C.txt", "obsah"); // CRC sedí - diff by nekopíroval

        var result = Engine(forced: true, stampTime: "66234,60449").Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("66234,60449", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_FullCopy_LocalesGoIntoSubdir()
    {
        WriteSrc(@"locales\cs.txt", "čeština");
        WriteSrc(@"EDFiles\form.edf", "form");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("čeština", File.ReadAllText(Path.Combine(_tgt, "locales", "cs.txt")));
        Assert.Equal("form", File.ReadAllText(Path.Combine(_tgt, "EDFiles", "form.edf")));
    }

    [Fact]
    public void Run_NotNewerNotForced_NoCopyNoStamp_Exit0()
    {
        WriteTgt("VERZE.INF", "LAST=260825.2\n");
        WriteSrc("A.dll", "new");

        var result = Engine(stampTime: "66234,60449").Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_StagingHashMismatch_TargetUntouched_Exit1()
    {
        WriteSrc("A.dll", "jiný obsah než čekáme");
        WriteTgt("A.dll", "old");
        WriteCrc(new CrcRecord("A.dll", "", new string('E', 64)));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Equal("old", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void Run_SwapFailure_RollsBack_Exit100()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        WriteTgt("A.dll", "oldA");
        var engine = Engine();
        engine.MoveShouldFail = (_, target) => target.EndsWith(@"tgt\B.dll", StringComparison.OrdinalIgnoreCase);

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitFailedRestored, result.ExitCode);
        Assert.Equal("oldA", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.False(File.Exists(Path.Combine(_tgt, "B.dll")));
        Assert.Equal("LAST=260825.1\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
    }

    [Fact]
    public void Run_RollbackFailure_Exit200()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        WriteTgt("A.dll", "oldA");
        var engine = Engine();
        engine.MoveShouldFail = (_, target) => target.EndsWith(@"tgt\B.dll", StringComparison.OrdinalIgnoreCase);
        // Obnova = kopie ZE zálohy; záloha samotná (kopie DO ní) projít musí
        engine.CopyShouldFail = (source, _) => source.Contains(UpdateEngine.BackupDirName);

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitFailedRestoreFailed, result.ExitCode);
    }

    [Fact]
    public void Run_WaitTimeout_Exit1_NothingChanged()
    {
        WriteSrc("A.dll", "new");
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt),
            new UpdateLog(_tgt))
        {
            WaitTimeoutMs = 500,
            ProcessCountFor = name => name == "EDISONMAIN" ? 1 : 0,
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("EDISONMAIN", result.Message);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_CorruptCrc_FallsBackToFullCopy()
    {
        File.WriteAllText(Path.Combine(_src, CrcFile.FileName), "tohle není CRC soubor");
        WriteSrc("A.dll", "new");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_StaleDiffInfOfUpdatedPackage_Deleted()
    {
        WriteSrc("EDISONMAIN.exe", "new");
        WriteTgt("EDISONMAIN.exe", "old");
        WriteTgt("EDISONMAIN_DIFF.INF", "staré novinky");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "EDISONMAIN_DIFF.INF")));
        Assert.True(File.Exists(Path.Combine(_tgt, UpdateEngine.BackupDirName, "EDISONMAIN_DIFF.INF")));
    }

    [Fact]
    public void Run_DiffInfCarriedBySource_Kept()
    {
        WriteSrc("EDISONMAIN.exe", "new");
        WriteSrc("EDISONMAIN_DIFF.INF", "nové novinky");
        WriteTgt("EDISONMAIN_DIFF.INF", "staré novinky");

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("nové novinky", File.ReadAllText(Path.Combine(_tgt, "EDISONMAIN_DIFF.INF")));
    }

    [Fact]
    public void Run_HttpSource_Exit1()
    {
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: "http://release.m-line.cz/edison", TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("HTTP", result.Message);
    }

    [Fact]
    public void Run_SourceEqualsTarget_Exit1()
    {
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _tgt, TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_Success_WritesStampOnNormalUpdate()
    {
        WriteSrc("A.dll", "new");

        var result = Engine(stampTime: "66234,60449").Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("66234,60449", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_DiffSubdirRecord_CopiedIntoSubdir()
    {
        WriteSrc(@"EDFiles\form.edf", "new");
        WriteCrc(new CrcRecord("form.edf", "EDFiles", Sha256OfContent(@"EDFiles\form.edf")));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "EDFiles", "form.edf")));
    }

    [Fact]
    public void Run_DuplicateAliasedRecords_HandledOnce_ExitOk()
    {
        // "B.dll" a "B.dll " je po Win32 normalizaci tentýž soubor - bez dedupu by druhý
        // MoveFile ve swapu spadl na chybějícím staged souboru (exit 100)
        WriteSrc("B.dll", "new");
        var hash = Sha256OfContent("B.dll");
        WriteCrc(new CrcRecord("B.dll", "", hash), new CrcRecord("B.dll ", "", hash));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "B.dll")));
    }

    [Fact]
    public void Run_ReadOnlyTargetFile_Updated_ExitOk()
    {
        WriteSrc("A.dll", "new");
        WriteTgt("A.dll", "old");
        File.SetAttributes(Path.Combine(_tgt, "A.dll"), FileAttributes.ReadOnly);

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.False(File.GetAttributes(Path.Combine(_tgt, "A.dll")).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void Run_SourceWithoutVerzeInf_Exit1()
    {
        File.Delete(Path.Combine(_src, "VERZE.INF"));
        WriteSrc("A.dll", "new");

        var result = Engine(version: "999999.1").Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_TargetNestedInSource_Exit1()
    {
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _root, TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_SourceEqualsTargetWithTrailingSlash_Exit1()
    {
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _tgt + Path.DirectorySeparatorChar, TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_WaitProcessWithExeSuffix_StillWaits()
    {
        WriteSrc("A.dll", "new");
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, WaitProcess: "JinyProces.exe"),
            new UpdateLog(_tgt))
        {
            WaitTimeoutMs = 500,
            ProcessCountFor = name => name == "JinyProces" ? 1 : 0,
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("JinyProces", result.Message);
    }

    [Fact]
    public void Run_RunExeWithPath_Exit1()
    {
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, RunExe: @"..\evil.exe"),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_CancelDuringStaging_Exit1_TargetUntouchedStagingCleaned()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        using var cancelSource = new CancellationTokenSource();
        var engine = new TestEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt),
            new UpdateLog(_tgt),
            cancelSource.Token);
        engine.AfterCopy = _ => cancelSource.Cancel(); // zruš po první stagované kopii

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
        Assert.False(File.Exists(Path.Combine(_tgt, "B.dll")));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }
}
