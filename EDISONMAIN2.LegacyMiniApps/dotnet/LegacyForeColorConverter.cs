using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EDControls.EDTheme;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Barva textu řádku z legacy serveru (<c>"#RRGGBB"</c>) → <see cref="SolidColorBrush"/>.
/// Ve světlém režimu 1:1 (parita se starým Edisonem), v tmavém se zesvětlí - legacy palety
/// jsou navržené na bílé pozadí a na tmavém by byly nečitelné. Prázdné / neplatné →
/// <see cref="DependencyProperty.UnsetValue"/>, tedy fallback na <c>EDTheme.Text.Primary</c>
/// ze stylu (stejný kontrakt jako <c>HexToBrushConverter</c>).
/// </summary>
public sealed class LegacyForeColorConverter : IValueConverter
{
    /// <summary>Když token nejde přečíst (testy bez Application): dark <c>EDTheme.Bg.Surface</c>.</summary>
    private static readonly Color DarkSurfaceFallback = Color.FromRgb(0x2D, 0x2D, 0x30);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex)) return DependencyProperty.UnsetValue;

        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }

        if (ThemeService.Current == AppTheme.Dark)
            color = LegacyForeColor.Adapt(color, SurfaceColor());

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Color SurfaceColor() =>
        Application.Current?.TryFindResource("EDTheme.Bg.Surface.Color") is Color c ? c : DarkSurfaceFallback;
}

/// <summary>
/// Zesvětlení legacy barvy na čitelnou úroveň. Inverze by z červené udělala azurovou a zahodila
/// sémantiku (červená = problém), proto se mění jen světlost v HSL - odstín a sytost zůstávají.
/// </summary>
public static class LegacyForeColor
{
    /// <summary>WCAG AA pro běžný text.</summary>
    private const double MinContrast = 4.5;

    /// <summary>Strop světlosti - výš už barva splývá s bílou a odstín přestane být čitelný.</summary>
    private const double MaxLightness = 0.85;

    private const double Step = 0.02;

    /// <summary>
    /// Vrátí <paramref name="fg"/> beze změny, pokud proti <paramref name="bg"/> stačí kontrast;
    /// jinak nejtmavší variantu se stejným odstínem, která na 4,5:1 dosáhne. Když ani strop
    /// nestačí (velmi tmavé pozadí není náš případ), vrátí barvu při stropu.
    /// </summary>
    public static Color Adapt(Color fg, Color bg)
    {
        if (Contrast(fg, bg) >= MinContrast) return fg;

        var (h, s, l) = ToHsl(fg);
        for (double candidate = l + Step; candidate <= MaxLightness; candidate += Step)
        {
            var adapted = FromHsl(h, s, candidate);
            if (Contrast(adapted, bg) >= MinContrast) return adapted;
        }
        return FromHsl(h, s, MaxLightness);
    }

    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Channel(byte raw)
    {
        double v = raw / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2;
        double delta = max - min;
        if (delta == 0) return (0, 0, l);

        double s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);
        double h;
        if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / delta + 2;
        else h = (r - g) / delta + 4;
        return (h / 6, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s == 0)
        {
            byte gray = Round(l);
            return Color.FromRgb(gray, gray, gray);
        }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return Color.FromRgb(Round(Hue(p, q, h + 1.0 / 3)), Round(Hue(p, q, h)), Round(Hue(p, q, h - 1.0 / 3)));
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private static byte Round(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
}
