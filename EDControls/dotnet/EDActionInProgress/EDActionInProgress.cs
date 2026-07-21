using System;
using System.Windows;
using System.Windows.Data;
using EDVm = EDControls.EDViewModel.EDViewModel;

namespace EDControls.EDActionInProgress;

/// <summary>
/// Neviditelný controller overlay „Akce probíhá" — vlož kamkoli do okna (konvence: poslední
/// child root Gridu). Na Loaded se sám naváže na <see cref="EDVm.IsActionInProgress"/>;
/// při aktivaci ztmaví okno (<see cref="WindowDimmer"/>) a zobrazí kartu s kapybarou
/// v owned okně <see cref="ActionInProgressWindow"/> — jediný způsob, jak ji vykreslit
/// nad HWND widgety (WebView2), které WPF obsah kvůli airspace nepřekreslí.
/// </summary>
public sealed class EDActionInProgress : FrameworkElement
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(EDActionInProgress),
            new PropertyMetadata(false, (d, e) => ((EDActionInProgress)d).Apply((bool)e.NewValue)));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private IDisposable? _dimScope;
    private ActionInProgressWindow? _overlay;
    private Window? _hostWindow;
    private Window? _pendingOwner;   // owner čekající na IsVisibleChanged (odložené show)

    public EDActionInProgress()
    {
        Visibility = Visibility.Collapsed;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void Apply(bool active)
    {
        if (active)
        {
            // Před Loaded / design time — OnLoaded zavolá Apply(IsActive) znovu.
            if (Window.GetWindow(this) is not { } owner) return;
            _dimScope ??= WindowDimmer.Dim(owner);
            // Owner ještě nezobrazený (akce před ShowDialog) — setter Owner by hodil výjimku
            // a karta by se vykreslila nad plochu; odlož na IsVisibleChanged.
            if (!owner.IsVisible)
            {
                DeferUntilVisible(owner);
                return;
            }
            if (_overlay is null)
            {
                _overlay = new ActionInProgressWindow(owner);
                // Owned okno zavře WPF spolu s ownerem — příští aktivace si vytvoří nové.
                _overlay.Closed += (_, _) => _overlay = null;
            }
            _overlay.ShowOverlay();
        }
        else
        {
            CancelDeferred();
            _overlay?.HideOverlay();
            _dimScope?.Dispose();
            _dimScope = null;
        }
    }

    private void DeferUntilVisible(Window owner)
    {
        if (_pendingOwner is not null) return;
        _pendingOwner = owner;
        owner.IsVisibleChanged += OnOwnerVisibleChanged;
    }

    private void OnOwnerVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) return;
        CancelDeferred();
        if (IsActive) Apply(true);
    }

    private void CancelDeferred()
    {
        if (_pendingOwner is null) return;
        _pendingOwner.IsVisibleChanged -= OnOwnerVisibleChanged;
        _pendingOwner = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;     // design time

        _hostWindow = window;
        window.DataContextChanged += OnWindowDataContextChanged;
        BindToViewModel(window.DataContext as EDVm);
        // VM mohl akci spustit dřív, než měl element okno (Apply tehdy no-opnul).
        if (IsActive) Apply(true);
    }

    // Overlay nesmí přežít odpojení ze stromu; při re-loadu ho Apply(IsActive) obnoví.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hostWindow is not null)
        {
            _hostWindow.DataContextChanged -= OnWindowDataContextChanged;
            _hostWindow = null;
        }
        Apply(false);
    }

    private void OnWindowDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        BindToViewModel(e.NewValue as EDVm);
    }

    private void BindToViewModel(EDVm? vm)
    {
        if (vm != null)
            SetBinding(IsActiveProperty, new Binding(nameof(EDVm.IsActionInProgress)));
        else
            ClearValue(IsActiveProperty);
    }
}
