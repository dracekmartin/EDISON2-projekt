using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Otevírá WPF okna (reflexe v entry assembly) i legacy WinForms formuláře z externích
/// .exe (přes 10-arg kontrakt <c>EDISONMAIN2API.FormToRunGetter</c>). Trackuje child procesy,
/// <see cref="CloseAllLegacy"/> kill při shell close.
/// </summary>
public class WindowNavigator : IWindowNavigator
{
    private readonly List<Process> _legacyProcesses = [];

    public void Show(string typeName, EDWindowInput? input = null) =>
        Show(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, input);

    public void Show(string typeName, string assemblyName, EDWindowInput? input = null)
    {
        var window = CreateWindow(typeName, assemblyName, input);
        window.Owner = Application.Current.MainWindow;
        window.Show();
    }

    public bool? ShowDialog(string typeName, EDWindowInput? input = null) =>
        ShowDialog(typeName, Assembly.GetEntryAssembly()!.GetName().Name!, input);

    public bool? ShowDialog(string typeName, string assemblyName, EDWindowInput? input = null)
    {
        var window = CreateWindow(typeName, assemblyName, input);
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

    private static Window CreateWindow(string typeName, string assemblyName, EDWindowInput? input)
    {
        var type = FindTypeInAssembly(typeName, assemblyName)
            ?? throw new InvalidOperationException(
                $"Window type '{typeName}' not found in assembly '{assemblyName}'.");
        var window = input is null
            ? (Window)Activator.CreateInstance(type)!
            : (Window)Activator.CreateInstance(type, input)!;

        var idfa = EDServices.Current.MLRest?.Idfa;
        if (!string.IsNullOrEmpty(idfa))
            window.Title = $"{window.Title} [{idfa}]";

        // Před Show() napojí klávesy + persistenci stavu okna (SourceInitialized se vyvolá
        // synchronně uvnitř Show() ještě před prvním paintem — restore stihne aplikovat hodnoty).
        if (window.DataContext is EDViewModel vm)
            vm.AttachToWindow(window);

        return window;
    }

    private static Type? FindTypeInAssembly(string typeName, string assemblyName)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName ||
                                 Path.GetFileNameWithoutExtension(a.Location) == assemblyName);
        if (loaded != null)
            return loaded.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

        var fileName = Path.HasExtension(assemblyName) ? assemblyName : assemblyName + ".dll";
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException(
                $"Assembly '{assemblyName}' not found in '{AppContext.BaseDirectory}'.");

        var asm = Assembly.LoadFrom(fullPath);
        return asm.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
    }
}
