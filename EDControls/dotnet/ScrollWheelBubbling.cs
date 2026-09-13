using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls;

/// <summary>
/// Attached <c>ScrollWheelBubbling.IsEnabled="True"</c> pro prvek s vlastním ScrollViewerem (DataGrid,
/// ListBox) uvnitř rolovatelného rodiče: kolečko, které vnitřní prvek nemá kam použít (je na kraji nebo
/// nemá co rolovat), předá rodiči. WPF ScrollViewer si totiž MouseWheel označí za handled vždy, takže
/// vnější panel se pod kurzorem nad seznamem nehnul - rolovat šlo jen nad jeho posuvníkem.
/// </summary>
public static class ScrollWheelBubbling
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ScrollWheelBubbling), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.PreviewMouseWheel -= OnPreviewMouseWheel;
        if ((bool)e.NewValue) el.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not UIElement el) return;
        var inner = el as ScrollViewer ?? FindScrollViewer(el);
        if (inner is not null && CanScroll(inner.VerticalOffset, inner.ScrollableHeight, e.Delta)) return;

        e.Handled = true;
        var parent = VisualTreeHelper.GetParent(el) as UIElement ?? (el as FrameworkElement)?.Parent as UIElement;
        parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = el,
        });
    }

    /// <summary>Vnitřní ScrollViewer kolečko použije: delta &lt; 0 = dolů (je kam), delta &gt; 0 = nahoru (není na začátku).</summary>
    internal static bool CanScroll(double verticalOffset, double scrollableHeight, int delta) =>
        delta < 0 ? verticalOffset < scrollableHeight : verticalOffset > 0;

    // První ScrollViewer do šířky (u DataGridu/ListBoxu ten ze šablony, ne případný v buňce).
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is ScrollViewer sv) return sv;
                queue.Enqueue(child);
            }
        }
        return null;
    }
}
