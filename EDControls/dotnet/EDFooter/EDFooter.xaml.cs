using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EDControls.EDViewModel;

namespace EDControls.EDFooter;

public partial class EDFooter : UserControl
{
    public static readonly DependencyProperty FooterModeProperty =
        DependencyProperty.Register(nameof(FooterMode), typeof(FooterMode), typeof(EDFooter),
            new PropertyMetadata(FooterMode.Close, OnFooterModeChanged));

    public static readonly DependencyProperty LeftContentProperty =
        DependencyProperty.Register(nameof(LeftContent), typeof(UIElement), typeof(EDFooter),
            new PropertyMetadata(null, OnLeftContentChanged));

    public FooterMode FooterMode
    {
        get => (FooterMode)GetValue(FooterModeProperty);
        set => SetValue(FooterModeProperty, value);
    }

    /// <summary>Volitelný UIElement umístěný na levé straně footeru.</summary>
    public UIElement? LeftContent
    {
        get => (UIElement?)GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    private EDControls.EDViewModel.EDViewModel? _vm;
    private Window? _window;

    public EDFooter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnFooterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var footer = (EDFooter)d;
        bool okCancel = (FooterMode)e.NewValue == FooterMode.OkCancel;
        footer.OkBtn.Visibility     = okCancel ? Visibility.Visible   : Visibility.Collapsed;
        footer.StornoBtn.Visibility = okCancel ? Visibility.Visible   : Visibility.Collapsed;
        footer.CloseBtn.Visibility  = okCancel ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnLeftContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((EDFooter)d).LeftContentPresenter.Content = e.NewValue;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;     // design time

        // Loaded fajruje při každém návratu do vizuálního stromu (tab, re-template) -
        // bez odhlášení by se DataContextChanged i RequestClose handlery akumulovaly.
        DetachFromWindow();
        _window = window;
        window.DataContextChanged += OnWindowDataContextChanged;
        // Esc přes KeyDown okna, ne IsCancel: Button po Click zavře dialog sám i přes odmítnuté ConfirmClose.
        window.KeyDown += OnWindowKeyDown;
        SubscribeToViewModel(window.DataContext as EDControls.EDViewModel.EDViewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachFromWindow();

    private void DetachFromWindow()
    {
        if (_window != null)
        {
            _window.DataContextChanged -= OnWindowDataContextChanged;
            _window.KeyDown -= OnWindowKeyDown;
            _window = null;
        }
        SubscribeToViewModel(null);
    }

    private void OnWindowDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        SubscribeToViewModel(e.NewValue as EDControls.EDViewModel.EDViewModel);

    private void SubscribeToViewModel(EDControls.EDViewModel.EDViewModel? vm)
    {
        if (_vm != null) _vm.RequestClose -= OnRequestClose;
        _vm = vm;
        if (_vm != null) _vm.RequestClose += OnRequestClose;
        ActionsBtn.Visibility = _vm?.Actions is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ActionsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm?.Actions is not { Count: > 0 } actions) return;
        var menu = EDControls.EDViewModel.EDAction.BuildContextMenu(actions);
        menu.PlacementTarget = ActionsBtn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void OnRequestClose(bool? dialogResult) => CloseWindow(dialogResult);

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            _vm.OkCommand.Execute(null);    // VM samo vyvolá RequestClose až bude připraveno
        else
            CloseWindow(true);
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Opakování podrženého Esc by po zavřeném dialogu zavřelo i okno pod ním.
        if (e.Key != Key.Escape || e.IsRepeat) return;
        // Popup (kalendář DatePickeru) se na Esc zavře sám, jen klávesu neoznačí jako zpracovanou.
        if (e.OriginalSource is Visual source && !((Visual)sender).IsAncestorOf(source)) return;
        e.Handled = true;
        Cancel();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Cancel();

    private void Cancel()
    {
        if (_vm is { ConfirmOnCancel: true })
        {
            if (!_vm.PopupDisplayer.ConfirmClose())
                return;
        }
        CloseWindow(false);
    }

    private void CloseWindow(bool? dialogResult)
    {
        var window = Window.GetWindow(this);
        if (window == null) return;

        if (dialogResult.HasValue)
        {
            try
            {
                window.DialogResult = dialogResult;
                return;     // setting DialogResult zároveň okno zavře
            }
            catch (InvalidOperationException) { }   // okno není ShowDialog() - fallback níž
        }
        window.Owner?.Activate();   // dáme focus ownerovi předtím než toto okno zavřeme
        window.Close();
    }
}
