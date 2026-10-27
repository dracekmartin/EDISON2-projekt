using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Legacy WinForms agendy z externích .exe: spuštění přímo nebo přes generický host
/// <c>EDISONMAIN2API.exe</c> (host agendu načte přes <c>Assembly.LoadFrom</c> - agenda nepotřebuje retrofit)
/// a životní cyklus procesů (dedup, hlášení pádu, ztmavení shellu, kill). Fasáda je <see cref="WindowNavigator"/>.
/// </summary>
internal sealed class LegacyModuleHost
{
    /// <summary>Cíl deploye legacy agend; single-file publish profily tam zapisují.</summary>
    internal const string DefaultDeployRoot = @"C:\Edison";

    /// <summary>Generický host, který agendu načte a spustí (viz EDISONMAIN2API). Spouští se
    /// místo agenda .exe; cesta k agendě jde do argů, takže agenda nemusí být retrofitnutá.</summary>
    internal const string DefaultLauncherExe = "EDISONMAIN2API.exe";

    private readonly string _launcherExe;

    /// <summary>Jak dlouho shell čeká na první okno modulu. Teprve potom smí proces bez okna nabídnout ukončit.</summary>
    internal static readonly TimeSpan ModuleWindowTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Jedno spuštění. FormFqn slouží i jako fallback label, když <c>MainWindowTitle</c> je
    /// prázdný (typicky brzy po startu, než child vytvoří okno).</summary>
    internal sealed class LegacyLaunch(ILegacyProcess process, string exeName, string formFqn, string inputParam,
                                       DateTime startedUtc = default)
    {
        public ILegacyProcess Process { get; } = process;
        public string ExeName { get; } = exeName;
        public string FormFqn { get; } = formFqn;
        public string InputParam { get; } = inputParam;
        public DateTime StartedUtc { get; } = startedUtc;

        /// <summary>Zabil ho shell sám (<see cref="CloseAll"/>) - takový nenulový exit kód není pád.</summary>
        public volatile bool KilledByShell;
    }

    private readonly List<LegacyLaunch> _tracked = [];
    private readonly Func<EDServices> _services;
    private readonly Func<ProcessStartInfo, ILegacyProcess?> _start;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, string, int, string?, TimeSpan> _reportExit;
    private readonly Func<int, DateTime, string?> _takeCrashDetail;
    private readonly Action<string, string> _trackUsage;
    private readonly Action<Action> _postToUi;
    private readonly Func<Window?> _dialogOwner;
    private readonly Func<Window?, IDisposable> _dim;
    private readonly Action<IntPtr> _focus;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<string?> _shellMonitor;

    /// <summary>Ctor nic nevolá - services se čtou až při launchi (shell je skládá po navigatoru).</summary>
    internal LegacyModuleHost(
        Func<EDServices> services,
        Func<ProcessStartInfo, ILegacyProcess?>? start = null,
        Func<string, bool>? fileExists = null,
        Action<string, string, int, string?, TimeSpan>? reportExit = null,
        Func<int, DateTime, string?>? takeCrashDetail = null,
        Action<string, string>? trackUsage = null,
        Action<Action>? postToUi = null,
        Func<Window?>? dialogOwner = null,
        Func<Window?, IDisposable>? dim = null,
        Action<IntPtr>? focus = null,
        string launcherExe = DefaultLauncherExe,
        Func<DateTime>? utcNow = null,
        Func<string?>? shellMonitor = null)
    {
        _services = services;
        _launcherExe = launcherExe;
        _start = start ?? SystemLegacyProcess.Start;
        _fileExists = fileExists ?? File.Exists;
        _reportExit = reportExit ?? ((exe, form, code, detail, uptime) =>
            ErrorReporting.ErrorReports.Pipeline.ReportLegacyExit(exe, form, code, detail, uptime));
        _takeCrashDetail = takeCrashDetail ?? ErrorReporting.LegacyCrashDetail.Take;
        _trackUsage = trackUsage ?? EDUsageTelemetry.Track;
        // Konec procesu chodí z thread poolu; při shutdownu (Application.Current null) se zahodí.
        _postToUi = postToUi ?? (action => Application.Current?.Dispatcher.BeginInvoke(action));
        // Během startu shellu je MainWindow ještě login okno.
        _dialogOwner = dialogOwner ?? (() => Application.Current?.MainWindow);
        _dim = dim ?? WindowDimmer.Dim;
        _focus = focus ?? ProcessWindows.Activate;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        // Okno modulu bez uložené pozice se otevře na monitoru shellu, ne pod kurzorem nebo na hlavním monitoru.
        _shellMonitor = shellMonitor ?? (() => Application.Current is { } app && app.Dispatcher.CheckAccess()
            ? MonitorPlacement.DeviceNameOf(app.MainWindow)
            : null);
    }

    public event Action? LegacyModuleExited;

    public void ShowLegacy(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        if (TryFocusExisting(exeName, formFqn, inputParam)) return;
        var launch = Launch(exeName, formFqn, shareShellSession, inputParam);
        if (launch is null) return;
        TrackModeless(launch);
    }

    public async Task ShowLegacyAsync(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        // Už běžící modul (stejné exe+forma+param) neotvírej znovu - jen na něj přenes focus.
        if (TryFocusExisting(exeName, formFqn, inputParam)) return;
        // Launch synchronně (případný fail Process.Start faultne task → caller ho chytne v popup).
        var launch = Launch(exeName, formFqn, shareShellSession, inputParam);
        if (launch is null) return;
        TrackModeless(launch);
        await WaitForModuleWindowAsync(launch.Process);
    }

    /// <summary>
    /// Zástupce na ploše (tisková sestava, uložený pohled) - host místo formy zavolá proceduru agendy.
    /// Záměrně bez dedup: dvě spuštění téže sestavy jsou dva legitimní tisky, ne duplikát okna. Proces
    /// se ze stejného důvodu netrackuje - není to modul a v „Spuštěné moduly" nemá co dělat.
    /// </summary>
    public async Task ShowLegacyShortcutAsync(string exeName, string formFqn, string shortcutPayload,
                                              bool shareShellSession, string inputParam)
    {
        var launch = Launch(exeName, formFqn, shareShellSession, inputParam, shortcutPayload);
        if (launch is null) return;
        await WaitForModuleWindowAsync(launch.Process);
    }

    public void ShowLegacyAndWait(string exeName, string formFqn, bool shareShellSession, string inputParam) =>
        Launch(exeName, formFqn, shareShellSession, inputParam)?.Process.WaitForExit();

    public void ShowLegacyAsDialog(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        var launch = Launch(exeName, formFqn, shareShellSession, inputParam);
        if (launch is null) return;
        _tracked.Add(launch);
        WireDialogBehavior(launch.Process);
    }

    public async Task ShowLegacyAsDialogAsync(string exeName, string formFqn, bool shareShellSession, string inputParam)
    {
        var launch = Launch(exeName, formFqn, shareShellSession, inputParam);
        if (launch is null) return;
        _tracked.Add(launch);
        WireDialogBehavior(launch.Process);
        // Overlay (caller drží ActionInProgress) se schová jakmile okno modulu naběhne;
        // shell zůstane ztmavený přes WireDialogBehavior až do zavření child okna.
        await WaitForModuleWindowAsync(launch.Process);
    }

    /// <summary>Kill tracked child procesů (zástupci a <c>AndWait</c> se netrackují).</summary>
    public void CloseAll()
    {
        foreach (var launch in _tracked)
        {
            try
            {
                if (launch.Process.HasExited) continue;
                launch.KilledByShell = true;
                launch.Process.Kill();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing process: {ex.Message}");
            }
        }
        _tracked.Clear();
    }

    /// <summary>Běžící tracked moduly pro menu „Spuštěné moduly".</summary>
    public IReadOnlyList<RunningModuleInfo> GetRunningModules() =>
        _tracked
            .Where(e => !e.Process.HasExited)
            .Select(e => new RunningModuleInfo(
                DisplayName: FormatLegacyLabel(e),
                Activate: () =>
                {
                    if (e.Process.HasExited) return;
                    var hwnd = e.Process.FindWindow();
                    if (hwnd != IntPtr.Zero) _focus(hwnd);
                    // Bez nového spuštění: záznam nenese shareShellSession ani to, zda šlo o dialogový launch.
                    else if (IsPastWindowTimeout(e)) KillIfConfirmed(e, "Ukončit ho?");
                })
            {
                ExeName = e.ExeName,
                FormFqn = e.FormFqn,
                InputParam = e.InputParam,
            })
            .ToList();

    /// <summary>Agendy zapisují zástupce na plochu přímo do Caché, konec modulu je chvíle je přenačíst.</summary>
    private void TrackModeless(LegacyLaunch launch)
    {
        _tracked.Add(launch);
        launch.Process.OnExited(_ => _postToUi(() => LegacyModuleExited?.Invoke()));
    }

    /// <summary>
    /// Dedup modeless launche: běží-li už stejné exe+forma+param, přenese focus a vrátí <c>true</c>.
    /// Mrtvé procesy cestou vyčistí. <c>true</c> i když okno ještě nenaběhlo - duplikát se spustit nesmí.
    /// Proces bez okna i po <see cref="ModuleWindowTimeout"/> nabídne ukončit, po souhlasu vrátí <c>false</c>
    /// a volající spustí nový.
    /// </summary>
    private bool TryFocusExisting(string exeName, string formFqn, string inputParam)
    {
        _tracked.RemoveAll(e => e.Process.HasExited);
        var match = _tracked.FirstOrDefault(e => MatchesTarget(e, exeName, formFqn, inputParam));
        if (match is null) return false;
        var hwnd = match.Process.FindWindow();
        if (hwnd != IntPtr.Zero)
        {
            _focus(hwnd);
            return true;
        }
        return !IsPastWindowTimeout(match) || !KillIfConfirmed(match, "Ukončit ho a spustit znovu?");
    }

    private bool IsPastWindowTimeout(LegacyLaunch launch) => _utcNow() - launch.StartedUtc >= ModuleWindowTimeout;

    /// <summary>
    /// Legacy proces může přežít svůj formulář nebo čekat na dialog, který uživatel nenajde. Bez ukončení
    /// by modul nešel otevřít až do restartu shellu. Vrátí <c>true</c>, když uživatel souhlasil.
    /// </summary>
    private bool KillIfConfirmed(LegacyLaunch launch, string question)
    {
        if (!_services().PopupDisplayer.Confirm(
                "Modul je spuštěný, ale nemá žádné okno. Možná se ještě načítá, nebo se zasekl." +
                Environment.NewLine + Environment.NewLine + question))
            return false;
        // Modul mohl naběhnout, zatímco dotaz visel.
        var hwnd = launch.Process.FindWindow();
        if (hwnd != IntPtr.Zero)
        {
            _focus(hwnd);
            return false;
        }
        if (!launch.Process.HasExited)
        {
            launch.KilledByShell = true;
            try
            {
                launch.Process.Kill();
            }
            catch
            {
                // Kill na skončený proces nehází, výjimka = proces běží dál a jeho pád se hlásit musí.
                launch.KilledByShell = false;
                throw;
            }
        }
        // Po Kill proces ještě chvíli nemá HasExited, dedup by ho jinak našel znovu.
        _tracked.Remove(launch);
        return true;
    }

    /// <summary>„Stejné okno" = stejné exe (case-insensitive path), forma i EDParamIn.</summary>
    internal static bool MatchesTarget(LegacyLaunch e, string exe, string form, string param) =>
        string.Equals(e.ExeName, exe, StringComparison.OrdinalIgnoreCase)
        && string.Equals(e.FormFqn, form, StringComparison.Ordinal)
        && string.Equals(e.InputParam, param, StringComparison.Ordinal);

    /// <summary>
    /// Čeká (poll á 100 ms), až child ukáže první okno, nebo do timeoutu / ukončení procesu. Await drží
    /// UI thread volný, overlay se stihne vykreslit. Timeout kryje moduly, které skončí bez okna.
    /// </summary>
    private static async Task WaitForModuleWindowAsync(ILegacyProcess prc)
    {
        for (int elapsed = 0; elapsed < ModuleWindowTimeout.TotalMilliseconds; elapsed += 100)
        {
            if (prc.HasExited) return;
            if (prc.FindWindow() != IntPtr.Zero) return;
            await Task.Delay(100);
        }
    }

    /// <summary>Modal-like chování pro child proces: ztmaví owner shell (WindowDimmer) po dobu
    /// běhu childu a bounce jeho okna do popředí při kliku na ztmavený shell.</summary>
    private void WireDialogBehavior(ILegacyProcess prc)
    {
        var owner = _dialogOwner();
        var undim = _dim(owner);

        // Dim adorner klik spolkne, ale aktivace okna proběhne → Activated fíruje.
        EventHandler? bounce = null;
        if (owner != null)
        {
            bounce = (_, _) =>
            {
                if (prc.HasExited) return;
                var hwnd = prc.FindWindow();
                if (hwnd != IntPtr.Zero) _focus(hwnd);
            };
            owner.Activated += bounce;
        }

        prc.OnExited(_ => _postToUi(() =>
        {
            if (owner != null && bounce != null) owner.Activated -= bounce;
            undim.Dispose();
            if (owner is { IsLoaded: true }) owner.Activate();
        }));
    }

    private LegacyLaunch? Launch(string exeName, string formFqn, bool shareShellSession,
                                 string inputParam, string shortcutPayload = "")
    {
        var services = _services();
        var mlRest = services.MLRest
            ?? throw new InvalidOperationException("Legacy launch requires EDServices.MLRest.");
        // Launch modulu jako usage telemetrie shellu. Záměrně ne (formFqn, "OPEN") -
        // hostovaná legacy forma si OPEN trackne sama přes EDForm, jinak by byl dvakrát.
        _trackUsage($"{services.FormIdPrefix}.LegacyLaunch", string.IsNullOrEmpty(formFqn) ? exeName : formFqn);
        var shellGuid = ShellGuidFor(mlRest, shareShellSession);
        // Chybějící soubor by skončil Win32Exception bez jména souboru; správce IS potřebuje vědět, co chybí.
        var agendaPath = ResolveExistingPath(exeName, _fileExists, DefaultDeployRoot);

        // EDISONMAIN se spouští napřímo (ne přes host): jeho ApplicationEvents.OnRun nastavuje
        // per-forma default EDParamIn (ApplyDefaultInput), který generický host neumí dodat.
        bool direct = LaunchesDirectly(exeName);
        var launcherPath = direct ? agendaPath : ResolveExistingPath(_launcherExe, _fileExists, DefaultDeployRoot);

        var startedUtc = _utcNow();
        var prc = _start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = launcherPath,
            Arguments = (LaunchArgsFor(services, mlRest, formFqn, shellGuid, inputParam,
                direct ? "" : agendaPath, shortcutPayload) with { ShellMonitor = _shellMonitor() ?? "" }).Format(),
        });
        if (prc is null) return null;

        var launch = new LegacyLaunch(prc, exeName, formFqn, inputParam, startedUtc);
        var pid = prc.Id;
        // Tvrdý pád hosta (host sám dosáhne jen na managed výjimky - viz FormToRunGetter v repu EDISON).
        prc.OnExited(code =>
        {
            // Detail se odebere při každém konci, i nehlášeném (kill shellem uprostřed chybového dialogu).
            var crashDetail = _takeCrashDetail(pid, startedUtc);
            if (code is { } exitCode && ShouldReportExit(exitCode, launch.KilledByShell))
                _reportExit(exeName, formFqn, exitCode, crashDetail, _utcNow() - startedUtc);
        });
        return launch;
    }

    /// <summary>0 = normální konec i `Application.Exit`; náš `Kill` dává -1, ale poznává se podle příznaku,
    /// ne kódu - `taskkill /F` zvenku (kód 1) hlásit chceme.</summary>
    internal static bool ShouldReportExit(int exitCode, bool killedByShell) =>
        exitCode != 0 && !killedByShell;

    /// <summary>Aplikace, které se spouští napřímo místo přes generický host: legacy shell
    /// EDISONMAIN (vlastní retrofit, který host nereplikuje) a WPF TemplateDesigner
    /// (kontrakt si parsuje sám, WinForms host na něj nejde použít).</summary>
    internal static bool LaunchesDirectly(string exeName) =>
        string.Equals(Path.GetFileName(exeName), "EDISONMAIN.exe", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Path.GetFileName(exeName), "TemplateDesigner.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Shell GUID = modul jede s Locks=1 pod shellovým zámkem; bez zámku (podpora, selhaný ReacquireLock)
    /// by každý jeho request padl na LockExist, proto prázdný = vlastní GUID bez kontrol.
    /// </summary>
    internal static string ShellGuidFor(IMLRest mlRest, bool shareShellSession) =>
        shareShellSession && mlRest.CacheLocks == 1 ? mlRest.Guid : "";

    /// <summary>Session shellu → launch kontrakt; pozice, formát a sanitizace drží <see cref="EdisonLaunchArgs"/>.
    /// Zástupce skládá shell (zná jeho typ), sem přichází jako neprůhledný payload.</summary>
    internal static EdisonLaunchArgs LaunchArgsFor(EDServices services, IMLRest mlRest, string formFqn, string shellGuid,
                                                   string inputParam, string agendaPath, string shortcutPayload)
    {
        // Licence shellu → host z ní postaví Lic a načte formuláři přístupová práva (MLProps.AccessTable); bez nich
        // moduly gate-ující obsah přes RightGranted (DSINPUT Plánování) nezobrazí data. Viz FormToRunGetter.
        // Při retry nad limitem licencí během přepnutí firmy je to ještě licence původní firmy; spouští se
        // tehdy jen okno aktivity pro admina, které licenci nekontroluje.
        var licence = services.ActiveLicence;
        return new EdisonLaunchArgs(mlRest.Server, mlRest.Port, mlRest.DataNamespace, mlRest.Idfa, mlRest.LoginName,
                                    mlRest.FullName, mlRest.Mlp, formFqn, shellGuid, inputParam)
        {
            AgendaPath = agendaPath,
            LicensedElements = licence?.Elements is { } els ? string.Join(",", els) : "",
            ShortcutPayload = shortcutPayload,
            IsAdmin = services.IsAdmin,
            MayUpdate = services.MayUpdate,
            UserDirectory = services.UserDirectory,
            UserEmail = services.UserEmail,
            LicenceVersion = licence?.Version ?? "",
            LicenceState = licence is null ? "" : ((int)licence.State).ToString(CultureInfo.InvariantCulture),
            LicenceValidTo = licence?.ValidityTo ?? "",
        };
    }

    /// <summary>Vedle exe (DEBUG běží z bin), jinak deploy adresář legacy agend.</summary>
    internal static string ResolvePath(string relativePath, Func<string, bool> fileExists, string deployRoot)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        return fileExists(localPath) ? localPath : Path.Combine(deployRoot, relativePath);
    }

    /// <summary>
    /// <see cref="ResolvePath"/> s pre-flight kontrolou: hláška jmenuje soubor i oba prohledané adresáře -
    /// shell běžící z UNC sdílení hledá nejdřív vedle sebe, samotný fallback by správce poslal jinam.
    /// </summary>
    internal static string ResolveExistingPath(string relativePath, Func<string, bool> fileExists, string deployRoot)
    {
        var path = ResolvePath(relativePath, fileExists, deployRoot);
        if (fileExists(path)) return path;
        var searched = new[] { Path.Combine(AppContext.BaseDirectory, relativePath), path }
            .Select(Path.GetDirectoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        throw new FileNotFoundException(
            $"Nelze spustit modul: soubor {Path.GetFileName(relativePath)} chybí v adresáři {string.Join(" i ", searched)}. " +
            "Kontaktujte správce IS.", path);
    }

    /// <summary>Text v záhlaví okna, fallback poslední segment FormFqn (<c>formMHD2Main</c>), nebo EXE bez přípony.</summary>
    internal static string FormatLegacyLabel(LegacyLaunch entry)
    {
        var title = SafeGetTitle(entry.Process);
        if (!string.IsNullOrEmpty(title)) return title;
        var lastDot = entry.FormFqn.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < entry.FormFqn.Length - 1) return entry.FormFqn[(lastDot + 1)..];
        if (!string.IsNullOrEmpty(entry.FormFqn)) return entry.FormFqn;
        return Path.GetFileNameWithoutExtension(entry.ExeName);
    }

    /// <summary><c>Process</c> si titulek přečtený před vznikem okna uloží prázdný, čerstvý dá až <c>Refresh</c>.</summary>
    private static string SafeGetTitle(ILegacyProcess process)
    {
        try
        {
            process.Refresh();
            return process.MainWindowTitle;
        }
        catch { return ""; }
    }
}
