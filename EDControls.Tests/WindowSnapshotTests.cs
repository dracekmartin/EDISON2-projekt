using System.Threading;
using System.Windows;
using System.Windows.Controls;
using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Snímek okna do PNG. Kreslí se přes <c>RenderTargetBitmap</c>, tedy **ne** snímek
/// obrazovky - cizí aplikace se do přílohy nemají jak dostat.
/// </summary>
public class WindowSnapshotTests
{
    /// <summary>Změřený a naaranžovaný prvek — nezobrazené okno má ActualWidth nulovou.</summary>
    private static FrameworkElement Laid(double width, double height, UIElement? content = null)
    {
        var root = new Border { Width = width, Height = height, Child = content };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        return root;
    }

    [Fact]
    public void Capture_ProducesPng()
    {
        RunSta(() =>
        {
            var png = WindowSnapshot.TryCapture(Laid(300, 200, new TextBlock { Text = "ahoj" }));

            Assert.NotNull(png);
            // PNG magic number - ověřuje, že to je opravdu obrázek, ne prázdný buffer.
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], png!.Take(4).ToArray());
        });
    }

    [Fact]
    public void Capture_ScalesDownOversizedWindow()
    {
        RunSta(() =>
        {
            var png = WindowSnapshot.TryCapture(Laid(5000, 400));

            Assert.NotNull(png);
            // Strop delší strany je 1600 px; nezmenšený snímek by přílohu zbytečně nafoukl.
            using var ms = new System.IO.MemoryStream(png!);
            var decoder = System.Windows.Media.Imaging.BitmapFrame.Create(ms);
            Assert.True(decoder.PixelWidth <= 1600, $"šířka {decoder.PixelWidth} přesáhla strop");
        });
    }

    [Fact]
    public void Capture_ZeroSizedElement_ReturnsNull()
    {
        RunSta(() => Assert.Null(WindowSnapshot.TryCapture(new Border())));
    }

    [Fact]
    public void Capture_WithoutApplication_ReturnsEmpty()
    {
        // V testech (a v raném startupu) Application.Current neexistuje - nesmí to spadnout.
        RunSta(() => Assert.Empty(WindowSnapshot.Capture()));
    }

    // Spustí akci na dedikovaném STA vlákně (Window to vyžaduje) a propaguje výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
