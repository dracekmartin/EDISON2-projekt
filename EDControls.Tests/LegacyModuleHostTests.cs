using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using ALVA.LICENCE.EDLicence;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Launch a životní cyklus legacy modulů bez skutečných procesů: fake proces a továrna zaznamenávající
/// <see cref="ProcessStartInfo"/>, UI marshal synchronní, owner dialogu žádný.
/// </summary>
public class LegacyModuleHostTests
{
    private const string Exe = "USERS.exe";
    private const string Form = "ALVA.USERS.formUSERSLocks";

    internal sealed class FakeProcess : ILegacyProcess
    {
        private readonly List<Action<int?>> _callbacks = [];
        private int? _exitCode;

        public bool HasExited { get; private set; }
        public IntPtr MainWindowHandle { get; set; }
        /// <summary>Okno, které se objeví až po <see cref="Refresh"/> (jako u <c>Process</c>).</summary>
        public IntPtr WindowAfterRefresh { get; set; }
        public string MainWindowTitle { get; set; } = "";
        public int? ExitCodeOnWait { get; set; }
        public int RefreshCount { get; private set; }
        public int KillCount { get; private set; }

        public void Refresh()
        {
            RefreshCount++;
            if (WindowAfterRefresh != IntPtr.Zero) MainWindowHandle = WindowAfterRefresh;
        }

        // Synchronně jako nejhorší případ: příznak „zabil shell" musí být nastavený před kill.
        public void Kill()
        {
            KillCount++;
            Exit(-1);
        }

        public void WaitForExit()
        {
            if (!HasExited) Exit(ExitCodeOnWait ?? 0);
        }

        public void OnExited(Action<int?> callback)
        {
            if (HasExited) callback(_exitCode);
            else _callbacks.Add(callback);
        }

        public void Exit(int? code)
        {
            HasExited = true;
            _exitCode = code;
            var pending = _callbacks.ToList();
            _callbacks.Clear();
            foreach (var cb in pending) cb(code);
        }
    }

    private sealed class Harness
    {
        public List<ProcessStartInfo> Started { get; } = [];
        public List<(string Exe, string Form, int Code)> Reports { get; } = [];
        public List<(string Form, string Control)> Usage { get; } = [];
        public List<IntPtr> Focused { get; } = [];
        public HashSet<string> MissingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Func<FakeProcess> CreateProcess { get; set; } = () => new FakeProcess();
        public FakeProcess? Last { get; private set; }
        public int ModuleExitedCount { get; private set; }
        /// <summary>Počet ztmavení shellu, která ještě nikdo neuvolnil.</summary>
        public int ActiveDims { get; private set; }
        /// <summary>Nenull = práce pro UI vlákno se jen řadí, provede ji až test (jako dispatcher).</summary>
        public List<Action>? UiQueue { get; init; }
        public Window? Owner { get; init; }
        public EDServices Services { get; }
        public LegacyModuleHost Host { get; }

        public Harness(IMLRest? mlRest = null, bool noMlRest = false,
                       string launcherExe = LegacyModuleHost.DefaultLauncherExe)
        {
            Services = new EDServices(noMlRest ? null : mlRest ?? MakeMlRest(),
                Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());
            Host = new LegacyModuleHost(() => Services,
                start: psi =>
                {
                    Started.Add(psi);
                    return Last = CreateProcess();
                },
                fileExists: path => !MissingFiles.Contains(path) && !MissingFiles.Contains(Path.GetFileName(path)),
                reportExit: (exe, form, code) => Reports.Add((exe, form, code)),
                trackUsage: (form, control) => Usage.Add((form, control)),
                postToUi: action =>
                {
                    if (UiQueue is null) action();
                    else UiQueue.Add(action);
                },
                dialogOwner: () => Owner,
                dim: _ =>
                {
                    ActiveDims++;
                    return new Undim(() => ActiveDims--);
                },
                focus: hwnd => Focused.Add(hwnd),
                launcherExe: launcherExe);
            Host.LegacyModuleExited += () => ModuleExitedCount++;
        }
    }

    private sealed class Undim(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private static IMLRest MakeMlRest()
    {
        var m = Substitute.For<IMLRest>();
        m.Server.Returns("192.168.16.1");
        m.Port.Returns(57770);
        m.DataNamespace.Returns("MLINE");
        m.Idfa.Returns("CB_DP");
        m.LoginName.Returns("jan.novak");
        m.FullName.Returns("Jan Novák");
        m.Mlp.Returns("157");
        m.Guid.Returns("SHELLGUID");
        m.CacheLocks.Returns(1);
        return m;
    }

    private static LegacyModuleHost.LegacyLaunch MakeEntry(string formFqn, string exeName = Exe, string inputParam = "",
                                                           string title = "") =>
        new(new FakeProcess { MainWindowTitle = title }, exeName, formFqn, inputParam);

    // --- dedup a trackování ---

    [Fact]
    public void ShowLegacy_SameTargetRunning_FocusesAndDoesNotStart()
    {
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");
        h.Last!.MainWindowHandle = 42;

        h.Host.ShowLegacy(Exe, Form, true, "");

        Assert.Single(h.Started);
        Assert.Equal([(IntPtr)42], h.Focused);
    }

    [Fact]
    public void ShowLegacy_DuplicateHasExited_StartsNew()
    {
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");
        h.Last!.Exit(0);

        h.Host.ShowLegacy(Exe, Form, true, "");

        Assert.Equal(2, h.Started.Count);
        Assert.Single(h.Host.GetRunningModules());
    }

    [Fact]
    public void ShowLegacyAsDialog_DoesNotDedup()
    {
        // Dialogový launch ztmavuje shell - druhé spuštění není re-focus, ale nový modul.
        var h = new Harness();

        h.Host.ShowLegacyAsDialog(Exe, Form, true, "");
        h.Host.ShowLegacyAsDialog(Exe, Form, true, "");

        Assert.Equal(2, h.Started.Count);
        Assert.Equal(2, h.Host.GetRunningModules().Count);
    }

    [Fact]
    public async Task ShowLegacyAsDialog_DimsShellUntilModuleExits()
    {
        var h = new Harness { CreateProcess = () => new FakeProcess { MainWindowHandle = 1 } };

        await h.Host.ShowLegacyAsDialogAsync(Exe, Form, true, "");
        Assert.Equal(1, h.ActiveDims);

        h.Last!.Exit(0);

        Assert.Equal(0, h.ActiveDims);
    }

    [Fact]
    public void ShowLegacyAsDialog_ModuleExitedBeforeSubscribe_ShellNotLeftDimmed()
    {
        // Process.Exited fíruje jednou - pozdní přihlášení nesmí nechat shell ztmavený s blokovanou klávesnicí.
        var h = new Harness
        {
            CreateProcess = () =>
            {
                var p = new FakeProcess();
                p.Exit(3);
                return p;
            }
        };

        h.Host.ShowLegacyAsDialog(Exe, Form, true, "");

        Assert.Equal(0, h.ActiveDims);
    }

    [Fact]
    public void ModuleExit_ShellReactionsRunOnUiThreadOnly()
    {
        // Konec chodí z thread poolu: obnova zástupců i WindowDimmer (UI-only) musí jít přes dispatcher.
        var h = new Harness { UiQueue = [] };
        h.Host.ShowLegacy(Exe, "A", true, "");
        var modeless = h.Last!;
        h.Host.ShowLegacyAsDialog(Exe, "B", true, "");
        var dialog = h.Last!;

        modeless.Exit(0);
        dialog.Exit(0);
        Assert.Equal(0, h.ModuleExitedCount);
        Assert.Equal(1, h.ActiveDims);

        foreach (var action in h.UiQueue!.ToList()) action();
        Assert.Equal(1, h.ModuleExitedCount);
        Assert.Equal(0, h.ActiveDims);
    }

    [Fact]
    public void ShowLegacyAsDialog_ActivatedShellBouncesRunningModuleToFront()
    {
        // Klik na ztmavený shell ho aktivuje; okno modulu musí přeskočit dopředu (modal-like UX).
        Sta.RunOnAppThread(() =>
        {
            var h = new Harness { Owner = new Window(), CreateProcess = () => new FakeProcess { MainWindowHandle = 5 } };
            h.Host.ShowLegacyAsDialog(Exe, Form, true, "");

            RaiseActivated(h.Owner!);

            Assert.Equal([(IntPtr)5], h.Focused);
        });
    }

    private static void RaiseActivated(Window window) =>
        typeof(Window).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [EventArgs.Empty]);

    [Fact]
    public async Task ShowLegacyAsync_SameTargetRunning_FocusesAndDoesNotStart()
    {
        var h = new Harness { CreateProcess = () => new FakeProcess { MainWindowHandle = 42 } };

        await h.Host.ShowLegacyAsync(Exe, Form, true, "");
        await h.Host.ShowLegacyAsync(Exe, Form, true, "");

        Assert.Single(h.Started);
        Assert.Equal([(IntPtr)42], h.Focused);
    }

    [Fact]
    public async Task ShowLegacyShortcut_NeitherDedupedNorTracked()
    {
        // Dvě spuštění téže sestavy jsou dva tisky; zástupce není modul, shell ho nezabíjí.
        var h = new Harness { CreateProcess = () => new FakeProcess { MainWindowHandle = 1 } };

        await h.Host.ShowLegacyShortcutAsync(Exe, Form, "UEFZTE9BRA==", true, "");
        await h.Host.ShowLegacyShortcutAsync(Exe, Form, "UEFZTE9BRA==", true, "");
        h.Host.CloseAll();

        Assert.Equal(2, h.Started.Count);
        Assert.Empty(h.Host.GetRunningModules());
        Assert.Equal(0, h.Last!.KillCount);
    }

    [Fact]
    public void ShowLegacyAndWait_ReportsCrashAndDoesNotTrack()
    {
        var h = new Harness { CreateProcess = () => new FakeProcess { ExitCodeOnWait = 3 } };

        h.Host.ShowLegacyAndWait(Exe, Form, false, "");

        // Fake doručuje konec synchronně; skutečný proces hlásí z thread poolu až po návratu z metody.
        Assert.Equal([(Exe, Form, 3)], h.Reports);
        Assert.Empty(h.Host.GetRunningModules());
    }

    [Fact]
    public void GetRunningModules_CarriesIdentityAndActivatesWindow()
    {
        var h = new Harness { CreateProcess = () => new FakeProcess { MainWindowTitle = "Uživatelé", MainWindowHandle = 9 } };
        h.Host.ShowLegacy(Exe, Form, true, "AdminMode=1");

        var module = Assert.Single(h.Host.GetRunningModules());
        module.Activate();

        Assert.Equal("Uživatelé", module.DisplayName);
        Assert.Equal((Exe, Form, "AdminMode=1"), (module.ExeName, module.FormFqn, module.InputParam));
        Assert.Equal([(IntPtr)9], h.Focused);
    }

    // --- hlášení pádu a konec modulu ---

    [Fact]
    public void Exit_NonZero_ReportsExeFormAndCodeOnce()
    {
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");

        h.Last!.Exit(3);

        Assert.Equal([(Exe, Form, 3)], h.Reports);
    }

    [Fact]
    public void Exit_Zero_DoesNotReport()
    {
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");

        h.Last!.Exit(0);

        Assert.Empty(h.Reports);
    }

    [Fact]
    public void Exit_UnknownCode_DoesNotReport()
    {
        // Exit kód nešel přečíst - hlášení pádu nesmí být druhá chyba.
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");

        h.Last!.Exit(null);

        Assert.Empty(h.Reports);
    }

    [Fact]
    public void CloseAll_KillsTrackedWithoutReporting()
    {
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");

        h.Host.CloseAll();

        Assert.Equal(1, h.Last!.KillCount);
        Assert.Empty(h.Reports);
        Assert.Empty(h.Host.GetRunningModules());
    }

    [Fact]
    public void ExternalKill_IsReportedAndNotKilledAgain()
    {
        // taskkill /F zvenku (kód 1) je pád, který chceme vidět; mrtvý proces se už nezabíjí.
        var h = new Harness();
        h.Host.ShowLegacy(Exe, Form, true, "");
        h.Last!.Exit(1);

        h.Host.CloseAll();

        Assert.Equal(0, h.Last.KillCount);
        Assert.Equal([(Exe, Form, 1)], h.Reports);
    }

    [Fact]
    public void ProcessExitedBeforeSubscribe_StillReportsAndRaisesModuleExited()
    {
        var h = new Harness
        {
            CreateProcess = () =>
            {
                var p = new FakeProcess();
                p.Exit(3);
                return p;
            }
        };

        h.Host.ShowLegacy(Exe, Form, true, "");

        Assert.Equal([(Exe, Form, 3)], h.Reports);
        Assert.Equal(1, h.ModuleExitedCount);
    }

    [Fact]
    public async Task LegacyModuleExited_RaisedOnlyForModelessLaunches()
    {
        // Jen modeless moduly můžou zapisovat zástupce, které si shell po jejich konci přenačte.
        var h = new Harness();
        var processes = new List<FakeProcess>();
        h.CreateProcess = () =>
        {
            var p = new FakeProcess { MainWindowHandle = 1 };
            processes.Add(p);
            return p;
        };

        h.Host.ShowLegacy(Exe, "A", true, "");
        await h.Host.ShowLegacyAsync(Exe, "B", true, "");
        h.Host.ShowLegacyAsDialog(Exe, "C", true, "");
        await h.Host.ShowLegacyAsDialogAsync(Exe, "D", true, "");
        await h.Host.ShowLegacyShortcutAsync(Exe, "E", "X", true, "");
        h.Host.ShowLegacyAndWait(Exe, "F", true, "");
        foreach (var p in processes.Where(p => !p.HasExited)) p.Exit(0);

        Assert.Equal(2, h.ModuleExitedCount);
    }

    [Fact]
    public async Task ShowLegacyAsync_CompletesOnceWindowAppears()
    {
        var h = new Harness { CreateProcess = () => new FakeProcess { WindowAfterRefresh = 7 } };

        var task = h.Host.ShowLegacyAsync(Exe, Form, true, "");

        Assert.True(task.IsCompletedSuccessfully);   // bez Task.Delay - okno je po prvním Refresh
        Assert.True(h.Last!.RefreshCount >= 1);
        await task;
    }

    // --- launch: kontrakt PSI, pre-flight, telemetrie ---

    [Fact]
    public void Launch_ViaHost_PassesSessionOfInjectedServices()
    {
        var h = new Harness();
        h.Services.IsAdmin = true;
        h.Services.MayUpdate = true;
        h.Services.UserDirectory = @"C:\Mline\Data";
        h.Services.UserEmail = "jan.novak@firma.cz";
        h.Services.ActiveLicence = new Lic
        {
            Elements = ["26001", "6001"], Version = "2.001", State = Lic.EStates.E01_OK, ValidityTo = "31.12.2026",
        };

        h.Host.ShowLegacy(Exe, Form, true, "AdminMode=1");

        var psi = Assert.Single(h.Started);
        Assert.True(psi.UseShellExecute);
        Assert.Equal("EDISONMAIN2API.exe", Path.GetFileName(psi.FileName));
        Assert.Equal(
            LegacyModuleHost.BuildLauncherArguments(h.Services.MLRest!, Form, "SHELLGUID", "AdminMode=1",
                Path.Combine(AppContext.BaseDirectory, Exe), "26001,6001", isAdmin: true, mayUpdate: true, "",
                @"C:\Mline\Data", "jan.novak@firma.cz", "2.001",
                ((int)Lic.EStates.E01_OK).ToString(CultureInfo.InvariantCulture), "31.12.2026"),
            psi.Arguments);
    }

    [Fact]
    public void Launch_EdisonMain_StartsDirectlyWithoutAgendaPath()
    {
        var h = new Harness();

        h.Host.ShowLegacy("EDISONMAIN.exe", "ALVA.EDISONMAIN.formX", true, "");

        var psi = Assert.Single(h.Started);
        Assert.Equal("EDISONMAIN.exe", Path.GetFileName(psi.FileName));
        Assert.Equal(
            LegacyModuleHost.BuildLauncherArguments(h.Services.MLRest!, "ALVA.EDISONMAIN.formX", "SHELLGUID", "",
                "", "", isAdmin: false, mayUpdate: false),
            psi.Arguments);
    }

    [Fact]
    public void Launch_NoShellSession_ModuleGetsOwnGuid()
    {
        // Pre-login admin override: modul nesmí jet pod shell GUID, zámek shell ještě nemá.
        var h = new Harness();

        h.Host.ShowLegacyAndWait(Exe, Form, false, "");

        Assert.DoesNotContain("SHELLGUID", Assert.Single(h.Started).Arguments);
    }

    [Fact]
    public void Launch_MissingLauncher_ThrowsBeforeStart()
    {
        var h = new Harness();
        h.MissingFiles.Add("EDISONMAIN2API.exe");

        var ex = Assert.Throws<FileNotFoundException>(() => h.Host.ShowLegacy(Exe, Form, true, ""));

        Assert.Contains("EDISONMAIN2API.exe", ex.Message);
        Assert.Empty(h.Started);
        Assert.Empty(h.Host.GetRunningModules());
    }

    [Fact]
    public void ShowLegacyAsync_LaunchFailure_FaultsTask()
    {
        // Caller (shell) chytá chybu z awaitu do popupu - nesmí vyletět synchronně.
        var h = new Harness();
        h.MissingFiles.Add(Exe);

        var task = h.Host.ShowLegacyAsync(Exe, Form, true, "");

        Assert.True(task.IsFaulted);
        Assert.IsType<FileNotFoundException>(task.Exception!.GetBaseException());
    }

    [Fact]
    public void Launch_WithoutMLRest_ThrowsWithoutTelemetry()
    {
        var h = new Harness(noMlRest: true);

        Assert.Throws<InvalidOperationException>(() => h.Host.ShowLegacy(Exe, Form, true, ""));

        Assert.Empty(h.Usage);
        Assert.Empty(h.Started);
    }

    [Theory]
    [InlineData(Form, Form)]
    [InlineData("", Exe)]
    public void Launch_TracksUsageByFormOrExe(string formFqn, string expectedControl)
    {
        var h = new Harness();

        h.Host.ShowLegacy(Exe, formFqn, true, "");

        Assert.Equal([("EDISON2.LegacyLaunch", expectedControl)], h.Usage);
    }

    [Fact]
    public void Launch_TracksUsageUnderAppPrefix()
    {
        // Telemetrie jiné aplikace se nesmí míchat se shellem.
        var h = new Harness();
        h.Services.FormIdPrefix = "TD";

        h.Host.ShowLegacy(Exe, Form, true, "");

        Assert.Equal([("TD.LegacyLaunch", Form)], h.Usage);
    }

    [Fact]
    public void Launch_NothingNextToExe_UsesDeployRootAndConfiguredLauncher()
    {
        var h = new Harness(launcherExe: "HOST.exe");
        h.MissingFiles.Add(Path.Combine(AppContext.BaseDirectory, Exe));
        h.MissingFiles.Add(Path.Combine(AppContext.BaseDirectory, "HOST.exe"));

        h.Host.ShowLegacy(Exe, Form, true, "");

        var psi = Assert.Single(h.Started);
        Assert.Equal(Path.Combine(WindowNavigator.EdisonDeployRoot, "HOST.exe"), psi.FileName);
        Assert.Contains($"\"{Path.Combine(WindowNavigator.EdisonDeployRoot, Exe)}\"", psi.Arguments);
    }

    // --- statické helpery ---

    [Fact]
    public void FormatLegacyLabel_WindowTitle_Wins()
    {
        Assert.Equal("Zámky uživatelů", LegacyModuleHost.FormatLegacyLabel(MakeEntry(Form, title: "Zámky uživatelů")));
    }

    [Fact]
    public void FormatLegacyLabel_NoTitle_ReturnsLastFqnSegment()
    {
        Assert.Equal("formUSERSLocks", LegacyModuleHost.FormatLegacyLabel(MakeEntry(Form)));
    }

    [Fact]
    public void FormatLegacyLabel_FqnWithoutDots_ReturnsWholeFqn()
    {
        Assert.Equal("formStandalone", LegacyModuleHost.FormatLegacyLabel(MakeEntry("formStandalone")));
    }

    [Fact]
    public void FormatLegacyLabel_EmptyFqn_FallsBackToExeName()
    {
        Assert.Equal("MyApp", LegacyModuleHost.FormatLegacyLabel(MakeEntry(formFqn: "", exeName: "MyApp.exe")));
    }

    [Fact]
    public void FormatLegacyLabel_FqnEndingWithDot_FallsBackToWholeFqn()
    {
        // FQN končící tečkou by měl prázdný poslední segment.
        Assert.Equal("ALVA.", LegacyModuleHost.FormatLegacyLabel(MakeEntry("ALVA.")));
    }

    [Theory]
    [InlineData(Exe, Form, "AdminMode=1", true)]
    [InlineData("users.exe", Form, "AdminMode=1", true)]                       // exe case-insensitive
    [InlineData(Exe, Form, "", false)]                                          // stejná forma v jiném módu = jiné okno
    [InlineData(Exe, "ALVA.USERS.formUSERSActivity", "AdminMode=1", false)]
    [InlineData(Exe, "alva.users.formuserslocks", "AdminMode=1", false)]       // forma je FQN Caché třídy
    public void MatchesTarget_ExeFormAndParam(string exe, string form, string param, bool expected)
    {
        var entry = MakeEntry(Form, Exe, "AdminMode=1");

        Assert.Equal(expected, LegacyModuleHost.MatchesTarget(entry, exe, form, param));
    }

    [Theory]
    [InlineData("EDISONMAIN.exe", true)]
    [InlineData(@"C:\Edison\EDISONMAIN.exe", true)]
    [InlineData("edisonmain.exe", true)]
    [InlineData("TemplateDesigner.exe", true)]
    [InlineData(@"C:\Edison\TemplateDesigner.exe", true)]
    [InlineData("USERS.exe", false)]
    [InlineData("MHD2PlanDopravy.exe", false)]
    public void LaunchesDirectly_EdisonMainAndTemplateDesigner(string exeName, bool expected)
    {
        Assert.Equal(expected, LegacyModuleHost.LaunchesDirectly(exeName));
    }

    [Theory]
    [InlineData(true, 1, "SHELLGUID")]
    [InlineData(true, 0, "")]  // shell bez zámku (podpora): modul by pod shell GUID padl na LockExist
    [InlineData(false, 1, "")] // pre-login admin override
    public void ShellGuidFor_SharesGuidOnlyUnderShellLock(bool share, int cacheLocks, string expected)
    {
        var mlRest = MakeMlRest();
        mlRest.CacheLocks.Returns(cacheLocks);

        Assert.Equal(expected, LegacyModuleHost.ShellGuidFor(mlRest, share));
    }

    [Theory]
    [InlineData(0, false, false)]            // normální konec i Application.Exit
    [InlineData(0, true, false)]
    [InlineData(-1, true, false)]            // náš Kill
    [InlineData(-1, false, true)]            // kill zvenku
    [InlineData(1, false, true)]             // taskkill /F
    [InlineData(2, false, true)]             // host: LaunchFailed
    [InlineData(3, false, true)]             // host: FatalReported
    [InlineData(-532462766, false, true)]    // 0xE0434352 neošetřená CLR výjimka
    [InlineData(-1073741819, false, true)]   // 0xC0000005 access violation
    public void ShouldReportExit_NonZeroCodeNotKilledByShell(int exitCode, bool killedByShell, bool expected)
    {
        Assert.Equal(expected, LegacyModuleHost.ShouldReportExit(exitCode, killedByShell));
    }

    [Fact]
    public void ResolvePath_ExistsNextToExe_ReturnsLocalPath()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, Exe), LegacyModuleHost.ResolvePath(Exe, _ => true, @"D:\Deploy"));
    }

    [Fact]
    public void ResolvePath_Missing_FallsBackToDeployRoot()
    {
        Assert.Equal(Path.Combine(@"D:\Deploy", Exe), LegacyModuleHost.ResolvePath(Exe, _ => false, @"D:\Deploy"));
    }

    [Fact]
    public void ResolveExistingPath_ExistingNextToExe_ReturnsIt()
    {
        var dll = Path.GetFileName(typeof(LegacyModuleHost).Assembly.Location);

        Assert.Equal(Path.Combine(AppContext.BaseDirectory, dll), LegacyModuleHost.ResolveExistingPath(dll, File.Exists, @"D:\Deploy"));
    }

    [Fact]
    public void ResolveExistingPath_MissingEverywhere_NamesFileAndBothDirectories()
    {
        // Shell z UNC sdílení hledá nejdřív vedle sebe - hláška jen s fallbackem by správce poslala jinam.
        var ex = Assert.Throws<FileNotFoundException>(
            () => LegacyModuleHost.ResolveExistingPath("EDISONMAIN2API.exe", _ => false, @"D:\Deploy"));

        var exeDir = Path.GetDirectoryName(Path.Combine(AppContext.BaseDirectory, "x"));
        Assert.Contains($"soubor EDISONMAIN2API.exe chybí v adresáři {exeDir} i D:\\Deploy.", ex.Message);
        Assert.Contains("správce IS", ex.Message);
        Assert.Equal(@"D:\Deploy\EDISONMAIN2API.exe", ex.FileName);
    }

    [Fact]
    public void ResolveExistingPath_ShellRunsFromDeployRoot_NamesDirectoryOnce()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => LegacyModuleHost.ResolveExistingPath("USERS.exe", _ => false, AppContext.BaseDirectory));

        var exeDir = Path.GetDirectoryName(Path.Combine(AppContext.BaseDirectory, "x"));
        Assert.Contains($"soubor USERS.exe chybí v adresáři {exeDir}.", ex.Message);
    }

    // --- argv kontrakt ---

    [Fact]
    public void BuildLauncherArguments_EmptyInputParam_StillOccupiesPositionalSlot()
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin: false, mayUpdate: false);

        // Prázdný inputParam zůstává jako "" na pozici [10] - pořadí polí se neposune.
        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\" \"0\" \"0\" \"\" \"\" \"\" \"\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_EmbeddedQuote_NotEscaped()
    {
        // Známý limit wire formátu: uvozovka uvnitř hodnoty se needukuje ani neescapuje.
        // Legacy parser kontrakt je zmrazený - produkci NEměnit.
        var m = MakeMlRest();
        m.FullName.Returns("Jan \"Boss\" Novák");

        var args = LegacyModuleHost.BuildLauncherArguments(
            m, "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin: false, mayUpdate: false);

        Assert.Contains("\"Jan \"Boss\" Novák\"", args);
    }

    [Fact]
    public void BuildLauncherArguments_OrderingMatchesContract()
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "AdminMode=1", @"C:\Edison\X.exe", "26001,6001", isAdmin: true, mayUpdate: true);

        Assert.Equal(
            "EDISONMAIN2 \"192.168.16.1\" \"57770\" \"MLINE\" \"CB_DP\" \"jan.novak\" \"Jan Novák\" \"157\" \"ALVA.X.formY\" \"SHELLGUID\" \"AdminMode=1\" \"C:\\Edison\\X.exe\" \"26001,6001\" \"\" \"1\" \"1\" \"\" \"\" \"\" \"\" \"\"",
            args);
    }

    [Fact]
    public void BuildLauncherArguments_ShortcutPayload_FollowsLicensedElements()
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", false, false, "UEFZTE9BRA==");

        // Zástupce je [13] - až za licencovanými elementy, aby starší host kontrakt jen ignoroval.
        Assert.EndsWith("\"26001,6001\" \"UEFZTE9BRA==\" \"0\" \"0\" \"\" \"\" \"\" \"\" \"\"", args);
    }

    [Theory]
    [InlineData(true, false, "\"1\" \"0\"")]
    [InlineData(false, true, "\"0\" \"1\"")]
    public void BuildLauncherArguments_UserFlags_FollowShortcutPayload(bool isAdmin, bool mayUpdate, string expectedTail)
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", isAdmin, mayUpdate);

        // Admin [14] a právo update [15] - host bez nich defaultuje na false, hodnota musí být
        // explicitní a nezávislá (admin neimplikuje update). Za nimi prázdný adresář a e-mail [16]/[17]
        // a prázdná pole licence [18]-[20].
        Assert.EndsWith($"\"\" {expectedTail} \"\" \"\" \"\" \"\" \"\"", args);
        Assert.Equal(21, args.Split(" \"").Length);
    }

    [Fact]
    public void BuildLauncherArguments_UserDirectoryAndEmail_FollowUserFlags()
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", false, false,
            userDirectory: @"C:\Mline\EdisonData", userEmail: "jan.novak@firma.cz");

        // [16] osobní adresář a [17] e-mail → MLProps.UserDirectory / UserEmail; host bez polí dá "".
        Assert.EndsWith("\"0\" \"0\" \"C:\\Mline\\EdisonData\" \"jan.novak@firma.cz\" \"\" \"\" \"\"", args);
        Assert.Equal(21, args.Split(" \"").Length);
    }

    [Fact]
    public void BuildLauncherArguments_LicenceFields_AreLastFields()
    {
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "26001,6001", false, false,
            userEmail: "jan.novak@firma.cz", licenceVersion: "2.001", licenceState: "1", licenceValidTo: "31.12.2026");

        // [18] verze, [19] stav a [20] konec platnosti - host z nich a z elementů [12] složí licenci bez GetActiveLic.
        Assert.EndsWith("\"jan.novak@firma.cz\" \"2.001\" \"1\" \"31.12.2026\"", args);
        Assert.Equal(21, args.Split(" \"").Length);
    }

    [Theory]
    [InlineData("31.12.2026", "31.12.2026")]
    [InlineData("31.12.2026 0:00:00", "31.12.2026 0:00:00")]
    [InlineData("31.12.\"2026", "31.12.2026")]
    [InlineData("31.12.2026\\", "31.12.2026")]
    public void BuildLauncherArguments_LicenceValidTo_KeepsFieldBoundaries(string validTo, string expected)
    {
        // Mezera v uvozovkách pole nerozdělí, uvozovka a koncové zpětné lomítko ano.
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "", false, false,
            licenceVersion: "2.001", licenceState: "1", licenceValidTo: validTo);

        Assert.EndsWith($"\"2.001\" \"1\" \"{expected}\"", args);
        Assert.Equal(21, args.Split(" \"").Length);
    }

    [Theory]
    [InlineData("2.001", "2.001")]
    [InlineData("1.003 beta", "1.003")]
    [InlineData("2\" \"x", "2")]
    [InlineData("", "")]
    public void BuildLauncherArguments_LicenceVersion_KeepsOnlyDigitsAndDots(string version, string expected)
    {
        // Mezera nebo uvozovka ve verzi by posunula pole za ní (argv hosta se neescapuje).
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "", false, false,
            licenceVersion: version, licenceState: "1");

        Assert.EndsWith($"\"{expected}\" \"1\" \"\"", args);
        Assert.Equal(21, args.Split(" \"").Length);
    }

    [Fact]
    public void BuildLauncherArguments_UserDirectory_TrailingBackslashTrimmed()
    {
        // "C:\dir\" by argv parser hosta četl \" jako escapovanou uvozovku a slil pole [16] s [17].
        var args = LegacyModuleHost.BuildLauncherArguments(
            MakeMlRest(), "ALVA.X.formY", "SHELLGUID", "", @"C:\Edison\X.exe", "", false, false,
            userDirectory: @"C:\Mline\EdisonData\");

        Assert.EndsWith("\"C:\\Mline\\EdisonData\" \"\" \"\" \"\" \"\"", args);
    }
}
