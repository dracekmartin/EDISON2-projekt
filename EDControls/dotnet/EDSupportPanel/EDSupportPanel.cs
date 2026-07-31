using System.Windows;
using EDControls.EDViewModel;

namespace EDControls.EDSupportPanel;

/// <summary>
/// Připojuje panel Podpora k oknům aplikace. Registruje se jednou z
/// <see cref="EDAppearance.Initialize"/> jako class handler na <c>Window.Loaded</c>, takže panel
/// dostane **každé** okno včetně odděděných tříd a bez ohledu na to, jak se otevírá (obdoba legacy
/// <c>BaseForm.AttachSideMenu</c>). Vypnout pro konkrétní okno: <see cref="SuppressProperty"/>,
/// u okna s ViewModelem taky <c>EDViewModel.ShowSupportPanel = false</c>.
/// </summary>
public static class EDSupportPanel
{
    /// <summary>
    /// Nastav <c>True</c> na okno, které panel Podpora dostat nemá (vlastní popupy EDControls,
    /// splash, odpočty). Okno s ViewModelem má i <c>EDViewModel.ShowSupportPanel</c>.
    /// </summary>
    public static readonly DependencyProperty SuppressProperty =
        DependencyProperty.RegisterAttached("Suppress", typeof(bool), typeof(EDSupportPanel));

    public static void SetSuppress(Window window, bool value) => window.SetValue(SuppressProperty, value);
    public static bool GetSuppress(Window window) => (bool)(window.GetValue(SuppressProperty) ?? false);

    // Panel se připojuje jen jednou per okno (re-Loaded při re-parentu okno nezdvojí).
    private static readonly DependencyProperty AttachedProperty =
        DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(EDSupportPanel));

    private static bool _initialized;

    /// <summary>Je k oknu panel už připojený? (Pro testy napojení a proti dvojímu attachi.)</summary>
    internal static bool IsAttached(Window owner) => (bool)(owner.GetValue(AttachedProperty) ?? false);

    /// <summary>
    /// Jednorázová registrace class handleru. Class handler na <c>typeof(Window)</c> chytá
    /// i odděděné třídy oken — implicitní styl `Window` v WPF ne, proto tento vzor
    /// (stejně ho používá <c>ThemeService.Initialize</c>).
    /// </summary>
    internal static void AttachToAllWindows()
    {
        if (_initialized) return;
        _initialized = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ShouldAttach(window)) return;
        if (EDServices.CurrentOrNull is not { } services) return;   // před composition rootem

        Attach(window, services, services.PopupDisplayer);
    }

    /// <summary>Má tohle okno dostat panel? (Rozhodnutí odděleno od eventu, aby šlo testovat.)</summary>
    internal static bool ShouldAttach(Window window) =>
        window is not IOverlayWindow                  // vlastní overlay okna EDControls
        && !GetSuppress(window)
        && window.DataContext is not EDViewModel.EDViewModel { ShowSupportPanel: false }
        && !IsAttached(window);

    internal static void Attach(Window owner, EDServices services, IPopupDisplayer popups)
    {
        if (IsAttached(owner)) return;
        owner.SetValue(AttachedProperty, true);

        if (owner.IsVisible)
        {
            Create(owner, services, popups);
            return;
        }

        // Pojistka pro přímé volání před Show() — setter Owner na nezobrazené okno hází.
        // Z class handleru se nepoužije, tam je okno na Loaded už zobrazené.
        void OnVisibleChanged(object s, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;
            owner.IsVisibleChanged -= OnVisibleChanged;
            Create(owner, services, popups);
        }

        owner.IsVisibleChanged += OnVisibleChanged;
    }

    // Best-effort: panel nikdy nesmí shodit zobrazení okna, ke kterému se připojuje.
    private static void Create(Window owner, EDServices services, IPopupDisplayer popups)
    {
        try { new SupportPanelWindow(owner, services, popups).TryShowPanel(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("EDSupportPanel: " + ex.Message); }
    }
}
