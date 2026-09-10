using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

/// <summary>
/// Fáze a cancel: CurrentPhase je autoritativní stav pro "smí se okno zavřít?"
/// (UpdateWindow.InCriticalPhase) a cancel token smí přerušit jen fáze před swapem.
/// </summary>
public class UpdatePhaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _src;
    private readonly string _tgt;

    public UpdatePhaseTests()
    {
        _src = Path.Combine(_root, "src");
        _tgt = Path.Combine(_root, "tgt");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_tgt);
        File.WriteAllText(Path.Combine(_src, "VERZE.INF"), "LAST=260825.2\n");
        File.WriteAllText(Path.Combine(_tgt, "VERZE.INF"), "LAST=260825.1\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteSrc(string relative, string content) =>
        File.WriteAllText(Path.Combine(_src, relative), content);

    private void WriteTgt(string relative, string content) =>
        File.WriteAllText(Path.Combine(_tgt, relative), content);

    private RecordingEngine Engine(InlineProgress? progress = null, CancellationToken token = default) =>
        new(new UpdaterArgs(SourcePath: _src, TargetPath: _tgt), new UpdateLog(_tgt), progress, token);

    private static UpdatePhase[] DistinctPhases(InlineProgress progress) =>
        progress.Reports.Select(r => r.Phase)
            .Aggregate(new List<UpdatePhase>(), (acc, p) =>
            {
                if (acc.Count == 0 || acc[^1] != p) acc.Add(p);
                return acc;
            })
            .ToArray();

    [Fact]
    public void Run_ReportsPhasesInOrder_PercentMonotonicUpTo100()
    {
        WriteSrc("A.dll", "new");
        var progress = new InlineProgress();

        var result = Engine(progress).Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(
            [UpdatePhase.Validation, UpdatePhase.Selection, UpdatePhase.Staging, UpdatePhase.Swap, UpdatePhase.Done],
            DistinctPhases(progress));
        var percents = progress.Reports.Where(r => r.Percent >= 0).Select(r => r.Percent).ToList();
        Assert.NotEmpty(percents);
        Assert.Equal(percents.OrderBy(p => p), percents);
        var last = progress.Reports[^1];
        Assert.Equal(UpdatePhase.Done, last.Phase);
        Assert.Equal(100, last.Percent);
    }

    [Fact]
    public void Run_BusyProcessAtFirst_ReportsWaitingForProcess()
    {
        WriteSrc("A.dll", "new");
        var progress = new InlineProgress();
        var engine = Engine(progress);
        int polls = 0;
        engine.ProcessCountFor = name => name == "EDISONMAIN" && ++polls <= 2 ? 1 : 0;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(
            [UpdatePhase.Validation, UpdatePhase.WaitingForProcess, UpdatePhase.Selection,
             UpdatePhase.Staging, UpdatePhase.Swap, UpdatePhase.Done],
            DistinctPhases(progress));
    }

    [Fact]
    public void CurrentPhase_DuringSwapMove_IsSwap()
    {
        WriteSrc("A.dll", "new");
        var engine = Engine();
        UpdatePhase? seen = null;
        engine.AfterMove = (_, _) => seen ??= engine.CurrentPhase;

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(UpdatePhase.Swap, seen);
    }

    [Fact]
    public void CurrentPhase_DuringRollbackRestore_IsRollback()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        WriteTgt("A.dll", "oldA");
        var engine = Engine();
        string failTarget = Path.Combine(_tgt, "B.dll");
        engine.MoveShouldFail = (_, target) => string.Equals(target, failTarget, StringComparison.OrdinalIgnoreCase);
        UpdatePhase? seenOnRestore = null;
        engine.AfterCopy = (source, _) =>
        {
            // Kopie ZE zálohy zpět do cíle = obnova; kopie DO zálohy běží ještě ve fázi Swap
            if (source.Contains(UpdateEngine.BackupDirName)) seenOnRestore ??= engine.CurrentPhase;
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitFailedRestored, result.ExitCode);
        Assert.Equal(UpdatePhase.Rollback, seenOnRestore);
    }

    [Fact]
    public void Cancel_DuringSwap_DoesNotInterrupt_AllFilesSwapped()
    {
        // SwapFiles záměrně nemá cancel checkpoint - přerušení uprostřed výměny by nechalo
        // cíl nekonzistentní; cancel vyžádaný během swapu se ignoruje a update dojede
        WriteSrc("A.dll", "newA");
        WriteSrc("B.dll", "newB");
        using var cancelSource = new CancellationTokenSource();
        var engine = Engine(token: cancelSource.Token);
        engine.AfterMove = (_, _) => cancelSource.Cancel();

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("newA", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("newB", File.ReadAllText(Path.Combine(_tgt, "B.dll")));
        Assert.Equal("LAST=260825.2\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
    }

    [Fact]
    public void Cancel_DuringWaitForProcesses_Exit1_TargetUntouched()
    {
        WriteSrc("A.dll", "new");
        using var cancelSource = new CancellationTokenSource();
        var engine = Engine(token: cancelSource.Token);
        engine.ProcessCountFor = name =>
        {
            if (name == "EDISONMAIN") cancelSource.Cancel();
            return name == "EDISONMAIN" ? 1 : 0;
        };

        var result = engine.Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("přerušena", result.Message);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("LAST=260825.1\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
    }
}
