using System.Diagnostics;

namespace EDControls.EDViewModel;

/// <summary>Spuštěný legacy proces, jak ho vidí <see cref="LegacyModuleHost"/> - šev pro testy bez skutečného procesu.</summary>
internal interface ILegacyProcess
{
    bool HasExited { get; }

    /// <summary>Cachovaná hodnota jako u <see cref="Process"/> - čerstvou dá až <see cref="Refresh"/>.</summary>
    IntPtr MainWindowHandle { get; }

    string MainWindowTitle { get; }

    void Refresh();

    void Kill();

    void WaitForExit();

    /// <summary>
    /// Zavolá <paramref name="callback"/> s exit kódem (null = nezjistitelný), u už skončeného procesu
    /// hned. Chodí z thread poolu, každé přihlášení dostane konec právě jednou.
    /// </summary>
    void OnExited(Action<int?> callback);
}

/// <summary>
/// <see cref="ILegacyProcess"/> nad <see cref="Process"/>. K <c>Exited</c> se přihlásí jednou hned po startu:
/// <c>Process</c> ho vyvolá jen jednou a pozdější odběratel by konec procesu, který mezitím skončil, nikdy
/// neuslyšel (shell by zůstal ztmavený). Proces se nikdy nedisposuje - exit kód se čte až po konci.
/// </summary>
internal sealed class SystemLegacyProcess : ILegacyProcess
{
    private readonly Process _process;
    private readonly TaskCompletionSource<int?> _exit = new();

    public SystemLegacyProcess(Process process)
    {
        _process = process;
        try
        {
            // Pořadí subscribe → EnableRaisingEvents: proces skončený mezi nimi by událost ztratil.
            process.Exited += (_, _) => _exit.TrySetResult(ReadExitCode());
            process.EnableRaisingEvents = true;
            if (process.HasExited) _exit.TrySetResult(ReadExitCode());
        }
        catch { /* best-effort: bez exit watche jen nepřijde hlášení pádu */ }
    }

    public bool HasExited => _process.HasExited;
    public IntPtr MainWindowHandle => _process.MainWindowHandle;
    public string MainWindowTitle => _process.MainWindowTitle ?? "";
    public void Refresh() => _process.Refresh();
    public void Kill() => _process.Kill();
    public void WaitForExit() => _process.WaitForExit();

    public void OnExited(Action<int?> callback) =>
        _exit.Task.ContinueWith(t =>
        {
            try { callback(t.Result); }
            catch { /* reakce na konec modulu nesmí shodit vlákno poolu */ }
        }, TaskScheduler.Default);

    private int? ReadExitCode()
    {
        try { return _process.ExitCode; }
        catch { return null; }
    }

    /// <summary>Výchozí továrna <see cref="LegacyModuleHost"/>: start + vazba na job object shellu (umře s ním).</summary>
    internal static ILegacyProcess? Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        if (process is null) return null;
        KillOnExit.Bind(process);
        return new SystemLegacyProcess(process);
    }
}
