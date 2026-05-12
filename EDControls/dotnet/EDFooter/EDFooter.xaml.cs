using System.Windows;
using System.Windows.Controls;
using EDControls.EDViewModel;

namespace EDControls.EDFooter;

public partial class EDFooter : UserControl
{
    public static readonly DependencyProperty FooterModeProperty =
        DependencyProperty.Register(nameof(FooterMode), typeof(EFooterMode), typeof(EDFooter),
            new PropertyMetadata(EFooterMode.Close, OnFooterModeChanged));

    public static readonly DependencyProperty LeftContentProperty =
        DependencyProperty.Register(nameof(LeftContent), typeof(UIElement), typeof(EDFooter),
            new PropertyMetadata(null, OnLeftContentChanged));

    public EFooterMode FooterMode
    {
        get => (EFooterMode)GetValue(FooterModeProperty);
        set => SetValue(FooterModeProperty, value);
    }

    /// <summary>Volitelný UIElement umístěný na levé straně footeru.</summary>
    public UIElement? LeftContent
    {
        get => (UIElement?)GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    private EDControls.EDViewModel.EDViewModel? _vm;

    public EDFooter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static void OnFooterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var footer = (EDFooter)d;
        bool okCancel = (EFooterMode)e.NewValue == EFooterMode.OkCancel;
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

        window.DataContextChanged += OnWindowDataContextChanged;
        SubscribeToViewModel(window.DataContext as EDControls.EDViewModel.EDViewModel);
    }

    private void OnWindowDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.RequestClose -= OnRequestClose;
        SubscribeToViewModel(e.NewValue as EDControls.EDViewModel.EDViewModel);
    }

    private void SubscribeToViewModel(EDControls.EDViewModel.EDViewModel? vm)
    {
        _vm = vm;
        if (_vm != null) _vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool? dialogResult) => CloseWindow(dialogResult);

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            _vm.OkCommand.Execute(null);    // VM samo vyvolá RequestClose až bude připraveno
        else
            CloseWindow(true);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
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
            catch (InvalidOperationException) { }   // okno není ShowDialog() — fallback níž
        }
        window.Owner?.Activate();   // dáme focus ownerovi předtím než toto okno zavřeme
        window.Close();
    }
}
