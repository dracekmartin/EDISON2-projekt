using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Otevírá WPF okna registrovaná v <see cref="Registry"/> + legacy WinForms formuláře z externích
/// .exe (přes 10-arg kontrakt <c>EDISONMAIN2API.FormToRunGetter</c>). Trackuje child procesy,
/// <see cref="CloseAll"/> kill při shell close.
/// </summary>
public class WindowNavigator : IWindowNavigator
{
    /// <summary>
    /// Cíl deploye legacy `.exe` agend (`USERS.exe`, `MHD2PlanDopravy.exe`, ...).
    /// Single-file publish profiles tam zapisují, <see cref="ResolveExecutablePath"/>
    /// odtud čte fallback, když relativní path nenajde v <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public const string EdisonDeployRoot = @"C:\Edison";

    /// <summary>Sledování legacy child procesů — Process + původní spawn args (exe, formFqn).
    /// FormFqn slouží jako fallback display label, když <c>Process.MainWindowTitle</c> je prázdný
    /// (typicky brzy po startu child procesu, než se vytvoří window).</summary>
    internal record LegacyProcessEntry(Process Process, string ExeName, string FormFqn);

    private readonly List<LegacyProcessEntry> _legacyProcesses = [];

    /// <summary>
    /// Registr klíč → factory pro WPF okna. Naplňuje composition root při startupu;
    /// agendy si registrují vlastní okna v rámci svého bootstrap hooku.
    /// </summary>
    public WindowRegistry Registry { get; } = new();

    public void Show(string key, EDWindowInput? input = null)
    {
        var window = CreateWindow(key, input);
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    public bool? ShowDialog(string key, EDWindowInput? input = null)
    {
        var window = CreateWindow(key, input);
        window.Owner = Application.Current.MainWindow;
        return window.ShowDialog();
    }

    public void ShowLegacy(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        if (prc != null) _legacyProcesses.Add(new(prc, exeName, formFqn));
    }

    public void ShowLegacyAndWait(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        using var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        prc?.WaitForExit();
    }

    public void ShowLegacyAsDialog(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        if (prc == null) return;
        _legacyProcesses.Add(new(prc, exeName, formFqn));

        var owner = Application.Current.MainWindow;
        if (owner != null) owner.IsEnabled = false;

        // Klik na zašedlý shell → cross-process "bounce" child okna do popředí (modal-like UX).
        // Activated fire-uje při WPF window activation i když je IsEnabled=false.
        EventHandler? bounce = null;
        if (owner != null)
        {
            bounce = (_, _) =>
            {
                if (prc.HasExited) return;
                var hwnd = prc.MainWindowHandle;
                if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);
            };
            owner.Activated += bounce;
        }

        prc.EnableRaisingEvents = true;
        prc.Exited += (_, _) =>
        {
            // Exited fire-uje na thread pool; Application.Current je null pokud shell už doběhl
            // shutdown (typicky CloseAll killnul i tento proces). Nic dělat netřeba.
            var app = Application.Current;
            if (app is null) return;
            app.Dispatcher.BeginInvoke(() =>
            {
                if (owner != null && bounce != null) owner.Activated -= bounce;
                if (owner is { IsLoaded: true }) { owner.IsEnabled = true; owner.Activate(); }
            });
        };
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Zavře všechna modeless WPF okna (kromě <c>MainWindow</c>) + kill tracked legacy
    /// child procesy. Záměrně public na konkrétní třídě, ne na <see cref="IWindowNavigator"/> —
    /// shell-only operace, agenda VMs ji nemají vidět.
    /// </summary>
    public void CloseAll()
    {
        foreach (var w in Application.Current.Windows.OfType<Window>().ToList())
            if (w != Application.Current.MainWindow) w.Close();

        foreach (var entry in _legacyProcesses)
        {
            try
            {
                if (!entry.Process.HasExited) entry.Process.Kill();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing process: {ex.Message}");
            }
        }
        _legacyProcesses.Clear();
    }

    /// <summary>
    /// Snapshot právě běžících modulů — legacy child procesy + WPF okna (kromě shell
    /// <c>MainWindow</c>). Pro menu "Spuštěné moduly" (binding přes refresh při open).
    /// Záměrně public na konkrétní třídě, ne na <see cref="IWindowNavigator"/> — shell-only feature.
    /// </summary>
    public IReadOnlyList<RunningModuleInfo> GetActiveModules()
    {
        var legacy = _legacyProcesses
            .Where(e => !e.Process.HasExited)
            .Select(e => new RunningModuleInfo(
                DisplayName: FormatLegacyLabel(e),
                Activate: () =>
                {
                    if (e.Process.HasExited) return;
                    var hwnd = e.Process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);
                }));

        var wpf = Application.Current.Windows
            .OfType<Window>()
            .Where(w => w != Application.Current.MainWindow
                        && w.IsLoaded
                        && !IsInternalWindow(w))
            .Select(w => new RunningModuleInfo(
                DisplayName: !string.IsNullOrEmpty(w.Title) ? w.Title : w.GetType().Name,
                Activate: () =>
                {
                    if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                    w.Activate();
                    w.Focus();
                }));

        return [.. legacy, .. wpf];
    }

    /// <summary>Skip WPF/WebView2 interních oken (AdornerWindow atd.) — uživatelská okna nejsou v MS./System./Microsoft. namespace.</summary>
    internal static bool IsInternalWindow(Window w)
    {
        var ns = w.GetType().Namespace ?? "";
        return ns.StartsWith("MS.")
            || ns.StartsWith("System.")
            || ns.StartsWith("Microsoft.");
    }

    /// <summary>
    /// Display label pro legacy proces: preferuje <c>MainWindowTitle</c> (text v záhlaví okna,
    /// to co user vidí), fallback last segment z FormFqn (např. <c>formMHD2Main</c>), nebo
    /// název EXE bez přípony, když ani jedno není dostupné.
    /// </summary>
    internal static string FormatLegacyLabel(LegacyProcessEntry entry)
    {
        var title = SafeGetTitle(entry.Process);
        if (!string.IsNullOrEmpty(title)) return title;
        var lastDot = entry.FormFqn.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < entry.FormFqn.Length - 1) return entry.FormFqn[(lastDot + 1)..];
        if (!string.IsNullOrEmpty(entry.FormFqn)) return entry.FormFqn;
        return Path.GetFileNameWithoutExtension(entry.ExeName);
    }

    private static string SafeGetTitle(Process process)
    {
        try { return process.MainWindowTitle ?? ""; }
        catch { return ""; }
    }

    private static Process? LaunchLegacy(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        var mlRest = EDServices.Current.MLRest
            ?? throw new InvalidOperationException("Legacy launch requires EDServices.Current.MLRest.");
        var shellGuid = shareShellSession ? mlRest.Guid : "";
        var prc = Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = ResolveExecutablePath(exeName),
            Arguments = $"EDISONMAIN2 \"{mlRest.Server}\" \"{mlRest.Port}\" \"{mlRest.DataNamespace}\" \"{mlRest.Idfa}\" \"{mlRest.LoginName}\" \"{mlRest.FullName}\" \"{mlRest.Mlp}\" \"{formFqn}\" \"{shellGuid}\" \"{inputParam}\"",
        });
        if (prc != null) KillOnExit.Bind(prc);
        return prc;
    }

    private static string ResolveExecutablePath(string relativePath)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(localPath)) return localPath;
        return Path.Combine(EdisonDeployRoot, relativePath);
    }

    private Window CreateWindow(string key, EDWindowInput? input)
    {
        var window = Registry.Create(key, input);

        var idfa = EDServices.Current.MLRest?.Idfa;
        if (!string.IsNullOrEmpty(idfa))
            window.Title = $"{window.Title} [{idfa}]";

        // Před Show() napojí klávesy + persistenci stavu okna (SourceInitialized se vyvolá
        // synchronně uvnitř Show() ještě před prvním paintem — restore stihne aplikovat hodnoty).
        if (window.DataContext is EDViewModel vm)
            vm.AttachToWindow(window);

        return window;
    }
}
