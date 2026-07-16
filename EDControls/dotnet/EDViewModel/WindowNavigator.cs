using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Otevírá WPF okna (caller předá instanci přes <c>new</c>) + legacy WinForms formuláře
/// z externích .exe přes generický host <c>EDISONMAIN2API.exe</c> (12-arg kontrakt:
/// host agendu načte přes <c>Assembly.LoadFrom</c> a roztočí formu — agenda nepotřebuje
/// žádný retrofit). Trackuje child procesy, <see cref="CloseAll"/> kill při shell close.
/// </summary>
public class WindowNavigator : IWindowNavigator
{
    /// <summary>
    /// Cíl deploye legacy `.exe` agend (`USERS.exe`, `MHD2PlanDopravy.exe`, ...).
    /// Single-file publish profiles tam zapisují, <see cref="ResolveExecutablePath"/>
    /// odtud čte fallback, když relativní path nenajde v <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public const string EdisonDeployRoot = @"C:\Edison";

    /// <summary>Generický host, který agendu načte a spustí (viz EDISONMAIN2API). Spouští se
    /// místo agenda .exe; cesta k agendě jde do argů, takže agenda nemusí být retrofitnutá.</summary>
    private const string LauncherExe = "EDISONMAIN2API.exe";

    /// <summary>Sledování legacy child procesů — Process + původní spawn args (exe, formFqn).
    /// FormFqn slouží jako fallback display label, když <c>Process.MainWindowTitle</c> je prázdný
    /// (typicky brzy po startu child procesu, než se vytvoří window).</summary>
    internal record LegacyProcessEntry(Process Process, string ExeName, string FormFqn);

    private readonly List<LegacyProcessEntry> _legacyProcesses = [];

    public void Show(Window window)
    {
        WireUp(window);
        window.Show();
    }

    public bool? ShowDialog(Window window)
    {
        WireUp(window);
        // Vstup blokuje Win32 modalita sama; dim přidává jen vizuální závoj na owner.
        using (WindowDimmer.Dim(window.Owner))
            return window.ShowDialog();
    }

    public void ShowLegacy(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        if (prc != null) _legacyProcesses.Add(new(prc, exeName, formFqn));
    }

    public async Task ShowLegacyAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        // Launch synchronně (případný fail Process.Start faultne task → caller ho chytne v popup).
        var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        if (prc == null) return;
        _legacyProcesses.Add(new(prc, exeName, formFqn));
        await WaitForModuleWindowAsync(prc);
    }

    /// <summary>
    /// Čeká (poll á 100 ms) až child proces vytvoří hlavní okno, nebo do timeoutu / ukončení
    /// procesu. Await Task.Delay drží UI thread volný, takže overlay se stihne vykreslit.
    /// Timeout kryje moduly, které skončí bez okna (chyba, background task).
    /// </summary>
    private static async Task WaitForModuleWindowAsync(Process prc, int timeoutMs = 20000)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += 100)
        {
            if (prc.HasExited) return;
            prc.Refresh();
            if (prc.MainWindowHandle != IntPtr.Zero) return;
            await Task.Delay(100);
        }
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
        WireDialogBehavior(prc);
    }

    public async Task ShowLegacyAsDialogAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "")
    {
        var prc = LaunchLegacy(exeName, formFqn, shareShellSession, inputParam);
        if (prc == null) return;
        _legacyProcesses.Add(new(prc, exeName, formFqn));
        WireDialogBehavior(prc);
        // Overlay (caller drží ActionInProgress) se schová jakmile okno modulu naběhne;
        // shell zůstane ztmavený přes WireDialogBehavior až do zavření child okna.
        await WaitForModuleWindowAsync(prc);
    }

    /// <summary>Modal-like chování pro child proces: ztmaví owner shell (WindowDimmer) po dobu
    /// běhu childu a bounce jeho okna do popředí při kliku na ztmavený shell.</summary>
    private void WireDialogBehavior(Process prc)
    {
        var owner = Application.Current.MainWindow;
        var undim = WindowDimmer.Dim(owner);

        // Klik na ztmavený shell → cross-process "bounce" child okna do popředí (modal-like UX).
        // Dim adorner klik spolkne, ale window activation proběhne → Activated fire-uje.
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
                undim.Dispose();
                if (owner is { IsLoaded: true }) owner.Activate();
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
    internal static bool IsInternalWindow(Window w) => IsInternalNamespace(w.GetType().Namespace);

    internal static bool IsInternalNamespace(string? ns)
    {
        ns ??= "";
        return ns.StartsWith("MS.") || ns.StartsWith("System.") || ns.StartsWith("Microsoft.");
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
        // Launch modulu jako usage telemetrie shellu. Záměrně ne (formFqn, "OPEN") —
        // hostovaná legacy forma si OPEN trackne sama přes EDForm, jinak by byl dvakrát.
        EDUsageTelemetry.Track("EDISON2.LegacyLaunch", string.IsNullOrEmpty(formFqn) ? exeName : formFqn);
        var shellGuid = shareShellSession ? mlRest.Guid : "";
        var agendaPath = ResolveExecutablePath(exeName);

        // EDISONMAIN se spouští napřímo (ne přes host): jeho ApplicationEvents.OnRun nastavuje
        // per-forma default EDParamIn (ApplyDefaultInput), který generický host neumí dodat.
        // Ostatní agendy jdou přes host, takže nepotřebují retrofit.
        bool direct = LaunchesDirectly(exeName);
        // Licencované elementy z aktivní licence shellu → host z nich postaví Lic a načte
        // formuláři přístupová práva (MLProps.AccessTable). Bez toho moduly, které gate-ují
        // obsah přes RightGranted (např. DSINPUT Plánování), nezobrazí data. Viz FormToRunGetter.
        var licence = EDServices.Current.ActiveLicence;
        string licensedElements = licence?.Elements is { } els ? string.Join(",", els) : "";

        var prc = Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = direct ? agendaPath : ResolveExecutablePath(LauncherExe),
            Arguments = BuildLauncherArguments(mlRest, formFqn, shellGuid, inputParam, direct ? "" : agendaPath, licensedElements),
        });
        if (prc != null) KillOnExit.Bind(prc);
        return prc;
    }

    /// <summary>Agendy, které se spouští napřímo místo přes generický host (drží si vlastní
    /// retrofit s logikou, kterou host nereplikuje). Zatím jen legacy shell EDISONMAIN.</summary>
    internal static bool LaunchesDirectly(string exeName) =>
        string.Equals(Path.GetFileName(exeName), "EDISONMAIN.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Složí payload pro generický host: sentinel + sys kontext + formFqn + shellGuid +
    /// inputParam + cesta k agendě + licencované elementy. Pozice 1–11 jsou shodné s legacy
    /// kontraktem (takže `FormToRunGetter.TryGetFormFromArgs` retrofitnutých agend zůstává
    /// kompatibilní); agendaPath (host ho čte pro <c>Assembly.LoadFrom</c>) je [11],
    /// licencované elementy [12] (comma-separated, pro MLProps.AccessTable — starší host je ignoruje).
    /// </summary>
    internal static string BuildLauncherArguments(IMLRest mlRest, string formFqn, string shellGuid,
        string inputParam, string agendaPath, string licensedElements) =>
        $"EDISONMAIN2 \"{mlRest.Server}\" \"{mlRest.Port}\" \"{mlRest.DataNamespace}\" \"{mlRest.Idfa}\" \"{mlRest.LoginName}\" \"{mlRest.FullName}\" \"{mlRest.Mlp}\" \"{formFqn}\" \"{shellGuid}\" \"{inputParam}\" \"{agendaPath}\" \"{licensedElements}\"";

    public static string ResolveExecutablePath(string relativePath)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(localPath)) return localPath;
        return Path.Combine(EdisonDeployRoot, relativePath);
    }

    // Sdílené wire-up před Show()/ShowDialog(): Owner, [Idfa] title suffix, AttachToWindow VM-if.
    // SourceInitialized fíruje synchronně uvnitř Show() před prvním paintem, restore stavu okna
    // tedy stihne aplikovat hodnoty.
    private static void WireUp(Window window)
    {
        window.Owner = Application.Current.MainWindow;

        var idfa = EDServices.Current.MLRest?.Idfa;
        if (!string.IsNullOrEmpty(idfa))
            window.Title = $"{window.Title} [{idfa}]";

        if (window.DataContext is EDViewModel vm)
            vm.AttachToWindow(window);
    }
}
