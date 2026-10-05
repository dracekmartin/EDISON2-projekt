using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace EDControls.EDViewModel;

/// <summary>
/// Otevírá WPF okna (caller předá instanci přes <c>new</c>) a legacy WinForms agendy, které deleguje
/// na <see cref="LegacyModuleHost"/>. <see cref="CloseAll"/> zavře okna i legacy moduly při zavření shellu.
/// </summary>
public class WindowNavigator : IWindowNavigator
{
    /// <summary>Deploy legacy agend; fallback <see cref="ResolveExecutablePath"/>, když soubor není vedle exe.</summary>
    public const string EdisonDeployRoot = @"C:\Edison";

    private readonly LegacyModuleHost? _legacy;

    /// <summary>Navigator shellu: legacy moduly berou session z <see cref="EDServices.Current"/> až při spuštění.</summary>
    public WindowNavigator() : this(new LegacyModuleHost(() => EDServices.Current)) { }

    internal WindowNavigator(LegacyModuleHost? legacy) => _legacy = legacy;

    /// <summary>Jen WPF okna - pro aplikaci, která legacy moduly nespouští (TemplateDesigner).</summary>
    public static IWindowNavigator WpfOnly() => new WindowNavigator((LegacyModuleHost?)null);

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

    public event Action? LegacyModuleExited
    {
        add { if (_legacy is not null) _legacy.LegacyModuleExited += value; }
        remove { if (_legacy is not null) _legacy.LegacyModuleExited -= value; }
    }

    public void ShowLegacy(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "") =>
        Legacy.ShowLegacy(exeName, formFqn, shareShellSession, inputParam);

    public Task ShowLegacyAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "") =>
        _legacy?.ShowLegacyAsync(exeName, formFqn, shareShellSession, inputParam) ?? Task.FromException(NoLegacyHost());

    public Task ShowLegacyShortcutAsync(string exeName, string formFqn, string shortcutPayload,
                                        bool shareShellSession = true, string inputParam = "") =>
        _legacy?.ShowLegacyShortcutAsync(exeName, formFqn, shortcutPayload, shareShellSession, inputParam)
        ?? Task.FromException(NoLegacyHost());

    public void ShowLegacyAndWait(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "") =>
        Legacy.ShowLegacyAndWait(exeName, formFqn, shareShellSession, inputParam);

    public void ShowLegacyAsDialog(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "") =>
        Legacy.ShowLegacyAsDialog(exeName, formFqn, shareShellSession, inputParam);

    public Task ShowLegacyAsDialogAsync(string exeName, string formFqn, bool shareShellSession = true, string inputParam = "") =>
        _legacy?.ShowLegacyAsDialogAsync(exeName, formFqn, shareShellSession, inputParam) ?? Task.FromException(NoLegacyHost());

    private LegacyModuleHost Legacy => _legacy ?? throw NoLegacyHost();

    private static NotSupportedException NoLegacyHost() =>
        new("Tato aplikace legacy moduly nespouští (WindowNavigator.WpfOnly).");

    /// <summary>Modeless WPF okna (kromě <c>MainWindow</c>) + kill legacy modulů. Shell-only, proto ne na
    /// <see cref="IWindowNavigator"/>. Veto okna ignoruje; při výměně session volej <see cref="TryCloseAll"/>.</summary>
    public void CloseAll()
    {
        CloseShellWindows();
        CloseLegacyModules();
    }

    /// <summary>Při vetu okna (<c>e.Cancel</c> v Closing) vrátí <c>false</c> a moduly nezabíjí - caller musí
    /// výměnu session zrušit, přeživší okno by jinak mluvilo s cizí session.</summary>
    public bool TryCloseAll()
    {
        if (!TryCloseShellWindows()) return false;
        CloseLegacyModules();
        return true;
    }

    /// <summary>Jen WPF část: tato okna volají server přes sdílený MLRest, při přepnutí firmy se proto
    /// zavírají před swapem Idfa, legacy moduly (vlastní spojení) až po úspěchu. Veto ignoruje.</summary>
    public void CloseShellWindows() => TryCloseShellWindows();

    /// <summary>Jako <see cref="CloseShellWindows"/>, ale vrátí <c>false</c>, když některé okno zavření
    /// odmítlo (typicky dotaz na neuložená data). Zbylá okna se přesto zkusí zavřít.</summary>
    public bool TryCloseShellWindows()
    {
        var allClosed = true;
        foreach (var w in Application.Current.Windows.OfType<Window>().ToList())
            if (ShouldClose(w, Application.Current.MainWindow))
            {
                w.Close();
                // Vetované Close() nechá okno naživu - IsLoaded je jediný spolehlivý signál.
                allClosed &= !w.IsLoaded;
            }
        return allClosed;
    }

    /// <summary>Jen legacy část <see cref="CloseAll"/>: kill tracked child procesů.</summary>
    public void CloseLegacyModules() => _legacy?.CloseAll();

    /// <summary>Snapshot pro menu „Spuštěné moduly": legacy moduly + WPF okna kromě <c>MainWindow</c>.
    /// Shell-only, proto ne na <see cref="IWindowNavigator"/>.</summary>
    public IReadOnlyList<RunningModuleInfo> GetActiveModules()
    {
        var wpf = Application.Current.Windows
            .OfType<Window>()
            .Where(w => w != Application.Current.MainWindow && w.IsLoaded && !IsInternalWindow(w))
            .Select(w => new RunningModuleInfo(
                DisplayName: !string.IsNullOrEmpty(w.Title) ? w.Title : w.GetType().Name,
                Activate: () =>
                {
                    if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                    w.Activate();
                    w.Focus();
                }));

        return [.. (_legacy?.GetRunningModules() ?? []), .. wpf];
    }

    /// <summary>Okno, které aplikace nevlastní: overlay okna EDControls (<see cref="IOverlayWindow"/> -
    /// panel Podpora / „Akce probíhá") a okna frameworku (<see cref="FrameworkWindows"/>, včetně chrome
    /// AvalonDocku). Nejsou to moduly a nesmí se zavírat.</summary>
    internal static bool IsInternalWindow(Window w) =>
        w is IOverlayWindow || FrameworkWindows.IsFrameworkOwned(w);

    /// <summary>Smí <see cref="CloseAll"/> tohle okno zavřít? (Odděleno od enumerace
    /// <c>Application.Windows</c>, aby šlo testovat bez běžící <c>Application</c>.)</summary>
    internal static bool ShouldClose(Window w, Window? mainWindow) =>
        w != mainWindow && !IsInternalWindow(w);

    public static string ResolveExecutablePath(string relativePath) =>
        LegacyModuleHost.ResolvePath(relativePath, File.Exists);

    // Firma v titulku záměrně není - jedno sezení = jedna firma, identitu nese titulek shellu a status bar.
    // AttachToWindow před Show(): SourceInitialized fíruje uvnitř Show() před prvním paintem (restore stavu).
    private static void WireUp(Window window)
    {
        window.Owner = Application.Current.MainWindow;

        if (window.DataContext is EDViewModel vm)
            vm.AttachToWindow(window);
    }
}
