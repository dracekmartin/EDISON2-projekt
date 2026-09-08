using EDUPDATE2;

namespace EDUPDATE2.Tests;

/// <summary>
/// Rozhodovací logika okna bez WPF instance: RunExe se spouští jen po úspěchu
/// (po chybě nikdy - smyčka detekce) a zavření okna řídí třístavové DecideClose.
/// </summary>
public class UpdateWindowLogicTests
{
    [Theory]
    [InlineData(UpdateEngine.ExitOk, "EDISONMAIN2.exe", true)]
    [InlineData(UpdateEngine.ExitOk, "", false)]
    [InlineData(UpdateEngine.ExitBadParamsOrAborted, "EDISONMAIN2.exe", false)]
    [InlineData(UpdateEngine.ExitFailedRestored, "EDISONMAIN2.exe", false)]
    [InlineData(UpdateEngine.ExitFailedRestoreFailed, "EDISONMAIN2.exe", false)]
    public void ShouldRunExe_OnlyAfterSuccessWithExeName(int exitCode, string runExe, bool expected) =>
        Assert.Equal(expected, UpdateWindow.ShouldRunExe(exitCode, runExe));

    [Theory]
    [InlineData(UpdatePhase.Swap, false)]
    [InlineData(null, true)]
    public void DecideClose_Finished_Allows(UpdatePhase? phase, bool closeRequested) =>
        Assert.Equal(UpdateWindow.CloseDecision.Allow,
            UpdateWindow.DecideClose(finished: true, phase, closeRequested));

    [Theory]
    [InlineData(UpdatePhase.Swap, false)]
    [InlineData(UpdatePhase.Rollback, false)]
    [InlineData(UpdatePhase.Swap, true)]
    [InlineData(UpdatePhase.Staging, true)]
    public void DecideClose_CriticalPhaseOrAlreadyRequested_Blocks(UpdatePhase? phase, bool closeRequested) =>
        Assert.Equal(UpdateWindow.CloseDecision.Block,
            UpdateWindow.DecideClose(finished: false, phase, closeRequested));

    [Theory]
    [InlineData(UpdatePhase.Staging)]
    [InlineData(UpdatePhase.WaitingForProcess)]
    [InlineData(null)]
    public void DecideClose_FirstRequestOutsideCriticalPhase_CancelsAndBlocks(UpdatePhase? phase) =>
        Assert.Equal(UpdateWindow.CloseDecision.CancelAndBlock,
            UpdateWindow.DecideClose(finished: false, phase, closeRequested: false));

    [Fact]
    public void DecideClose_SecondRequestDuringCancellation_DoesNotCancelAgain()
    {
        // Idempotence: první zavření mimo kritickou fázi ruší engine (CancelAndBlock),
        // opakované zavření už jen blokuje - _cancelSource.Cancel() se nesmí volat dvakrát
        var first = UpdateWindow.DecideClose(finished: false, enginePhase: UpdatePhase.Staging, closeRequested: false);
        var second = UpdateWindow.DecideClose(finished: false, enginePhase: UpdatePhase.Staging, closeRequested: true);

        Assert.Equal(UpdateWindow.CloseDecision.CancelAndBlock, first);
        Assert.Equal(UpdateWindow.CloseDecision.Block, second);
    }
}
