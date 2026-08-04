using System;
using System.Windows;

namespace EDControls;

/// <summary>
/// Okna, která si vytvořil framework (WPF/WebView2 interní, chrome AvalonDocku), ne aplikace.
/// Shell na nich nesmí nic dělat: zavírat je, věšet na ně vlastní owned okna ani je nabízet jako
/// spuštěný modul — vlastník si drží jejich stav a cizí zásah mu ho rozbije.
/// </summary>
internal static class FrameworkWindows
{
    internal static bool IsFrameworkOwned(Window window)
    {
        var type = window.GetType();
        // Holé `Window` je okno aplikace (i když leží v System.Windows) — okna frameworku jsou
        // vždycky jeho podtypy.
        return type != typeof(Window) && IsFrameworkNamespace(type.Namespace);
    }

    /// <summary>
    /// Uživatelská okna nejsou v MS./System./Microsoft. namespace. **AvalonDock**: `DockingManager`
    /// si overlay okno (vzniká skryté už při jeho Loaded) cachuje a nulluje ho teprve po úspěšném
    /// `Close()` — cizí zavření ho nechá zavřené a každý další drag přes dock pak hodí „po zavření
    /// okna nelze nastavit Visibility". Marker <c>IOverlayWindow</c> ho nepokryje: AvalonDock má
    /// vlastní typ téhož krátkého názvu.
    /// </summary>
    internal static bool IsFrameworkNamespace(string? ns)
    {
        ns ??= "";
        return ns.StartsWith("MS.", StringComparison.Ordinal)
            || ns.StartsWith("System.", StringComparison.Ordinal)
            || ns.StartsWith("Microsoft.", StringComparison.Ordinal)
            || ns.StartsWith("AvalonDock", StringComparison.Ordinal);
    }
}
