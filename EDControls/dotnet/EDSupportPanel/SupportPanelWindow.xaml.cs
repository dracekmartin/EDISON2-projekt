using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using EDControls.EDViewModel;

namespace EDControls.EDSupportPanel;

/// <summary>
/// Vysouvací panel Podpora ve vlastním owned okně — jediná cesta, jak úchyt i kartu
/// vykreslit nad HWND obsahem (WebView2 miniaplikace u pravého okraje shellu), který WPF
/// kvůli airspace nepřekreslí. Owned okno drží window manager nad ownerem i bez Topmost.
/// </summary>
internal sealed partial class SupportPanelWindow : Window, IOverlayWindow
{
    /// <summary>Vysunutí karty v px (0 = jen úchyt). Animuje se, změna přepočítá pozici okna.</summary>
    private static readonly DependencyProperty SlideOffsetProperty =
        DependencyProperty.Register(nameof(SlideOffset), typeof(double), typeof(SupportPanelWindow),
            new PropertyMetadata(0.0, (d, _) => ((SupportPanelWindow)d).Reposition()));

    private double SlideOffset => (double)GetValue(SlideOffsetProperty);

    private readonly Window _owner;
    private readonly EDServices _services;
    private readonly IPopupDisplayer _popups;
    private bool _isOpen;

    internal SupportPanelWindow(Window owner, EDServices services, IPopupDisplayer popups)
    {
        InitializeComponent();
        _owner = owner;
        _services = services;
        _popups = popups;
    }

    /// <summary>
    /// Napojení na ownera je až tady, ne v konstruktoru — dokud panel neprošel testem
    /// velikosti, nesmí se zapsat do <c>owner.OwnedWindows</c> ani na jeho eventy
    /// (nezobrazený panel by je držel po celý život okna).
    /// </summary>
    private void AttachToOwner()
    {
        Owner = _owner;
        _owner.LocationChanged += OnOwnerBoundsChanged;
        _owner.SizeChanged += OnOwnerSizeChanged;
        _owner.StateChanged += OnOwnerStateChanged;
        _owner.Closed += OnOwnerClosed;
        // SizeToContent="Height" → výška se mění při přepnutí velikosti fontu.
        SizeChanged += OnOwnSizeChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // NOACTIVATE: klik na tlačítko nesmí sebrat aktivaci ownerovi (myší zprávy okno
        // dostává dál, klávesnici panel nepotřebuje). TOOLWINDOW: mimo Alt+Tab.
        SetWindowLong(hwnd, GWL_EXSTYLE,
            GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Zobrazí panel. Vrací false, když se karta do klientské oblasti ownera nevejde —
    /// volající objekt zahodí (panel se k takovému oknu vůbec nepřipojí).
    /// </summary>
    internal bool TryShowPanel()
    {
        if (TryGetOwnerClient(out _, out _, out var w, out var h)
            && !SupportPanelGeometry.Fits(w, h, MeasuredHeight()))
            return false;

        AttachToOwner();
        ShowPanel();
        return true;
    }

    private void ShowPanel()
    {
        if (_owner.WindowState == WindowState.Minimized) return;   // dožene OnOwnerStateChanged
        Show();
        Reposition();
    }

    /// <summary>Výška karty. Před prvním zobrazením se musí změřit — <c>ActualHeight</c> je 0.</summary>
    private double MeasuredHeight()
    {
        if (ActualHeight > 0) return ActualHeight;
        Root.Measure(new Size(Root.Width, double.PositiveInfinity));
        return Root.DesiredSize.Height;
    }

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isOpen = !_isOpen;
        ArrowFlip.ScaleX = _isOpen ? -1 : 1;   // zavřeno ukazuje doleva (vytáhnout), otevřeno doprava
        BeginAnimation(SlideOffsetProperty, new DoubleAnimation(
            _isOpen ? SupportPanelGeometry.CardWidth : 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void Feedback_Click(object sender, RoutedEventArgs e) => Guarded(() =>
    {
        var ownerType = _owner.GetType();
        var version = ownerType.Assembly.GetName().Version?.ToString();
        SupportActions.OpenUrl(SupportActions.BuildFeedbackUrl(
            _services.MLRest?.Idfa,
            _services.MLRest?.LoginName,
            ownerType.Assembly.GetName().Name,
            SupportActions.BuildContext(ownerType.Name, _owner.Title, ownerType.Namespace, version)));
    });

    private void Portal_Click(object sender, RoutedEventArgs e) =>
        Guarded(() => SupportActions.OpenUrl(SupportActions.PortalUrl));

    private void Version_Click(object sender, RoutedEventArgs e) => Guarded(() =>
        _popups.Info(SupportActions.BuildVersionInfo(
            _services.MLRest, _services.AppDisplayName, _services.AppVersion), _owner));

    // Chyba akce nesmí shodit aplikaci (panel je v každém okně) — mirror legacy, které
    // výjimky handlerů polykalo; tady je ale aspoň ukážeme.
    private void Guarded(Action action)
    {
        try { action(); }
        catch (Exception ex) { _popups.Error(ex, _owner); }
    }

    // Minimalizace ownera → explicitní Hide: OS auto-hide owned oken je na HWND úrovni
    // a WPF IsVisible ho nevidí, takže Reposition by pak počítal s neplatnými souřadnicemi.
    private void OnOwnerStateChanged(object? sender, EventArgs e)
    {
        if (_owner.WindowState == WindowState.Minimized)
            Hide();
        else
            ShowPanel();
    }

    private void OnOwnerBoundsChanged(object? sender, EventArgs e) => Reposition();
    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();
    private void OnOwnSizeChanged(object sender, SizeChangedEventArgs e) => Reposition();
    private void OnOwnerClosed(object? sender, EventArgs e) => Close();

    /// <summary>
    /// Pravý okraj KLIENTSKÉ oblasti ownera. <c>PointToScreen(0,0)</c> + <c>GetClientRect</c>,
    /// protože maximalizovaný owner drží v <c>Left/Top</c> restore bounds.
    /// </summary>
    private void Reposition()
    {
        if (!IsVisible) return;
        if (!TryGetOwnerClient(out var left, out var top, out var width, out var height)) return;

        var placement = SupportPanelGeometry.Compute(
            left, top, width, height, ActualHeight, SlideOffset);

        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
    }

    /// <summary>Klientská oblast ownera v DIP, obrazovkové souřadnice.</summary>
    private bool TryGetOwnerClient(out double left, out double top, out double width, out double height)
    {
        left = top = width = height = 0;
        if (PresentationSource.FromVisual(_owner)?.CompositionTarget is not { } ct) return false;
        if (!GetClientRect(new WindowInteropHelper(_owner).Handle, out var rc)) return false;

        var toDips = ct.TransformFromDevice;
        var origin = toDips.Transform(_owner.PointToScreen(new Point(0, 0)));
        var client = toDips.Transform(new Point(rc.Right, rc.Bottom));   // GetClientRect: Left/Top = 0

        left = origin.X;
        top = origin.Y;
        width = client.X;
        height = client.Y;
        return true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _owner.LocationChanged -= OnOwnerBoundsChanged;
        _owner.SizeChanged -= OnOwnerSizeChanged;
        _owner.StateChanged -= OnOwnerStateChanged;
        _owner.Closed -= OnOwnerClosed;
        base.OnClosed(e);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT rc);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
