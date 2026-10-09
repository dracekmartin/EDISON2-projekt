using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace EDISON2.E2ETests;

/// <summary>
/// Spuštěný DEBUG shell s <c>e2e=1</c> a jeho okna přes UI Automation. Co se smí klikat,
/// drží <c>CLAUDE.md</c> projektu.
/// </summary>
internal sealed class ShellSession : IDisposable
{
    private const string MainTitlePrefix = "M-line EDISON [";

    // Musí sedět s TestRunOptions.WindowMarker v EDISONMAIN2.
    private const string E2EMarker = "E2E";

    // Kopie EdisonMainViewModel.ExitConfirmMessage.
    private const string ExitConfirmText = "Opravdu chcete ukončit aplikaci? Zavřou se všechna otevřená agendová okna.";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    private readonly Mutex _runLock;
    private readonly Process _process;
    private readonly Application _app;
    private readonly UIA3Automation _automation = new();
    private readonly HashSet<string> _allowedWindows = [];
    private bool _exited;
    private bool _screenshotTaken;

    private ShellSession(Mutex runLock, Process process)
    {
        _runLock = runLock;
        _process = process;
        _app = Application.Attach(process);
    }

    public Window Main { get; private set; } = null!;

    public int ExitCode => _process.ExitCode;

    public static ShellSession Start() => Launch([], waitUntilReady: true);

    /// <summary>Shell s dalšími argumenty bez čekání na hlavní okno: okna před shellem obslouží test
    /// (<see cref="WaitForStartupWindow"/>) a pak zavolá <see cref="WaitUntilReady"/>.</summary>
    public static ShellSession StartBeforeShell(params string[] extraArgs) => Launch(extraArgs, waitUntilReady: false);

    private static ShellSession Launch(IEnumerable<string> extraArgs, bool waitUntilReady)
    {
        var exe = ShellExe();
        var (host, port) = Server();
        // Bez serveru by GetCompanyList visel bez jediného okna na 8h timeoutu MLRest.
        if (!Reachable(host, port))
            throw new InvalidOperationException($"Dev server {host}:{port} není dostupný (VPN?).");

        var runLock = new Mutex(false, @"Local\EDISON2.E2E");
        if (!runLock.WaitOne(0))
        {
            runLock.Dispose();
            throw new InvalidOperationException("Běží jiný E2E test - oba by sdílely E2E GUID.");
        }

        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        psi.ArgumentList.Add($"server={host}");
        psi.ArgumentList.Add($"restport={port}");
        psi.ArgumentList.Add("e2e=1");
        // Výchozí firma serveru nemusí mít pro DEBUG uživatele žádné moduly (na DEV má prázdný strom).
        var idfa = Environment.GetEnvironmentVariable("EDISON_E2E_IDFA") is { Length: > 0 } custom ? custom : "CB_DP";
        psi.ArgumentList.Add($"idfa={idfa}");
        foreach (var arg in extraArgs) psi.ArgumentList.Add(arg);

        ShellSession? session = null;
        try
        {
            session = new ShellSession(runLock, Process.Start(psi)!);
            if (waitUntilReady) session.WaitUntilReady();
            return session;
        }
        catch
        {
            if (session is null) runLock.ReleaseMutex();
            session?.Dispose();
            throw;
        }
    }

    /// <summary>Položka menu: <paramref name="menuId"/> → <paramref name="top"/> → <paramref name="item"/> (texty Header).</summary>
    public void InvokeMenu(string menuId, string top, string item)
    {
        var menu = WaitFor(() => Main.FindFirstDescendant(cf => cf.ByAutomationId(menuId)), $"menu {menuId}");
        var topItem = WaitFor(() => menu.FindFirstChild(cf => cf.ByName(top))?.AsMenuItem(), $"položka „{top}\"");
        topItem.Expand();
        var target = WaitFor(() => FindMenuItem(topItem, item)?.AsMenuItem(), $"položka „{item}\"");
        // Invoke, ne Click: nepotřebuje myš ani fokus a modální okno, které otevře, ho nezablokuje.
        target.Invoke();
    }

    public Window WaitForWindow(string title) => WaitForWindow(title, StepTimeout);

    /// <summary>Okno, které shell ukáže během startu: před ním běží dotazy na server, čeká se jako na hlavní okno.</summary>
    public Window WaitForStartupWindow(string title) => WaitForWindow(title, StartupTimeout);

    private Window WaitForWindow(string title, TimeSpan timeout)
    {
        _allowedWindows.Add(title);
        return WaitFor(() => Windows().FirstOrDefault(w => w.Name == title)?.AsWindow(), $"okno „{title}\"", timeout);
    }

    public void WaitUntilClosed(string title)
    {
        WaitUntil(() => !Windows().Any(w => w.Name == title), $"zavření okna „{title}\"", StepTimeout);
        _allowedWindows.Remove(title);
    }

    /// <summary>Text EDPopupu.</summary>
    public string PopupMessage(Window popup) =>
        WaitFor(() => popup.FindFirstDescendant(cf => cf.ByAutomationId("MessageText")), "text popupu").Name;

    public Button Button(AutomationElement scope, string automationId) =>
        WaitFor(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsButton(), $"tlačítko {automationId}");

    public T WaitFor<T>(Func<T?> probe, string what) where T : class => WaitFor(probe, what, StepTimeout);

    /// <summary>Hlavní okno pod minimum; WPF ho zastaví na MinWidth × MinHeight. Vrací se po ustálení velikosti.</summary>
    public void ShrinkToMinimumSize()
    {
        Main.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        Main.Patterns.Transform.Pattern.Resize(1, 1);
        var last = System.Drawing.Rectangle.Empty;
        WaitUntil(() =>
        {
            var now = Main.BoundingRectangle;
            bool stable = now == last;
            last = now;
            return stable;
        }, "ustálení velikosti okna", StepTimeout);
    }

    /// <summary>Položky první úrovně obou menu lišty (EDISON a hlavní menu) s jejich obdélníky na obrazovce.</summary>
    public IReadOnlyList<(string Name, System.Drawing.Rectangle Bounds)> MenuBarItems() =>
        [.. new[] { "EdisonMenu", "TopMenu" }
            .Select(id => WaitFor(() => Main.FindFirstDescendant(cf => cf.ByAutomationId(id)), $"menu {id}"))
            .SelectMany(menu => menu.FindAllChildren(cf => cf.ByControlType(ControlType.MenuItem)))
            .Select(item => (item.Name, item.BoundingRectangle))];

    /// <summary>„EDISON" → „Ukončit program" → „Ano" a čekání na konec procesu (logout uvolní E2E zámek).</summary>
    public void ExitViaMenu()
    {
        InvokeMenu("EdisonMenu", "EDISON", "Ukončit program");
        var popup = WaitForWindow("Potvrzení");
        var message = PopupMessage(popup);
        // Titulek „Potvrzení" a „Ano" má i brána přihlášení - její Ano by povolilo přihlašování celému serveru.
        if (message != ExitConfirmText) Fail($"Potvrzení s nečekaným textem: „{message}\".", popup);
        Button(popup, "PositiveBtn").Invoke();
        if (!_process.WaitForExit(StepTimeout)) Fail("Shell po potvrzení konce do 30 s neskončil.");
        _exited = true;
    }

    public void Dispose()
    {
        if (!_exited && !_process.HasExited)
        {
            if (!_screenshotTaken) Screenshot(Main);
            // Kill, ne zavření okna: WM_CLOSE vyvolá potvrzení konce. Zámek pod E2E GUID uklidí příští čistý běh.
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(TimeSpan.FromSeconds(10));
        }
        _app.Dispose();
        _automation.Dispose();
        _process.Dispose();
        _runLock.ReleaseMutex();
        _runLock.Dispose();
    }

    /// <summary>Hlavní okno shellu se stromem ze serveru; skončený proces nebo nečekané okno test shodí.</summary>
    public void WaitUntilReady()
    {
        Main = WaitFor(FindMainWindow, "hlavní okno shellu", StartupTimeout);
        // Starý build e2e=1 nezná a běží s DEV GUID: jeho čistý konec by smazal zámky vývojářova shellu.
        if (Main.Properties.ItemStatus.ValueOrDefault != E2EMarker)
            Fail("Build shellu nezná e2e=1 (hlavní okno nemá značku E2E) - přebuildovat EDISONMAIN2 v Debug.");
        // Strom ze serveru filtrovaný licencí. Karty ani uzly nepřepínat, obojí zapisuje uživatelovy preference.
        WaitFor(() => Main.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem)), "položky stromu modulů", StartupTimeout);
    }

    private T WaitFor<T>(Func<T?> probe, string what, TimeSpan timeout) where T : class
    {
        T? found = null;
        WaitUntil(() => (found = probe()) is not null, what, timeout);
        return found!;
    }

    private void WaitUntil(Func<bool> condition, string what, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (_process.HasExited) Fail($"Shell skončil (exit {_process.ExitCode}) při čekání na: {what}.");
            if (UnexpectedWindow() is { } unexpected)
                Fail($"Nečekané okno při čekání na: {what} - {Describe(unexpected)}", unexpected);
            try
            {
                if (condition()) return;
            }
            catch (Exception)
            {
                // UIA strom se během startu a zavírání oken mění pod rukama; příští pokus.
            }
            if (DateTime.UtcNow > deadline) Fail($"Do {timeout.TotalSeconds:0} s se nedočkal: {what}.");
            Thread.Sleep(250);
        }
    }

    private AutomationElement[] ProcessRoots() =>
        _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(_process.Id));

    private IEnumerable<AutomationElement> Windows()
    {
        // Modální okna WPF se v UIA objevují jako děti ownera i jako top-level - prohledat obojí.
        foreach (var root in ProcessRoots())
            if (root.ControlType == ControlType.Window) yield return root;
        if (Main is not null)
            foreach (var child in Main.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
                yield return child;
    }

    private Window? FindMainWindow() =>
        ProcessRoots().FirstOrDefault(e => e.ControlType == ControlType.Window
                                           && e.Name.StartsWith(MainTitlePrefix, StringComparison.Ordinal))?.AsWindow();

    private AutomationElement? FindMenuItem(AutomationElement parent, string name)
    {
        // Podmenu WPF je vlastní popup okno procesu, ne vždy potomek položky.
        var item = parent.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.MenuItem)));
        return item ?? ProcessRoots()
            .Select(root => root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.MenuItem))))
            .FirstOrDefault(found => found is not null);
    }

    private AutomationElement? UnexpectedWindow()
    {
        try
        {
            // Okna bez titulku (panel Podpora, překryv „Akce probíhá") patří k běžnému chodu.
            return Windows().FirstOrDefault(w => w.Name is { Length: > 0 } title
                                                 && !title.StartsWith(MainTitlePrefix, StringComparison.Ordinal)
                                                 && !_allowedWindows.Contains(title));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Describe(AutomationElement window)
    {
        try
        {
            var texts = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(t => t.Name).Where(n => n.Length > 0).Take(8);
            return $"„{window.Name}\": {string.Join(" | ", texts)}";
        }
        catch (Exception)
        {
            return $"„{window.Name}\"";
        }
    }

    [DoesNotReturn]
    private void Fail(string message, AutomationElement? subject = null)
    {
        var shot = Screenshot(subject ?? Main);
        throw new InvalidOperationException(shot is null ? message : $"{message} Artefakty: {shot} (+ .uia.txt)");
    }

    /// <summary>Obsah okna (ne výřez obrazovky - okno bývá za jiným) a výpis stromu UIA vedle něj.</summary>
    private string? Screenshot(AutomationElement? element)
    {
        if (element is null) return null;
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "e2e-artifacts");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"shell-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            _screenshotTaken = true;
            File.WriteAllText(Path.ChangeExtension(path, ".uia.txt"), DumpProcessTree());
            WindowCapture.ToFile(element.Properties.NativeWindowHandle.ValueOrDefault, path);
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string DumpProcessTree()
    {
        var walker = _automation.TreeWalkerFactory.GetControlViewWalker();
        var sb = new System.Text.StringBuilder();
        var budget = 1500;
        void Dump(AutomationElement node, int depth)
        {
            if (budget-- <= 0 || depth > 12) return;
            string Safe(Func<string> read) { try { return read(); } catch (Exception) { return "?"; } }
            sb.Append(' ', depth * 2)
              .Append(Safe(() => node.ControlType.ToString())).Append(" '").Append(Safe(() => node.Name)).Append("' [")
              .Append(Safe(() => node.AutomationId)).Append(']').AppendLine();
            for (var child = walker.GetFirstChild(node); child is not null; child = walker.GetNextSibling(child))
                Dump(child, depth + 1);
        }
        foreach (var root in ProcessRoots()) Dump(root, 0);
        return sb.ToString();
    }

    private static string ShellExe()
    {
        if (Environment.GetEnvironmentVariable("EDISON_E2E_EXE") is { Length: > 0 } custom) return custom;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        var exe = dir is null
            ? ""
            : Path.Combine(dir.FullName, "EDISONMAIN2", "dotnet", "bin", "Debug", "net10.0-windows", "EDISONMAIN2.exe");
        // Bez ProjectReference: build by spadl na zamčených DLL, když z bin\Debug běží vývojářův shell.
        if (!File.Exists(exe))
            throw new FileNotFoundException("Chybí DEBUG build shellu - sestav EDISONMAIN2 v Debug, nebo nastav EDISON_E2E_EXE.", exe);
        return exe;
    }

    private static (string Host, int Port) Server() =>
        (Environment.GetEnvironmentVariable("EDISON_IT_SERVER") is { Length: > 0 } host ? host : "192.168.16.1",
         int.TryParse(Environment.GetEnvironmentVariable("EDISON_IT_PORT"), out var port) ? port : 57770);

    private static bool Reachable(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            client.ConnectAsync(host, port, timeout.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
