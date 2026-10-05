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

    private RecordingEngine Engine(bool forced = false, string stampTime = "", string version = "")
    {
        var args = new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, Version: version,
            NucenyUpdate: forced, NucenyUpdateTime: stampTime);
        return new RecordingEngine(args, new UpdateLog(_tgt));
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
    public void Run_DiffMode_SkipsOwnExe()
    {
        // Věta pro vlastní exe s neexistujícím zdrojem - kopie by spadla, skip projde
        WriteCrc(new CrcRecord("EDUPDATE2.exe", "", new string('F', 64)));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
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
    public void Run_RollbackFailure_Exit200()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        WriteTgt("A.dll", "oldA");
        var engine = Engine();
        string failTarget = Path.Combine(_tgt, "B.dll");
        engine.MoveShouldFail = (_, target) => string.Equals(target, failTarget, StringComparison.OrdinalIgnoreCase);
        // Obnova = kopie ZE zálohy; záloha samotná (kopie DO ní) projít musí
        engine.CopyShouldFail = (source, _) => source.Contains(UpdateEngine.BackupDirName);

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitFailedRestoreFailed, result.ExitCode);
    }

    [Fact]
    public void Run_WaitTimeout_Exit1_NothingChanged()
    {
        WriteSrc("A.dll", "new");
        var engine = new RecordingEngine(
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
    public void Run_SourceEqualsTarget_Exit1()
    {
        var engine = new RecordingEngine(
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
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _root, TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_SourceEqualsTargetWithTrailingSlash_Exit1()
    {
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _tgt + Path.DirectorySeparatorChar, TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_WaitProcessWithExeSuffix_StillWaits()
    {
        WriteSrc("A.dll", "new");
        var engine = new RecordingEngine(
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
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, RunExe: @"..\evil.exe"),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
    }

    [Fact]
    public void Run_ForcedWithoutSourceVerzeInf_StillRuns_ExitOk()
    {
        // Hotfix vlna u zákazníka bez INF: vynucený update nesmí na chybějící VERZE.INF
        // spadnout - vlnu ukončuje razítko, ne posun verze
        File.Delete(Path.Combine(_src, "VERZE.INF"));
        WriteSrc("A.dll", "new");

        var result = Engine(forced: true, stampTime: "66234,60449").Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("66234,60449", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_CopyRetries_TransientFailures_ExitOk()
    {
        WriteSrc("A.dll", "new");
        var engine = Engine();
        int attempts = 0;
        engine.CopyShouldFail = (source, _) => source.EndsWith("A.dll") && ++attempts <= 4;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(5, attempts); // 4 selhání + úspěšný pátý (poslední povolený) pokus
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_CopyRetriesExhausted_Exit1_StagingCleaned()
    {
        WriteSrc("A.dll", "new");
        WriteTgt("A.dll", "old");
        var engine = Engine();
        engine.CopyShouldFail = (source, _) => source.EndsWith("A.dll");

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Equal("old", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void Run_WaitProcessDuplicatesFixedName_QueriedOnce()
    {
        // WaitProcess=edisonmain je duplikát fixního EDISONMAIN - Distinct ho nesmí ptát dvakrát
        WriteSrc("A.dll", "new");
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt, WaitProcess: "edisonmain.exe"),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(["EDISONMAIN", "EDUPDATE", "TemplateDesigner", "EDISONMAIN2API"], engine.ProcessQueries);
    }

    [Fact]
    public void Run_TemplateDesignerRunning_WaitTimeout_Exit1()
    {
        // TD spuštěný z legacy agendy shell při exitu nezavře a drží EDControls.dll - updater na něj čeká.
        WriteSrc("A.dll", "new");
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt),
            new UpdateLog(_tgt))
        {
            WaitTimeoutMs = 500,
            ProcessCountFor = name => name == "TemplateDesigner" ? 1 : 0,
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("TemplateDesigner", result.Message);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_EmptySourcePath_Exit1()
    {
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: "", TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("SourcePath", result.Message);
    }

    [Fact]
    public void Run_MissingSourceDir_Exit1()
    {
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: Path.Combine(_root, "neexistuje"), TargetPath: _tgt),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("Zdrojová cesta", result.Message);
    }

    [Fact]
    public void Run_MissingTargetDir_Exit1()
    {
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: Path.Combine(_root, "neexistuje")),
            new UpdateLog(_tgt));

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("Cílová cesta", result.Message);
    }

    [Fact]
    public void Run_FreeSpaceCheckThrows_WarnsAndContinues_ExitOk()
    {
        WriteSrc("A.dll", "new");
        var engine = Engine();
        engine.FreeBytesFor = _ => throw new IOException("síťový disk nedostupný");

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_ReadOnlySourceFile_StagedAttributeCleared_TargetWritable()
    {
        // RO ze serverového share se kopií propaguje do stagingu; bez vyčištění by
        // příští update spadl na přepisu cíle
        WriteSrc("A.dll", "new");
        File.SetAttributes(Path.Combine(_src, "A.dll"), FileAttributes.ReadOnly);

        var result = Engine().Run();

        File.SetAttributes(Path.Combine(_src, "A.dll"), FileAttributes.Normal); // úklid fixture
        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.False(File.GetAttributes(Path.Combine(_tgt, "A.dll")).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void Run_KeyFileAlreadyInManifest_CopiedOnce()
    {
        // EDISONini.xml nese manifest i keyFiles pojistka - druhá kopie by ve swapu
        // přesouvala už přesunutý staged soubor
        WriteSrc("EDISONini.xml", "config");
        WriteTgt("EDISONini.xml", "starý config");
        WriteCrc(new CrcRecord("EDISONini.xml", "", Sha256OfContent("EDISONini.xml")));
        var engine = Engine();

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("config", File.ReadAllText(Path.Combine(_tgt, "EDISONini.xml")));
        Assert.Equal(1, engine.Ops.Count(o => o.Kind == "copy"
            && o.Target!.Contains(UpdateEngine.StagingDirName)
            && o.Target.EndsWith("EDISONini.xml", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Run_ForcedStampWriteFails_StillExitOk()
    {
        // Nezapsané razítko jen znamená, že příští start spustí vynucenou vlnu znovu -
        // úspěšně vyměněné soubory se nesmí hlásit jako chyba
        WriteSrc("A.dll", "new");
        var engine = Engine(stampTime: "66234,60449");
        engine.ForcedStampWriteFails = true;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("", ForcedStamp.Read(_tgt));
    }

    [Fact]
    public void Run_CancelDuringStaging_Exit1_TargetUntouchedStagingCleaned()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        using var cancelSource = new CancellationTokenSource();
        var engine = new RecordingEngine(
            new UpdaterArgs(SourcePath: _src, TargetPath: _tgt),
            new UpdateLog(_tgt),
            cancellationToken: cancelSource.Token);
        engine.AfterCopy = (_, _) => cancelSource.Cancel(); // zruš po první stagované kopii

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
        Assert.False(File.Exists(Path.Combine(_tgt, "B.dll")));
        Assert.False(Directory.Exists(Path.Combine(_tgt, UpdateEngine.StagingDirName)));
    }
}
