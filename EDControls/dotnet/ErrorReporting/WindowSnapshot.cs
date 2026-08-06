using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EDControls.ErrorReporting;

/// <summary>
/// Snímek oken aplikace jako příloha hlášení.
/// <para>
/// Záměrně **nesnímá obrazovku** (na rozdíl od legacy <c>CaptureScreenToFile</c>, které sešije
/// všechny monitory): kreslí jen vlastní okna přes <see cref="RenderTargetBitmap"/>, takže se
/// cizí aplikace do přílohy nemají jak dostat. Není to politika, je to vlastnost metody.
/// </para>
/// <para>
/// Důsledek: HWND obsah se nevykreslí, takže WebView2 miniaplikace shellu zůstanou v snímku
/// prázdné. Snímek proto nikdy není přesně to, co uživatel viděl.
/// </para>
/// </summary>
public static class WindowSnapshot
{
    /// <summary>Delší strana snímku v pixelech - drží přílohu v rozumné velikosti.</summary>
    private const double MaxDimension = 1600;

    /// <summary>Nejvýš dvě přílohy (aktivní okno + hlavní okno), ať payload neroste.</summary>
    private const int MaxWindows = 2;

    /// <summary>
    /// Nasnímá aktivní a hlavní okno. **Volat na UI vlákně.** Nikdy nehází - snímek je
    /// bonus k hlášení, ne jeho podmínka.
    /// </summary>
    public static IReadOnlyList<ErrorAttachment> Capture()
    {
        try
        {
            if (Application.Current is not { } app) return [];

            var windows = app.Windows.OfType<Window>()
                .Where(IsCapturable)
                .OrderByDescending(w => w.IsActive)
                .Take(MaxWindows)
                .ToList();

            var result = new List<ErrorAttachment>(windows.Count);
            for (var i = 0; i < windows.Count; i++)
            {
                if (TryCapture(windows[i]) is not { } png) continue;
                var name = SafeFileName(windows[i]);
                result.Add(new ErrorAttachment($"okno{i + 1}_{name}.png", "image/png",
                                               Convert.ToBase64String(png)));
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsCapturable(Window w) =>
        w.IsVisible && w.ActualWidth >= 1 && w.ActualHeight >= 1;

    /// <summary>
    /// Bere <see cref="FrameworkElement"/>, ne <see cref="Window"/> - vykreslení nic víc
    /// nepotřebuje a nezobrazené okno má <c>ActualWidth</c> nulovou, takže by šlo testovat
    /// jen s reálně otevřeným oknem. <c>internal</c> kvůli testům.
    /// </summary>
    internal static byte[]? TryCapture(FrameworkElement element)
    {
        try
        {
            var scale = Math.Min(1.0, MaxDimension / Math.Max(element.ActualWidth, element.ActualHeight));
            var pixelW = (int)Math.Round(element.ActualWidth * scale);
            var pixelH = (int)Math.Round(element.ActualHeight * scale);
            if (pixelW < 1 || pixelH < 1) return null;

            // dpi škálované spolu s rozměrem = render rovnou ve zmenšené velikosti,
            // bez druhého průchodu přes TransformedBitmap.
            var rtb = new RenderTargetBitmap(pixelW, pixelH, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(element);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string SafeFileName(Window window)
    {
        var raw = string.IsNullOrWhiteSpace(window.Name) ? window.GetType().Name : window.Name;
        var clean = new string([.. raw.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-')]);
        return clean.Length > 0 ? clean : "okno";
    }
}
