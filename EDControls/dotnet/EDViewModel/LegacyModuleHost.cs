using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Legacy WinForms agendy z externích .exe: spuštění přímo nebo přes generický host
/// <c>EDISONMAIN2API.exe</c> (host agendu načte přes <c>Assembly.LoadFrom</c> - agenda nepotřebuje retrofit)
/// a životní cyklus procesů (dedup, hlášení pádu, ztmavení shellu, kill). Fasáda je <see cref="WindowNavigator"/>.
/// </summary>
internal sealed class LegacyModuleHost
{
    /// <summary>Generický host, který agendu načte a spustí (viz EDISONMAIN2API). Spouští se
    /// místo agenda .exe; cesta k agendě jde do argů, takže agenda nemusí být retrofitnutá.</summary>
    private const string LauncherExe = "EDISONMAIN2API.exe";

    /// <summary>Jedno spuštění. FormFqn slouží i jako fallback label, když <c>MainWindowTitle</c> je
    /// prázdný (typicky brzy po startu, než child vytvoří okno).</summary>
    internal sealed class LegacyLaunch(ILegacyProcess process, string exeName, string formFqn, string inputParam)
    {
        public ILegacyProcess Process { get; } = process;
        public string ExeName { get; } = exeName;
        public string FormFqn { get; } = formFqn;
        public string InputParam { get; } = inputParam;

        /// <summary>Zabil ho shell sám (<see cref="CloseAll"/>) - takový nenulový exit kód není pád.</summary>
        public volatile bool KilledByShell;
    }

    private readonly List<LegacyLaunch> _tracked = [];
    private readonly Func<EDServices> _services;
    private readonly Func<ProcessStartInfo, ILegacyProcess?> _start;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, string, int> _reportExit;
    private readonly Action<string, string> _trackUsage;
    private readonly Action<Action> _postToUi;
    private readonly Func<Window?> _dialogOwner;
    private readonly Func<Window?, IDisposable> _dim;
    private readonly Action<IntPtr> _focus;

    /// <summary>Ctor nic nevolá - services se čtou až při launchi (shell je skládá po navigatoru).</summary>
    internal LegacyModuleHost(
        Func<EDServices> services,
        Func<ProcessStartInfo, ILegacyProcess?>? start = null,
        Func<string, bool>? fileExists = null,
        Action<string, string, int>? reportExit = null,
        Action<string, string>? trackUsage = null,
        Action<Action>? postToUi = null,
        Func<Window?>? dialogOwner = null,
        Func<Window?, IDisposable>? dim = null,
        Action<IntPtr>? focus = null)
    {
        _services = services;
        _start = start ?? SystemLegacyProcess.Start;
        _fileExists = fileExists ?? File.Exists;
        _reportExit = reportExit ?? ErrorReporting.ErrorReports.ReportLegacyExit;
        _trackUsage = trackUsage ?? EDUsageTelemetry.Track;
        // Konec procesu chodí z thread poolu; při shutdownu (Application.Current null) se zahodí.
        _postToUi = postToUi ?? (action => Application.Current?.Dispatcher.BeginInvoke(action));
        // Během startu shellu je MainWindow ještě login okno.
        _dialogOwner = dialogOwner ?? (() => Application.Current?.MainWindow);
        _dim = dim ?? WindowDimmer.Dim;
        _focus = focus ?? (hwnd => SetForegroundWindow(hwnd));
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
                    var hwnd = e.Process.MainWindowHandle;
                    if (hwnd != IntPtr.Zero) _focus(hwnd);
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
    /// </summary>
    private bool TryFocusExisting(string exeName, string formFqn, string inputParam)
    {
        _tracked.RemoveAll(e => e.Process.HasExited);
        var match = _tracked.FirstOrDefault(e => MatchesTarget(e, exeName, formFqn, inputParam));
        if (match is null) return false;
        var hwnd = match.Process.MainWindowHandle;
        if (hwnd != IntPtr.Zero) _focus(hwnd);
        return true;
    }

    /// <summary>„Stejné okno" = stejné exe (case-insensitive path), forma i EDParamIn.</summary>
    internal static bool MatchesTarget(LegacyLaunch e, string exe, string form, string param) =>
        string.Equals(e.ExeName, exe, StringComparison.OrdinalIgnoreCase)
        && string.Equals(e.FormFqn, form, StringComparison.Ordinal)
        && string.Equals(e.InputParam, param, StringComparison.Ordinal);

    /// <summary>
    /// Čeká (poll á 100 ms), až child vytvoří hlavní okno, nebo do timeoutu / ukončení procesu. Await drží
    /// UI thread volný, overlay se stihne vykreslit. Timeout kryje moduly, které skončí bez okna.
    /// </summary>
    private static async Task WaitForModuleWindowAsync(ILegacyProcess prc, int timeoutMs = 20000)
    {
        for (int elapsed = 0; elapsed < timeoutMs; elapsed += 100)
        {
            if (prc.HasExited) return;
            prc.Refresh();
            if (prc.MainWindowHandle != IntPtr.Zero) return;
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
                var hwnd = prc.MainWindowHandle;
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
        _trackUsage("EDISON2.LegacyLaunch", string.IsNullOrEmpty(formFqn) ? exeName : formFqn);
        var shellGuid = ShellGuidFor(mlRest, shareShellSession);
        var agendaPath = ResolvePath(exeName, _fileExists);

        // EDISONMAIN se spouští napřímo (ne přes host): jeho ApplicationEvents.OnRun nastavuje
        // per-forma default EDParamIn (ApplyDefaultInput), který generický host neumí dodat.
        bool direct = LaunchesDirectly(exeName);
        var launcherPath = direct ? agendaPath : ResolvePath(LauncherExe, _fileExists);
        // Chybějící soubor by skončil Win32Exception bez jména souboru; správce IS potřebuje vědět, co chybí.
        EnsureExecutableExists(agendaPath, Path.GetFileName(exeName), _fileExists);
        if (!direct) EnsureExecutableExists(launcherPath, LauncherExe, _fileExists);
        // Licencované elementy z aktivní licence shellu → host z nich postaví Lic a načte
        // formuláři přístupová práva (MLProps.AccessTable). Bez toho moduly, které gate-ují
        // obsah přes RightGranted (např. DSINPUT Plánování), nezobrazí data. Viz FormToRunGetter.
        var licence = services.ActiveLicence;
        // Při retry nad limitem licencí během přepnutí firmy je to ještě licence původní firmy; spouští se
        // tehdy jen okno aktivity pro admina, které licenci nekontroluje.
        string licensedElements = licence?.Elements is { } els ? string.Join(",", els) : "";

        var prc = _start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = launcherPath,
            Arguments = BuildLauncherArguments(mlRest, formFqn, shellGuid, inputParam, direct ? "" : agendaPath,
                licensedElements, services.IsAdmin, services.MayUpdate, shortcutPayload,
                services.UserDirectory, services.UserEmail,
                licence?.Version ?? "", licence is null ? "" : ((int)licence.State).ToString(CultureInfo.InvariantCulture),
                licence?.ValidityTo ?? ""),
        });
        if (prc is null) return null;

        var launch = new LegacyLaunch(prc, exeName, formFqn, inputParam);
        // Tvrdý pád hosta (host sám dosáhne jen na managed výjimky - viz FormToRunGetter v repu EDISON).
        prc.OnExited(code =>
        {
            if (code is { } exitCode && ShouldReportExit(exitCode, launch.KilledByShell))
                _reportExit(exeName, formFqn, exitCode);
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

    /// <summary>
    /// Složí payload pro generický host: značka + sys kontext + formFqn + shellGuid +
    /// inputParam + cesta k agendě + licencované elementy. Pozice 1–11 jsou shodné s legacy
    /// kontraktem (takže `FormToRunGetter.TryGetFormFromArgs` retrofitnutých agend zůstává
    /// kompatibilní); agendaPath (host ho čte pro <c>Assembly.LoadFrom</c>) je [11],
    /// licencované elementy [12] (comma-separated, pro MLProps.AccessTable - starší host je ignoruje),
    /// zástupce na ploše [13] (opaque, host ho místo formy spustí jako proceduru; prázdné = běžný launch),
    /// admin flag [14] a právo update [15] ("1"/"0" → MLProps.UserIsAdmin / UserMayUpdate; host bez pole
    /// defaultuje na false), osobní adresář [16] a e-mail [17] (→ MLProps.UserDirectory / UserEmail, parita
    /// s legacy frmNoMDI; host bez polí dá ""). Koncové zpětné lomítko adresáře se odřízne - `"C:\dir\"` by
    /// argv parser četl jako escapovanou uvozovku a slil pole. Verze [18], stav [19] a konec platnosti [20]
    /// licence shellu (`Lic.Version`, `(int)Lic.State`, `Lic.ValidityTo`) doplní hostu k elementům [12]
    /// celou licenci - host pak nevolá `GetActiveLic` (seznam všech licencí firmy, na DEV ~1 s) při každém
    /// startu modulu, dokud je licence v pořádku a platná.
    /// Obsah [13] skládá shell (zná typ zástupce), sem přichází jako neprůhledný string - musí být
    /// argv-safe (bez uvozovek a mezer).
    /// </summary>
    internal static string BuildLauncherArguments(IMLRest mlRest, string formFqn, string shellGuid,
        string inputParam, string agendaPath, string licensedElements, bool isAdmin, bool mayUpdate,
        string shortcutPayload = "", string userDirectory = "", string userEmail = "",
        string licenceVersion = "", string licenceState = "", string licenceValidTo = "") =>
        $"EDISONMAIN2 \"{mlRest.Server}\" \"{mlRest.Port}\" \"{mlRest.DataNamespace}\" \"{mlRest.Idfa}\" \"{mlRest.LoginName}\" \"{mlRest.FullName}\" \"{mlRest.Mlp}\" \"{formFqn}\" \"{shellGuid}\" \"{inputParam}\" \"{agendaPath}\" \"{licensedElements}\" \"{shortcutPayload}\" \"{Flag(isAdmin)}\" \"{Flag(mayUpdate)}\" \"{Dir(userDirectory)}\" \"{userEmail}\" \"{Version(licenceVersion)}\" \"{licenceState}\" \"{Plain(licenceValidTo)}\"";

    private static string Flag(bool value) => value ? "1" : "0";
    private static string Dir(string directory) => directory.TrimEnd('\\');
    // Verze jde do legacy Val(): stačí číslice a tečka, cokoli dalšího by jen hrozilo rozbitím argv.
    private static string Version(string version) => new(version.Where(c => char.IsDigit(c) || c == '.').ToArray());
    // Uvozovka nebo koncové zpětné lomítko by pole slily s dalším, mezera uvnitř uvozovek nevadí.
    private static string Plain(string value) => value.Replace("\"", "").TrimEnd('\\');

    /// <summary>Vedle exe (DEBUG běží z bin), jinak deploy adresář legacy agend.</summary>
    internal static string ResolvePath(string relativePath, Func<string, bool> fileExists)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        return fileExists(localPath) ? localPath : Path.Combine(WindowNavigator.EdisonDeployRoot, relativePath);
    }

    /// <summary>Pre-flight před spuštěním legacy procesu: hláška jmenuje chybějící soubor, ne jen „soubor nenalezen".</summary>
    internal static void EnsureExecutableExists(string path, string displayName, Func<string, bool> fileExists)
    {
        if (fileExists(path)) return;
        throw new FileNotFoundException(
            $"Nelze spustit modul: v adresáři EDISONu chybí soubor {displayName}. Kontaktujte správce IS.", path);
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

    private static string SafeGetTitle(ILegacyProcess process)
    {
        try { return process.MainWindowTitle; }
        catch { return ""; }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
