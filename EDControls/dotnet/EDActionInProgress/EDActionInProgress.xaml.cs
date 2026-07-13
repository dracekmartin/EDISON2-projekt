using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EDVm = EDControls.EDViewModel.EDViewModel;

namespace EDControls.EDActionInProgress;

public partial class EDActionInProgress : UserControl
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(EDActionInProgress),
            new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private IDisposable? _dimScope;

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EDActionInProgress)d;
        control.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        control.UpdateDim((bool)e.NewValue);
    }

    // Závoj přes WindowDimmer místo vlastního pozadí — refcount nesčítá tint
    // s dialog dimem (WireDialogBehavior) během náběhu legacy dialogu.
    private void UpdateDim(bool active)
    {
        if (active)
        {
            _dimScope ??= WindowDimmer.Dim(Window.GetWindow(this));
        }
        else
        {
            _dimScope?.Dispose();
            _dimScope = null;
        }
    }

    public EDActionInProgress()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;     // design time

        window.DataContextChanged += OnWindowDataContextChanged;
        BindToViewModel(window.DataContext as EDVm);
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
