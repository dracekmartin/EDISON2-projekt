using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EDControls.EDViewModel;

/// <summary>Spuštěný legacy proces, jak ho vidí <see cref="LegacyModuleHost"/> - šev pro testy bez skutečného procesu.</summary>
internal interface ILegacyProcess
{
    int Id { get; }

    bool HasExited { get; }

    /// <summary>Okno, které má shell vytáhnout dopředu (<see cref="ProcessWindows.Find"/>), 0 = proces žádné viditelné nemá.</summary>
    IntPtr FindWindow();

    /// <summary>Cachovaná hodnota jako u <see cref="Process"/> - čerstvou dá až <see cref="Refresh"/>.</summary>
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

    /// <summary>0 = nezjistitelný; slouží jen k dohledání detailu pádu, launch kvůli němu padat nesmí.</summary>
    public int Id
    {
        get
        {
            try { return _process.Id; }
            catch { return 0; }
        }
    }

    public bool HasExited => _process.HasExited;
    public IntPtr FindWindow() => ProcessWindows.Find(Id);
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

/// <summary>
/// Okna cizího procesu bez cache <see cref="Process"/>. <c>Process.MainWindowHandle</c> nestačí: bere jen okno
/// bez vlastníka, takže dialog, který legacy agenda otevře ještě před svým formulářem (vlastník skrytý nebo
/// neviditelný), shell nenajde a modul zůstane ve „Spuštěných" bez okna. Parita s <c>AppActivate(pid)</c> starého shellu.
/// </summary>
internal static class ProcessWindows
{
    private const int SW_RESTORE = 9;

    /// <summary>Nejvyšší viditelné povolené okno procesu (modální dialog před zablokovaným formulářem),
    /// jinak nejvyšší viditelné, jinak 0. <c>EnumWindows</c> jde v Z-pořadí shora.</summary>
    public static IntPtr Find(int pid)
    {
        if (pid == 0) return IntPtr.Zero;
        IntPtr enabled = IntPtr.Zero, visible = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner != (uint)pid || !IsWindowVisible(hwnd)) return true;
            if (visible == IntPtr.Zero) visible = hwnd;
            if (!IsWindowEnabled(hwnd)) return true;
            enabled = hwnd;
            return false;
        }, IntPtr.Zero);
        return enabled != IntPtr.Zero ? enabled : visible;
    }

    /// <summary>Samotné <c>SetForegroundWindow</c> minimalizované okno jen aktivuje, na ploše zůstane schované.
    /// Obnova přes <c>ShowWindowAsync</c>: synchronní <c>ShowWindow</c> čeká na UI vlákno modulu a zaseknutý modul by zamrazil shell.</summary>
    public static void Activate(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindowAsync(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hwnd, int cmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
