using System.Diagnostics;
using EDControls.EDViewModel;

namespace EDControls.Tests;

/// <summary>Wrapper nad skutečným procesem: exit kód dorazí každému odběrateli, i tomu, kdo se přihlásí až po konci.</summary>
public class SystemLegacyProcessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static Process StartExit(int code) =>
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c exit {code}") { UseShellExecute = false, CreateNoWindow = true })!;

    private static Task<int?> NextExit(ILegacyProcess process)
    {
        var tcs = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OnExited(code => tcs.TrySetResult(code));
        return tcs.Task;
    }

    [Fact]
    public async Task OnExited_DeliversExitCode()
    {
        var process = new SystemLegacyProcess(StartExit(3));

        Assert.Equal(3, await NextExit(process).WaitAsync(Timeout));
    }

    [Fact]
    public async Task OnExited_AfterProcessEnded_StillDelivered()
    {
        // Dialogový launch se přihlašuje až po hlášení pádu; konec mezi nimi nesmí nechat shell ztmavený.
        var process = new SystemLegacyProcess(StartExit(3));
        await NextExit(process).WaitAsync(Timeout);

        Assert.Equal(3, await NextExit(process).WaitAsync(Timeout));
    }

    [Fact]
    public async Task Start_ReturnsWatchedProcess()
    {
        // Výchozí továrna hostu: bez ní by testy s fake procesem nepoznaly, že reálný launch nehlídá konec.
        var process = SystemLegacyProcess.Start(
            new ProcessStartInfo("cmd.exe", "/c exit 4") { UseShellExecute = false, CreateNoWindow = true });

        Assert.NotNull(process);
        Assert.Equal(4, await NextExit(process).WaitAsync(Timeout));
    }

    [Fact]
    public async Task WrappedAfterExit_StillDeliversExitCode()
    {
        var raw = StartExit(5);
        await raw.WaitForExitAsync().WaitAsync(Timeout);

        var process = new SystemLegacyProcess(raw);

        Assert.Equal(5, await NextExit(process).WaitAsync(Timeout));
    }
}
