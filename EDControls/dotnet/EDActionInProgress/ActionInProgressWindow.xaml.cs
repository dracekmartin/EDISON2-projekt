using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace EDControls.EDActionInProgress;

/// <summary>
/// Karta „Akce probíhá" ve vlastním top-level okně - jediná cesta, jak ji vykreslit nad
/// HWND obsahem (WebView2 widgety): child HWND nikdy nepřekreslí cizí top-level a owned
/// okno drží window manager vždy nad ownerem, bez Topmost (žádný únik nad cizí aplikace;
/// WPF Popup by topmost byl). Okno je jen velikosti karty - celoplošné AllowsTransparency
/// okno by na 4K znamenalo ~33MB layered surface překreslovanou animací 60 fps.
/// Klik propadá skrz (WS_EX_TRANSPARENT) na DimAdorner / widgety, aktivaci nekrade.
/// Vytváří se per akci (ne recyklace přes Hide/Show) - skryté okno by na příštím Show()
/// na jeden frame přehrálo poslední složený obraz (sprite v půli cesty z minula).
/// </summary>
internal sealed partial class ActionInProgressWindow : Window, EDViewModel.IOverlayWindow
{
    // Prefix klíče spritů v Window.Resources - nový sprite stačí přidat do XAML s tímto prefixem.
    private const string SpriteKeyPrefix = "Sprite.";

    private readonly Window _owner;
    private readonly Random _rng = new();
    private string[]? _spriteKeys;
    private bool _active;

    public ActionInProgressWindow(Window owner)
    {
        InitializeComponent();
        _owner = owner;
        Owner = owner;
        owner.LocationChanged += OnOwnerBoundsChanged;
        owner.SizeChanged += OnOwnerSizeChanged;
        owner.StateChanged += OnOwnerStateChanged;
        // SizeToContent - velikost karty se změní i za běhu (font/theme swap).
        SizeChanged += OnOwnSizeChanged;
        PickRandomSprite();   // jednou per okno (= per akci)
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GWL_EXSTYLE,
            GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void ShowOverlay()
    {
        _active = true;
        // Minimalizovaný owner - Show() by kartu vykreslil nad plochu; dožene OnOwnerStateChanged.
        if (_owner.WindowState == WindowState.Minimized) return;
        Show();
        Reposition();
        ToggleWalk(true);
    }

    // Náhodně vybere jeden ze spritů z Window.Resources (klíče s prefixem "Sprite."). Klíče
    // se počítají jednou; přidání dalšího spritu do XAML je tak jediná potřebná změna.
    // x:Shared="False" → FindResource vrací pokaždé novou instanci (žádný konflikt rodiče).
    private void PickRandomSprite()
    {
        _spriteKeys ??= Resources.Keys.OfType<string>()
            .Where(k => k.StartsWith(SpriteKeyPrefix, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
        if (_spriteKeys.Length == 0) return;
        SpriteHost.Content = (UIElement)FindResource(_spriteKeys[_rng.Next(_spriteKeys.Length)]);
    }

    // Minimalizace ownera → Hide (OS auto-hide owned oken WPF IsVisible nevidí a Forever
    // storyboard by tikal dál, držel dispatcher vzhůru). Restore → zpět nahoru.
    private void OnOwnerStateChanged(object? sender, EventArgs e)
    {
        if (_owner.WindowState == WindowState.Minimized)
        {
            ToggleWalk(false);
            Hide();
        }
        else if (_active)
        {
            Show();
            Reposition();
            ToggleWalk(true);
        }
    }

    private void OnOwnerBoundsChanged(object? sender, EventArgs e) => Reposition();
    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();
    private void OnOwnSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();

    // Střed KLIENTSKÉ oblasti ownera. PointToScreen(0,0) = levý horní roh klientské oblasti
    // v device px - funguje i pro Maximized, kde Left/Top drží restore bounds
    // (WindowStartupLocation=CenterOwner je proto pro maximalizovaný owner nepoužitelné).
    private void Reposition()
    {
        if (!IsVisible) return;
        if (PresentationSource.FromVisual(_owner)?.CompositionTarget is not { } ct) return;
        if (!GetClientRect(new WindowInteropHelper(_owner).Handle, out var rc)) return;

        var toDips = ct.TransformFromDevice;
        var origin = toDips.Transform(_owner.PointToScreen(new Point(0, 0)));
        var client = toDips.Transform(new Point(rc.Right, rc.Bottom)); // GetClientRect: Left/Top = 0

        Left = origin.X + (client.X - ActualWidth) / 2;
        Top = origin.Y + (client.Y - ActualHeight) / 2;
    }

    // Storyboard řídí code-behind - Begin(..., isControllable) při aktivaci resetuje kapybaru.
    private void ToggleWalk(bool active)
    {
        var walk = (Storyboard)Resources["WalkAnimation"];
        if (active)
            walk.Begin(this, true);
        else
            walk.Stop(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        ToggleWalk(false);   // zastav Forever clock při teardownu
        _owner.LocationChanged -= OnOwnerBoundsChanged;
        _owner.SizeChanged -= OnOwnerSizeChanged;
        _owner.StateChanged -= OnOwnerStateChanged;
        base.OnClosed(e);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;  // klik propadá skrz
    private const int WS_EX_NOACTIVATE = 0x08000000;   // nekrade aktivaci
    private const int WS_EX_TOOLWINDOW = 0x00000080;   // mimo Alt+Tab

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT rc);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
