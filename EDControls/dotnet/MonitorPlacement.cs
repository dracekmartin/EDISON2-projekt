using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using EDControls.EDViewModel;

namespace EDControls;

/// <summary>
/// Monitor pro okno, které nemá uloženou pozici ani použitelného vlastníka. Bez toho jde takové okno na hlavní
/// monitor (CW_USEDEFAULT, <c>SystemParameters.WorkArea</c>), i když uživatel má shell na jiném. Monitor se
/// mezi procesy předává jménem zařízení (<c>\\.\DISPLAY2</c>): je stejné v každém procesu bez ohledu na DPI režim.
/// </summary>
public static class MonitorPlacement
{
    private const uint MonitorDefaultToNull = 0;
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>Jméno monitoru, na kterém okno leží (minimalizované podle poslední obnovené pozice); null bez HWND.</summary>
    public static string? DeviceNameOf(Window? window)
    {
        if (window is null) return null;
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == IntPtr.Zero ? null : DeviceNameOf(MonitorFromWindow(hwnd, MonitorDefaultToNearest));
    }

    /// <summary>
    /// Pracovní plocha (DIP) pro okno bez použitelného vlastníka: monitor vlastníka (i minimalizovaného), hlavního
    /// okna aplikace, monitor z <see cref="EDServices.PreferredMonitor"/>, jiného viditelného okna, kurzoru.
    /// Nic z toho → hlavní monitor. UI thread only.
    /// </summary>
    public static Rect FallbackWorkArea(Window window)
    {
        var app = Application.Current;
        var main = MainWindowOtherThan(app?.MainWindow, window);
        var device = ChooseMonitor(
            Candidates(
                () => DeviceNameOf(window.Owner),
                () => DeviceNameOf(main),
                EDServices.CurrentOrNull?.PreferredMonitor,
                OtherVisibleWindows(app, window).Select(w => DeviceNameOf(w)),
                CursorMonitor),
            IsConnected);
        return device is not null && FindWorkAreaPx(device) is { } px ? ToDips(px, window) : SystemParameters.WorkArea;
    }

    /// <summary>Pracovní plocha (DIP) monitoru z nápovědy pro okno bez vlastníka; null, když nápověda chybí nebo monitor není připojený.</summary>
    internal static Rect? PreferredWorkArea(Window window)
    {
        if (window.Owner is not null) return null;
        var device = EDServices.CurrentOrNull?.PreferredMonitor;
        if (string.IsNullOrEmpty(device) || FindWorkAreaPx(device) is not { } px) return null;
        return ToDips(px, window);
    }

    /// <summary>Okno samo kandidátem není: první okno procesu je zároveň MainWindow a bez pozice leží na hlavním monitoru.</summary>
    internal static Window? MainWindowOtherThan(Window? main, Window window) => ReferenceEquals(main, window) ? null : main;

    /// <summary>Kandidáti v pořadí přednosti. Líně, ať se monitor kurzoru nezjišťuje, když rozhodne vlastník.</summary>
    internal static IEnumerable<string?> Candidates(Func<string?> owner, Func<string?> mainWindow, string? preferred,
                                                    IEnumerable<string?> otherWindows, Func<string?> cursor)
    {
        yield return owner();
        yield return mainWindow();
        yield return preferred;
        foreach (var other in otherWindows) yield return other;
        yield return cursor();
    }

    /// <summary>První neprázdný kandidát, jehož monitor je připojený (nápověda shellu mohla přijít před odpojením).</summary>
    internal static string? ChooseMonitor(IEnumerable<string?> candidates, Func<string, bool> isConnected) =>
        candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && isConnected(c));

    /// <summary>Vycentruje okno na pracovní plochu; větší okno přiloží k levému hornímu rohu, ať titulek zůstane vidět.</summary>
    internal static void CenterIn(Window window, Rect area)
    {
        var size = new Size(Known(window.Width, window.ActualWidth), Known(window.Height, window.ActualHeight));
        var topLeft = OwnerWindowCentering.ComputeTopLeft(area.TopLeft, area.Size, size);
        window.Left = Math.Max(area.Left, topLeft.X);
        window.Top = Math.Max(area.Top, topLeft.Y);
    }

    private static double Known(double size, double actual) => double.IsFinite(size) && size > 0 ? size : actual;

    private static IEnumerable<Window> OtherVisibleWindows(Application? app, Window window)
    {
        if (app is null) yield break;
        foreach (Window w in app.Windows)
            if (!ReferenceEquals(w, window) && w.IsVisible) yield return w;
    }

    // Souřadnice z GetMonitorInfo jsou v prostoru DPI režimu procesu, stejně jako Left/Top okna přepočtené jeho DPI.
    private static Rect ToDips(Rect px, Visual visual)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        return new Rect(px.X / dpi.DpiScaleX, px.Y / dpi.DpiScaleY, px.Width / dpi.DpiScaleX, px.Height / dpi.DpiScaleY);
    }

    private static bool IsConnected(string device) => FindWorkAreaPx(device) is not null;

    private static string? CursorMonitor() =>
        GetCursorPos(out var pt) ? DeviceNameOf(MonitorFromPoint(pt, MonitorDefaultToNull)) : null;

    private static Rect? FindWorkAreaPx(string device)
    {
        Rect? found = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            if (!TryGetInfo(hMonitor, out var info) || !string.Equals(info.szDevice, device, StringComparison.OrdinalIgnoreCase))
                return true;
            var w = info.rcWork;
            found = new Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string? DeviceNameOf(IntPtr hMonitor) =>
        hMonitor != IntPtr.Zero && TryGetInfo(hMonitor, out var info) ? info.szDevice : null;

    private static bool TryGetInfo(IntPtr hMonitor, out MONITORINFOEX info)
    {
        info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(hMonitor, ref info);
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}
