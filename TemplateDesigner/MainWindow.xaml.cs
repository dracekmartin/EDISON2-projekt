using System.Windows;
using SkiaSharp;

namespace TemplateDesigner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        skCanvas.PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, object e)
    {
        var args = (dynamic)e;
        SKSurface surface = args.Surface;
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.Green);

        using var paint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = true };
        canvas.DrawRect(new SKRect(50, 50, 300, 200), paint);

        using var font = new SKFont { Size = 24 };
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText("Hello Template!", 60, 250, font, textPaint);
    }
}