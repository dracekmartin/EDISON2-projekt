using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EDControls.EDViewModel;

namespace EDControls;

/// <summary>
/// Obnoví ikonu okna v liště Windows po prvním zobrazení. Taskbar Windows 11 (od KB5051989) se na
/// ikonu ptá zprávou WM_GETICON jednou, hned po vzniku okna, s krátkým timeoutem; když UI thread
/// zrovna blokuje startup (server, první render), spadne na ikonu třídy okna - WPF žádnou nenastavuje -
/// a výchozí ikona zůstane, dokud se nezmění titulek nebo ikona. Registruje se z
/// <see cref="EDAppearance.Initialize"/> jako class handler na <c>Window.Loaded</c>.
/// </summary>
internal static class TaskbarIcon
{
    private const int WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    // Jednou per okno - opakovaný Loaded (re-parent) by třídě okna přidával další kopie ikon.
    private static readonly DependencyProperty RefreshedProperty =
        DependencyProperty.RegisterAttached("Refreshed", typeof(bool), typeof(TaskbarIcon));

    private static bool _initialized;

    public static void AttachToAllWindows()
    {
        if (_initialized) return;
        _initialized = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ShouldRefresh(window)) return;
        window.SetValue(RefreshedProperty, true);
        // Až thread nic nedělá - taskbar se po WM_SETICON zeptá znovu a odpověď musí dostat včas.
        window.Dispatcher.BeginInvoke(() => Refresh(window), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Jen okno v liště s ikonou nastavenou hodnotou: bez Icon není co znovu poslat
    /// a binding by lokální zápis přebil.</summary>
    internal static bool ShouldRefresh(Window window) =>
        window.ShowInTaskbar
        && window.ReadLocalValue(Window.IconProperty) is ImageSource
        && window is not IOverlayWindow
        && !FrameworkWindows.IsFrameworkOwned(window)
        && !(bool)(window.GetValue(RefreshedProperty) ?? false);

    /// <summary>Znovu pošle WM_SETICON (taskbar se zeptá znovu) a tutéž ikonu dá třídě okna,
    /// kterou taskbar čte bez odpovědi threadu - kryje dotazy během blokovaného bootstrapu.</summary>
    internal static void Refresh(Window window)
    {
        if (!window.IsLoaded) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            if (window.ReadLocalValue(Window.IconProperty) is ImageSource icon)
            {
                window.Icon = null;
                window.Icon = icon;
            }
            SetClassIcons(window, hwnd);
        }
        catch
        {
            // Ikona v liště je kosmetika - nesmí shodit okno.
        }
    }

    private static void SetClassIcons(Window window, IntPtr hwnd)
    {
        // Kopie: WPF své handly při další změně Icon zničí a třída okna by ukazovala do prázdna.
        var big = CopyIcon(SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero));
        var small = CopyIcon(SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero));
        if (big != IntPtr.Zero) SetClassLongPtr(hwnd, GCLP_HICON, big);
        if (small != IntPtr.Zero) SetClassLongPtr(hwnd, GCLP_HICONSM, small);
        window.Closed += (_, _) =>
        {
            if (big != IntPtr.Zero) DestroyIcon(big);
            if (small != IntPtr.Zero) DestroyIcon(small);
        };
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Jen 64-bit entry point - aplikace se vydává win-x64, 32-bit user32 ho nemá.
    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
