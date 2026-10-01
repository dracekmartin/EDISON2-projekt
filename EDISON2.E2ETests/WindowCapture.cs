using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EDISON2.E2ETests;

/// <summary>Snímek obsahu okna přes PrintWindow - funguje i pro okno zakryté jiným, bez krádeže fokusu.</summary>
internal static class WindowCapture
{
    // Bez PW_RENDERFULLCONTENT vrací WPF (DirectComposition) okno černé.
    private const uint PwRenderFullContent = 2;

    public static void ToFile(nint hwnd, string path)
    {
        if (hwnd == 0 || !GetWindowRect(hwnd, out var rect)) return;
        using var bitmap = new Bitmap(Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            try { PrintWindow(hwnd, hdc, PwRenderFullContent); }
            finally { graphics.ReleaseHdc(hdc); }
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);
}
