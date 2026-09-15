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
    // Režim karty určuje klíč sady: bez klíče (produkce) se točí kolečko, s klíčem běží maskoti.
    // Pixel-art styl se v produkci neosvědčil (zákaznická reklamace kapybary), sady ale zůstávají
    // celé - pustí je „Test funkcí ▸ Akce probíhá", takže je jde kdykoli vrátit i do produkce.
    // Doba jedné otáčky kolečka.
    private static readonly TimeSpan SpinnerTurnDuration = TimeSpan.FromSeconds(1.1);

    // Sprity žijí v sadách "Sprite.<Sada>.<Kus>" ve Window.Resources; nový kus = jen XAML.
    private const string SpriteKeyPrefix = "Sprite.";
    // Výchozí sada (overlay bez explicitního klíče): dopravní prostředky, režim jízdy.
    private const string DefaultSetKey = "Vehicles";
    // Tag prvního kusu sady = režim celé sady: "Fall" padá shora, jinak jízda přes průhled.
    private const string FallTag = "Fall";
    // Počet souběžně padajících kusů ve fall režimu (šablony sady se střídají dokola).
    private const int FallPieceCount = 5;
    // Délka jednoho přejezdu průhledu; po něm se kus vymění za jiný náhodný ze sady.
    private static readonly TimeSpan DrivePassDuration = TimeSpan.FromSeconds(2.5);

    private readonly Window _owner;
    private readonly Random _rng = new();
    // Fall režim: kusy sady + jejich transformy; prázdné = režim jízdy.
    private readonly List<(TranslateTransform Transform, FrameworkElement Element)> _fallPieces = [];
    private string[] _pieceKeys = [];
    private bool _active;
    private bool _running;
    private bool _driveLeftToRight = true;
    private int _lastPieceIndex = -1;

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
        PickSet(spriteKey);   // jednou per okno (= per akci)
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

    // Vybere sadu podle klíče z VM (ActionSpriteKey), bez klíče / pro neznámý klíč výchozí
    // dopravní prostředky. Režim určuje Tag prvního kusu ("Fall" = pád, jinak jízda).
    // x:Shared="False" → přístup k resource vrací pokaždé novou instanci (žádný konflikt rodiče).
    private void PickSet(string? preferred)
    {
        _pieceKeys = SetPieceKeys(preferred);
        if (_pieceKeys.Length == 0) return;   // bez sady zůstane viditelné kolečko
        Spinner.Visibility = Visibility.Collapsed;
        SpriteViewport.Visibility = Visibility.Visible;
        if (Equals((Resources[_pieceKeys[0]] as FrameworkElement)?.Tag, FallTag))
            BuildFallPieces();
    }

    // Stop přes BeginAnimation(.., null) vrátí úhel na klidovou hodnotu - stejný vzor jako
    // u spritů, aby skryté okno nedrželo tikající clock.
    private void ToggleSpinner(bool active)
    {
        if (Spinner.Visibility != Visibility.Visible) return;
        SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, active
            ? new DoubleAnimation(0, 360, SpinnerTurnDuration) { RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    private string[] SetPieceKeys(string? set) => set is null ? [] :
        [.. Resources.Keys.OfType<string>()
            .Where(k => k.StartsWith(SpriteKeyPrefix + set + ".", StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)];

    // Naplní průhled padajícími kusy sady (šablony dokola, každý kus vlastní transform).
    // Klidová Y = -výška (kus schovaný nad průhledem, jako X=-60 u jízdy).
    private void BuildFallPieces()
    {
        var canvas = new Canvas { Width = SpriteViewport.Width, Height = SpriteViewport.Height };
        for (int i = 0; i < FallPieceCount; i++)
        {
            var element = (FrameworkElement)FindResource(_pieceKeys[i % _pieceKeys.Length]);
            var transform = new TranslateTransform { Y = -element.Height };
            element.RenderTransform = transform;
            canvas.Children.Add(element);
            _fallPieces.Add((transform, element));
        }
        SpriteHost.Content = canvas;
        // Klidové X=-60 hosta je pro režim jízdy; fall canvas musí sedět na průhledu.
        SlideTranslate.X = 0;
    }

    // Náhodu (výběr kusu, pozice, tempo) storyboard s pevnými hodnotami neumí - obě animace
    // řídí code-behind: DoubleAnimation per průjezd/pád a v Completed další kolo.
    private void StartFall()
    {
        foreach (var piece in _fallPieces) DropPiece(piece, initial: true);
    }

    private void StopFall()
    {
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
        fall.Completed += (_, _) => { if (_running) DropPiece(piece, initial: false); };
        piece.Transform.BeginAnimation(TranslateTransform.YProperty, fall);
    }

    // Jízda: kus přejede průhled, na kraji ho vystřídá jiný náhodný ze sady a jede zpátky.
    // Směr se střídá, kus je vždy čelem po směru jízdy (sprity se kreslí čelem doprava).
    private void StartDrive() => DrivePass();

    private void StopDrive()
    {
        // Odstranění clocku vrací X na klidovou -60 (kus schovaný vlevo mimo záběr).
        SlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private void DrivePass()
    {
        var piece = NextDrivePiece();
        SpriteHost.Content = piece;
        FlipTransform.CenterX = piece.Width / 2;
        FlipTransform.ScaleX = _driveLeftToRight ? 1 : -1;
        double offscreen = -piece.Width;
        var pass = _driveLeftToRight
            ? new DoubleAnimation(offscreen, SpriteViewport.Width, DrivePassDuration)
            : new DoubleAnimation(SpriteViewport.Width, offscreen, DrivePassDuration);
        pass.Completed += (_, _) =>
        {
            if (!_running) return;
            _driveLeftToRight = !_driveLeftToRight;
            DrivePass();
        };
        SlideTranslate.BeginAnimation(TranslateTransform.XProperty, pass);
    }

    private FrameworkElement NextDrivePiece()
    {
        int i;
        // Bez okamžitého opakování - výměna má být vidět.
        do { i = _rng.Next(_pieceKeys.Length); } while (_pieceKeys.Length > 1 && i == _lastPieceIndex);
        _lastPieceIndex = i;
        return (FrameworkElement)FindResource(_pieceKeys[i]);
    }

    // Minimalizace ownera → Hide (OS auto-hide owned oken WPF IsVisible nevidí a animační
    // clocky by tikaly dál, držely dispatcher vzhůru). Restore → zpět nahoru.
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

    // Start/stop animace jen podle viditelnosti - skrytého okna se clocky nesmí držet
    // (tikaly by dál a držely dispatcher vzhůru).
    private void ToggleWalk(bool active)
    {
        _running = active;
        ToggleSpinner(active);
        if (_fallPieces.Count > 0)
        {
            if (active) StartFall(); else StopFall();
        }
        else if (_pieceKeys.Length > 0)
        {
            if (active) StartDrive(); else StopDrive();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ToggleWalk(false);   // zastav animační clocky při teardownu
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
