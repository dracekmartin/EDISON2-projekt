using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EDControls.EDTheme;

/// <summary>
/// Ztmaví/zesvětlí OS titulkovou lištu okna (caption) podle dark modu. Lištu kreslí Windows
/// DWM, ne WPF — nereaguje na EDTheme tokeny, proto se nastavuje imperativně přes
/// <c>DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)</c>. Volá <see cref="ThemeService"/>
/// v <c>OnWindowLoaded</c> (nové okno) a v <c>Apply</c> (runtime přepnutí režimu na všech oknech).
/// </summary>
internal static class WindowTitleBar
{
    // Win11 / Win10 20H1+ hodnota; starší Win10 buildy (1809/1903) používaly 19. Zkusíme 20,
    // při chybě 19 — a celé v try/catch, aby starý OS / RDP bez DWM nikdy neshodil UI.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        if (window is null) return;
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return; // Ještě není HWND — caption se doladí při dalším průchodu.

        int value = dark ? 1 : 0;
        try
        {
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_LEGACY, ref value, sizeof(int));
        }
        catch
        {
            // OS bez podpory atributu — titulek zůstane světlý, klientská plocha je i tak tmavá.
        }
    }
}
