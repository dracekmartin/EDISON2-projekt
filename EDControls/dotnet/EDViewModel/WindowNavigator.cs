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
    private readonly List<Process> _legacyProcesses = [];

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
        if (prc != null) _legacyProcesses.Add(prc);
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
        _legacyProcesses.Add(prc);

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

        foreach (var process in _legacyProcesses)
        {
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing process: {ex.Message}");
            }
        }
        _legacyProcesses.Clear();
    }

    private static Process? LaunchLegacy(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        var mlRest = EDServices.Current.MLRest
            ?? throw new InvalidOperationException("Legacy launch requires EDServices.Current.MLRest.");
        var shellGuid = shareShellSession ? mlRest.Guid : "";
        return Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = ResolveExecutablePath(exeName),
            Arguments = $"EDISONMAIN2 \"{mlRest.Server}\" \"{mlRest.Port}\" \"{mlRest.DataNamespace}\" \"{mlRest.Idfa}\" \"{mlRest.LoginName}\" \"{mlRest.FullName}\" \"{mlRest.Mlp}\" \"{formFqn}\" \"{shellGuid}\" \"{inputParam}\"",
        });
    }

    private static string ResolveExecutablePath(string relativePath)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(localPath)) return localPath;
        return Path.Combine(@"C:\Edison", relativePath);
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
