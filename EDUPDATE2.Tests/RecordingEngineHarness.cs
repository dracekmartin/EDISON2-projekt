using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

internal sealed record FsOp(string Kind, string Path, string? Target);

/// <summary>
/// Jediný testovací harness enginu: op-recording, fail/after hooky, progress a cancel token.
/// Zaznamenává jen operace jdoucí přes seamy UpdateEngine - UpdateLog a zápis razítka jdou mimo.
/// </summary>
internal sealed class RecordingEngine : UpdateEngine
{
    public readonly List<FsOp> Ops = [];
    public readonly List<string> ProcessQueries = [];
    public Func<string, int>? ProcessCountFor;
    public Func<string, string, bool>? MoveShouldFail;
    public Func<string, string, bool>? CopyShouldFail;
    public Func<string, bool>? DeleteShouldFail;
    public Action<string, string>? AfterCopy;
    public Action<string, string>? AfterMove;
    public Func<string, long>? FreeBytesFor;
    public bool ForcedStampWriteFails;
    public long FreeBytes = long.MaxValue;

    public RecordingEngine(UpdaterArgs args, UpdateLog log,
        IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
        : base(args, log, progress, cancellationToken) { }

    protected override int GetProcessCount(string name)
    {
        ProcessQueries.Add(name);
        return ProcessCountFor?.Invoke(name) ?? 0;
    }

    protected override void Sleep(int milliseconds) { }
    protected override long GetFreeBytes(string path) => FreeBytesFor?.Invoke(path) ?? FreeBytes;

    protected override void CopyFile(string source, string target)
    {
        if (CopyShouldFail?.Invoke(source, target) == true) throw new IOException("testovací lock");
        Ops.Add(new("copy", source, target));
        base.CopyFile(source, target);
        AfterCopy?.Invoke(source, target);
    }

    protected override void MoveFile(string source, string target)
    {
        if (MoveShouldFail?.Invoke(source, target) == true) throw new IOException("testovací lock");
        Ops.Add(new("move", source, target));
        base.MoveFile(source, target);
        AfterMove?.Invoke(source, target);
    }

    protected override void DeleteFile(string path)
    {
        if (DeleteShouldFail?.Invoke(path) == true) throw new IOException("testovací lock");
        Ops.Add(new("delete", path, null));
        base.DeleteFile(path);
    }

    protected override void DeleteDirectory(string path)
    {
        Ops.Add(new("rmdir", path, null));
        base.DeleteDirectory(path);
    }

    protected override void CreateDirectory(string path)
    {
        Ops.Add(new("mkdir", path, null));
        base.CreateDirectory(path);
    }

    protected override void ClearReadOnly(string path)
    {
        Ops.Add(new("clearRO", path, null));
        base.ClearReadOnly(path);
    }

    protected override void WriteForcedStamp(string targetPath, string stamp)
    {
        if (ForcedStampWriteFails) throw new IOException("testovací lock");
        Ops.Add(new("stamp", targetPath, stamp));
        base.WriteForcedStamp(targetPath, stamp);
    }
}

/// <summary>Synchronní IProgress - reporty se sbírají hned, bez SynchronizationContextu.</summary>
internal sealed class InlineProgress : IProgress<UpdateProgress>
{
    public readonly List<UpdateProgress> Reports = [];
    public void Report(UpdateProgress value) => Reports.Add(value);
}
