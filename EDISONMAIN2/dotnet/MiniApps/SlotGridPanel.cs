using System.Windows;
// Aliasy proti ambient System.Windows.Forms / System.Drawing (_wpftmp importuje WinForms).
using Panel = System.Windows.Controls.Panel;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Responzivní mřížka dlaždic: každé dítě umístí na svůj <see cref="SlotProperty">Slot</see>
/// (0-based, row-major). Počet sloupců = šířka / <see cref="CellWidth"/> → přizpůsobí se šířce panelu.
/// Prázdné sloty (mezi obsazenými) zůstávají prázdné = mezery. Změna Slotu přeskládá (AffectsParentArrange).
/// </summary>
public sealed class SlotGridPanel : Panel
{
    public double CellWidth { get; set; } = 140;
    public double CellHeight { get; set; } = 84;

    public static readonly DependencyProperty SlotProperty = DependencyProperty.RegisterAttached(
        "Slot", typeof(int), typeof(SlotGridPanel),
        new FrameworkPropertyMetadata(0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void SetSlot(DependencyObject o, int value) => o.SetValue(SlotProperty, value);
    public static int GetSlot(DependencyObject o) => (int)o.GetValue(SlotProperty);

    private int Columns(double width) => Math.Max(1, (int)(width / CellWidth));

    protected override Size MeasureOverride(Size availableSize)
    {
        var cell = new Size(CellWidth, CellHeight);
        int maxSlot = -1;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(cell);
            maxSlot = Math.Max(maxSlot, GetSlot(child));
        }
        double width = double.IsInfinity(availableSize.Width) ? CellWidth : availableSize.Width;
        int cols = Columns(width);
        int rows = maxSlot < 0 ? 0 : (maxSlot / cols) + 1;
        return new Size(cols * CellWidth, rows * CellHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int cols = Columns(finalSize.Width);
        foreach (UIElement child in InternalChildren)
        {
            int slot = GetSlot(child);
            child.Arrange(new Rect((slot % cols) * CellWidth, (slot / cols) * CellHeight, CellWidth, CellHeight));
        }
        return finalSize;
    }

    /// <summary>Slot pod bodem (pro drop): row-major index podle aktuálního počtu sloupců.</summary>
    public int SlotAt(Point p)
    {
        int cols = Columns(ActualWidth);
        int c = Math.Max(0, Math.Min(cols - 1, (int)(p.X / CellWidth)));
        int r = Math.Max(0, (int)(p.Y / CellHeight));
        return r * cols + c;
    }
}
