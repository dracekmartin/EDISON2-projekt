using System.Windows;
// Aliasy proti ambient System.Windows.Forms / System.Drawing (_wpftmp importuje WinForms).
using Panel = System.Windows.Controls.Panel;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Mřížka dlaždic s <b>absolutní</b> pozicí: každé dítě jde na svůj
/// <see cref="ColumnProperty">Column</see>/<see cref="RowProperty">Row</see> (0-based).
/// Záměrně nepřeskládává podle šířky - uživatelovo rozložení musí přežít resize panelu,
/// přesah řeší scroll hostitele (desired size = obsah, ne available). Prázdné buňky = mezery.
/// </summary>
public sealed class TileGridPanel : Panel
{
    public double CellWidth { get; set; } = 140;
    public double CellHeight { get; set; } = 84;

    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(TileGridPanel),
        new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row", typeof(int), typeof(TileGridPanel),
        new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void SetColumn(DependencyObject o, int value) => o.SetValue(ColumnProperty, value);
    public static int GetColumn(DependencyObject o) => (int)o.GetValue(ColumnProperty);

    public static void SetRow(DependencyObject o, int value) => o.SetValue(RowProperty, value);
    public static int GetRow(DependencyObject o) => (int)o.GetValue(RowProperty);

    // Čistá matematika mřížky vytažená do statických helperů - testovatelná bez WPF instance
    // (Panel vyžaduje STA vlákno).
    internal static int ColumnsFor(double width, double cellWidth) => Math.Max(1, (int)(width / cellWidth));

    protected override Size MeasureOverride(Size availableSize)
    {
        var cell = new Size(CellWidth, CellHeight);
        int maxCol = -1, maxRow = -1;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(cell);
            maxCol = Math.Max(maxCol, GetColumn(child));
            maxRow = Math.Max(maxRow, GetRow(child));
        }
        return new Size((maxCol + 1) * CellWidth, (maxRow + 1) * CellHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
            child.Arrange(new Rect(GetColumn(child) * CellWidth, GetRow(child) * CellHeight, CellWidth, CellHeight));
        return finalSize;
    }

    /// <summary>Buňka pod bodem (pro drop). Vpravo/dole neomezená - přesah je legitimní pozice.</summary>
    public (int Column, int Row) CellAt(Point p) => CellAtFor(p.X, p.Y, CellWidth, CellHeight);

    internal static (int Column, int Row) CellAtFor(double x, double y, double cellWidth, double cellHeight) =>
        (Math.Max(0, (int)(x / cellWidth)), Math.Max(0, (int)(y / cellHeight)));
}
