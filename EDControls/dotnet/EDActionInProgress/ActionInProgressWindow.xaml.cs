using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
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
    // Počet souběžně padajících kusů ve fall režimu (šablony sady se střídají dokola).
    private const int FallPieceCount = 5;

    private readonly Window _owner;
    private readonly Random _rng = new();
    // Fall režim: kusy sady + jejich transformy; prázdné = běžný walk sprite.
    private readonly List<(TranslateTransform Transform, FrameworkElement Element)> _fallPieces = [];
    private bool _active;
    private bool _fallRunning;

    public ActionInProgressWindow(Window owner, string? spriteKey = null)
    {
        InitializeComponent();
        _owner = owner;
        Owner = owner;
        owner.LocationChanged += OnOwnerBoundsChanged;
        owner.SizeChanged += OnOwnerSizeChanged;
        owner.StateChanged += OnOwnerStateChanged;
        // SizeToContent - velikost karty se změní i za běhu (font/theme swap).
        SizeChanged += OnOwnSizeChanged;
        PickSprite(spriteKey);   // jednou per okno (= per akci)
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

    // Explicitní klíč z VM (ActionSpriteKey) vybere vyhrazenou SADU "Sprite.<klíč>.*" (fall
    // režim); jinak náhodně z poolu "Sprite.*" bez další tečky (kusy sad v poolu nejsou).
    // x:Shared="False" → přístup k resource vrací pokaždé novou instanci (žádný konflikt rodiče).
    private void PickSprite(string? preferred)
    {
        if (preferred is not null && TrySetupFallSet(SpriteKeyPrefix + preferred + ".")) return;

        var pool = Resources.Keys.OfType<string>()
            .Where(k => k.StartsWith(SpriteKeyPrefix, StringComparison.Ordinal)
                        && k.IndexOf('.', SpriteKeyPrefix.Length) < 0)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
        if (pool.Length == 0) return;
        SpriteHost.Content = (UIElement)FindResource(pool[_rng.Next(pool.Length)]);
    }

    // Naplní průhled padajícími kusy sady (šablony dokola, každý kus vlastní transform).
    // Klidová Y = -výška (kus schovaný nad průhledem, jako X=-60 u walk spritu).
    private bool TrySetupFallSet(string setPrefix)
    {
        var pieceKeys = Resources.Keys.OfType<string>()
            .Where(k => k.StartsWith(setPrefix, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
        if (pieceKeys.Length == 0) return false;

        var canvas = new Canvas { Width = SpriteViewport.Width, Height = SpriteViewport.Height };
        for (int i = 0; i < FallPieceCount; i++)
        {
            var element = (FrameworkElement)FindResource(pieceKeys[i % pieceKeys.Length]);
            var transform = new TranslateTransform { Y = -element.Height };
            element.RenderTransform = transform;
            canvas.Children.Add(element);
            _fallPieces.Add((transform, element));
        }
        SpriteHost.Content = canvas;
        // Klidové X=-60 hosta je pro walk sprite; fall canvas musí sedět na průhledu.
        SlideTranslate.X = 0;
        return true;
    }

    // Náhodné padání storyboard s pevnými hodnotami neumí - kusy animuje code-behind:
    // každý kus po dopadu dostane nové náhodné X a tempo (Completed → další pád).
    private void StartFall()
    {
        _fallRunning = true;
        foreach (var piece in _fallPieces) DropPiece(piece, initial: true);
    }

    private void StopFall()
    {
        _fallRunning = false;
        // Odstranění clocku vrací Y na klidovou hodnotu (kus schovaný nad průhledem).
        foreach (var piece in _fallPieces) piece.Transform.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private void DropPiece((TranslateTransform Transform, FrameworkElement Element) piece, bool initial)
    {
        Canvas.SetLeft(piece.Element, _rng.NextDouble() * (SpriteViewport.Width - piece.Element.Width));
        // Bankovky padají pomaleji než mince; první vlna se rozprostře delším rozestupem.
        double seconds = piece.Element.Height >= 24 ? 1.8 + _rng.NextDouble() * 0.8 : 1.1 + _rng.NextDouble() * 0.6;
        var fall = new DoubleAnimation(-piece.Element.Height, SpriteViewport.Height, TimeSpan.FromSeconds(seconds))
        {
            BeginTime = TimeSpan.FromSeconds(_rng.NextDouble() * (initial ? 1.6 : 0.7)),
        };
        fall.Completed += (_, _) => { if (_fallRunning) DropPiece(piece, initial: false); };
        piece.Transform.BeginAnimation(TranslateTransform.YProperty, fall);
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

    // Storyboard řídí code-behind - Begin(..., isControllable) při aktivaci resetuje maskota.
    private void ToggleWalk(bool active)
    {
        if (_fallPieces.Count > 0)
        {
            if (active) StartFall(); else StopFall();
            return;
        }
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
