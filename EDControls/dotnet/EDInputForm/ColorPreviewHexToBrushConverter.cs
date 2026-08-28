using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EDControls.EDInputForm;

/// <summary>
/// Converter pro barevný náhled u Color pole - parsuje hex string `"#AARRGGBB"` (SkiaSharp formát)
/// na <see cref="SolidColorBrush"/>. Neplatný / null input → transparent brush.
/// </summary>
public class ColorPreviewHexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Transparent;

        // Hodnota je odkaz na data (např. „{BARVA_LINKY}") a host nedodal náhledovou barvu →
        // šrafování. Bez toho vypadá pole jako nenastavená bílá, což mate (zjištěno u #12997).
        var trimmed = hex.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '{' && trimmed[^1] == '}')
            return DataBoundBrush;

        string h = hex.TrimStart('#');
        byte a = 0xFF, r, g, b;
        try
        {
            if (h.Length == 8)
            {
                a = System.Convert.ToByte(h.Substring(0, 2), 16);
                r = System.Convert.ToByte(h.Substring(2, 2), 16);
                g = System.Convert.ToByte(h.Substring(4, 2), 16);
                b = System.Convert.ToByte(h.Substring(6, 2), 16);
            }
            else if (h.Length == 6)
            {
                r = System.Convert.ToByte(h.Substring(0, 2), 16);
                g = System.Convert.ToByte(h.Substring(2, 2), 16);
                b = System.Convert.ToByte(h.Substring(4, 2), 16);
            }
            else
            {
                return Brushes.Transparent;
            }
        }
        catch { return Brushes.Transparent; }

        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException("ColorPreviewHexToBrushConverter is one-way only");

    /// <summary>Šrafování pro „barva se vezme z dat" - vizuálně odlišné od bílé i od průhledné.</summary>
    private static readonly Brush DataBoundBrush = CreateDataBoundBrush();

    private static Brush CreateDataBoundBrush()
    {
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new System.Windows.Point(0, 4), new System.Windows.Point(4, 0)));
        geometry.Children.Add(new LineGeometry(new System.Windows.Point(-1, 1), new System.Windows.Point(1, -1)));
        geometry.Children.Add(new LineGeometry(new System.Windows.Point(3, 5), new System.Windows.Point(5, 3)));
        var brush = new DrawingBrush(new GeometryDrawing(null, new Pen(Brushes.Gray, 1), geometry))
        {
            TileMode = TileMode.Tile,
            Viewport = new System.Windows.Rect(0, 0, 4, 4),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }
}
