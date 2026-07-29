using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
// Aliasy proti ambient System.Windows.Forms/Drawing (UseWindowsForms je zapnuté pro FontDialog).
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace EDISONMAIN2.MiniApps;

public partial class SupportCarouselMiniAppView : UserControl
{
    public SupportCarouselMiniAppView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is SupportCarouselMiniAppViewModel vm) vm.EngagementProbe = SampleEngagement;
        };
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        (DataContext as SupportCarouselMiniAppViewModel)?.UpdateViewportWidth(e.NewSize.Width);

    /// <summary>Hover nelze brát z <c>IsMouseOver</c> — obsah stránek je HWND WebView2 (airspace),
    /// WPF nad ním mouse eventy nedostane; proto fyzická pozice kurzoru proti bounds controlu.
    /// Pohyb kurzoru odlišuje čtení od odloženého kurzoru (VM podle něj nechá pauzu vypršet).</summary>
    private CarouselEngagement SampleEngagement()
    {
        if (!IsVisible || PresentationSource.FromVisual(this) is null) return default;
        var cursor = System.Windows.Forms.Control.MousePosition;
        bool moved = cursor != _lastCursor;
        _lastCursor = cursor;
        bool inside = new Rect(RenderSize).Contains(PointFromScreen(new Point(cursor.X, cursor.Y)))
                      && IsOwnWindowAt(cursor);
        return new CarouselEngagement(inside, IsKeyboardFocusWithin, moved);
    }

    // Souřadnice nestačí: cizí okno (legacy agenda) může shell překrývat a práce myší „nad karuselem"
    // by pauzu držela navěky. Vlastní okno stačí viditelné, nemusí být aktivní.
    private bool IsOwnWindowAt(System.Drawing.Point screenPoint)
    {
        if (Window.GetWindow(this) is not { } window) return false;
        nint ours = new WindowInteropHelper(window).Handle;
        nint hit = WindowFromPoint(screenPoint);
        return ours != 0 && hit != 0 && GetAncestor(hit, GA_ROOT) == ours;
    }

    private System.Drawing.Point _lastCursor;

    private const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(System.Drawing.Point point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint hwnd, uint flags);
}
