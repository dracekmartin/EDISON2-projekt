using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDControls.EDViewModel;

namespace EDControls.EDComponentToolbar;

/// <summary>
/// Sdílený toolbar pro EDInputForm / EDList / tabulkové komponenty: brandový pruh s Title
/// vlevo a search boxem + opt-in ikonami vpravo. Ctrl+F focusne search box. API viz DP níže.
/// </summary>
public partial class EDComponentToolbar : UserControl
{
    private KeyBinding? _ctrlFBinding;
    private Window? _attachedWindow;

    public EDComponentToolbar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Jedno okno = jeden Ctrl+F binding. V hlavním okně shellu je toolbarů desítky (každá
    // tabulka miniaplikace je EDList) a WPF stejně obslouží první shodný binding - ostatní by se
    // jen hromadily v InputBindings.
    private static readonly DependencyProperty CtrlFOwnerProperty =
        DependencyProperty.RegisterAttached("CtrlFOwner", typeof(EDComponentToolbar), typeof(EDComponentToolbar));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PrevezmiCtrlF();
        AktualizujPlaceholder();
    }

    private void PrevezmiCtrlF()
    {
        // Ctrl+F focusne search box (window-level binding, pokud parent Window existuje).
        // Unsubscribe nutný v OnUnloaded - jinak při re-load akumulují binding kopie.
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is null || _ctrlFBinding is not null) return;
        if (parentWindow.GetValue(CtrlFOwnerProperty) is not null) return;

        _ctrlFBinding = new KeyBinding(
            new RelayCommand(FocusSearch),
            new KeyGesture(Key.F, ModifierKeys.Control));
        _attachedWindow = parentWindow;
        parentWindow.SetValue(CtrlFOwnerProperty, this);
        parentWindow.InputBindings.Add(_ctrlFBinding);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_ctrlFBinding is not null && _attachedWindow is not null)
        {
            _attachedWindow.InputBindings.Remove(_ctrlFBinding);
            _attachedWindow.ClearValue(CtrlFOwnerProperty);   // uvolni slot dalšímu toolbaru
            _ctrlFBinding = null;
            _attachedWindow = null;
        }
        AktualizujPlaceholder();
    }

    // Nápověda „(Ctrl+F)" jen u toolbaru, který Ctrl+F v okně opravdu obsluhuje - ostatním
    // (v shellu jsou jich desítky) by lhala.
    private void AktualizujPlaceholder() =>
        SetValue(EffectiveSearchPlaceholderPropertyKey,
            _ctrlFBinding is null ? SearchPlaceholder : $"{SearchPlaceholder} (Ctrl+F)");

    private void FocusSearch()
    {
        if (!ShowSearch) return;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    /// <summary>Volitelný název zobrazený vlevo (např. „Šablony" pro EDList).</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty ShowSearchProperty =
        DependencyProperty.Register(nameof(ShowSearch), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(true));

    public bool ShowSearch
    {
        get => (bool)GetValue(ShowSearchProperty);
        set => SetValue(ShowSearchProperty, value);
    }

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EDComponentToolbar),
            new FrameworkPropertyMetadata("",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly DependencyProperty SearchPlaceholderProperty =
        DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(EDComponentToolbar),
            new PropertyMetadata("Hledat…", (d, _) => ((EDComponentToolbar)d).AktualizujPlaceholder()));

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    private static readonly DependencyPropertyKey EffectiveSearchPlaceholderPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(EffectiveSearchPlaceholder), typeof(string),
            typeof(EDComponentToolbar), new PropertyMetadata("Hledat…"));

    public static readonly DependencyProperty EffectiveSearchPlaceholderProperty =
        EffectiveSearchPlaceholderPropertyKey.DependencyProperty;

    /// <summary>Zobrazený placeholder hledání: <see cref="SearchPlaceholder"/>, u toolbaru se zkratkou
    /// Ctrl+F doplněný o „(Ctrl+F)".</summary>
    public string EffectiveSearchPlaceholder => (string)GetValue(EffectiveSearchPlaceholderProperty);

    public static readonly DependencyProperty ShowExportProperty =
        DependencyProperty.Register(nameof(ShowExport), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(false));

    public bool ShowExport
    {
        get => (bool)GetValue(ShowExportProperty);
        set => SetValue(ShowExportProperty, value);
    }

    public static readonly DependencyProperty ShowPrintProperty =
        DependencyProperty.Register(nameof(ShowPrint), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(false));

    public bool ShowPrint
    {
        get => (bool)GetValue(ShowPrintProperty);
        set => SetValue(ShowPrintProperty, value);
    }

    public static readonly DependencyProperty ShowHelpProperty =
        DependencyProperty.Register(nameof(ShowHelp), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(true));

    public bool ShowHelp
    {
        get => (bool)GetValue(ShowHelpProperty);
        set => SetValue(ShowHelpProperty, value);
    }

    public static readonly DependencyProperty ShowSettingsProperty =
        DependencyProperty.Register(nameof(ShowSettings), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(true));

    public bool ShowSettings
    {
        get => (bool)GetValue(ShowSettingsProperty);
        set => SetValue(ShowSettingsProperty, value);
    }

    public static readonly DependencyProperty ExportCommandProperty =
        DependencyProperty.Register(nameof(ExportCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    /// <summary>Host implementuje. Volitelně bere format string parametr (PDF/XLSX/CSV).</summary>
    public ICommand? ExportCommand
    {
        get => (ICommand?)GetValue(ExportCommandProperty);
        set => SetValue(ExportCommandProperty, value);
    }

    public static readonly DependencyProperty PrintCommandProperty =
        DependencyProperty.Register(nameof(PrintCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? PrintCommand
    {
        get => (ICommand?)GetValue(PrintCommandProperty);
        set => SetValue(PrintCommandProperty, value);
    }

    public static readonly DependencyProperty HelpCommandProperty =
        DependencyProperty.Register(nameof(HelpCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? HelpCommand
    {
        get => (ICommand?)GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }

    public static readonly DependencyProperty SettingsCommandProperty =
        DependencyProperty.Register(nameof(SettingsCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? SettingsCommand
    {
        get => (ICommand?)GetValue(SettingsCommandProperty);
        set => SetValue(SettingsCommandProperty, value);
    }
}

