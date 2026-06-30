using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDControls.EDViewModel;

namespace EDControls.EDComponentToolbar;

/// <summary>
/// Sdílený toolbar bar pro EDInputForm / EDList / budoucí tabulkové komponenty.
/// Tmavě modré pozadí (EDTheme.Accent.BlueDark), search box vlevo + standardní ikony vpravo.
///
/// <para><b>API přehled:</b></para>
/// <list type="bullet">
/// <item><c>Title</c> — volitelný název vlevo (např. pro EDList).</item>
/// <item><c>SearchText</c> — two-way bound text. Host váže na svoji filter logic.</item>
/// <item><c>CurrentMatchIndex</c> / <c>TotalMatches</c> — host updates jak iteruje matche.</item>
/// <item><c>FindNextCommand</c> / <c>FindPreviousCommand</c> / <c>ClearSearchCommand</c> — host implementace skoku na další/předchozí match.</item>
/// <item><c>ShowExport</c> / <c>ShowPrint</c> / <c>ShowHelp</c> / <c>ShowSettings</c> — opt-in pravé ikony s ICommand.</item>
/// </list>
///
/// <para><b>Ctrl+F</b> uvnitř komponenty focusne search box (universal browser/editor convention).</para>
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ctrl+F focusne search box (window-level binding, pokud parent Window existuje).
        // Unsubscribe nutný v OnUnloaded — jinak při re-load akumulují binding kopie.
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is null || _ctrlFBinding is not null) return;

        _ctrlFBinding = new KeyBinding(
            new RelayCommand(FocusSearch),
            new KeyGesture(Key.F, ModifierKeys.Control));
        _attachedWindow = parentWindow;
        parentWindow.InputBindings.Add(_ctrlFBinding);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_ctrlFBinding is not null && _attachedWindow is not null)
        {
            _attachedWindow.InputBindings.Remove(_ctrlFBinding);
            _ctrlFBinding = null;
            _attachedWindow = null;
        }
    }

    private void FocusSearch()
    {
        if (!ShowSearch) return;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    // ─── Title ──────────────────────────────────────────────────────────────

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    /// <summary>Volitelný název zobrazený vlevo (např. „Šablony" pro EDList).</summary>
    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // ─── Search ─────────────────────────────────────────────────────────────

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
            new PropertyMetadata("Hledat:"));

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    public static readonly DependencyProperty CurrentMatchIndexProperty =
        DependencyProperty.Register(nameof(CurrentMatchIndex), typeof(int), typeof(EDComponentToolbar),
            new PropertyMetadata(0));

    /// <summary>1-based index aktuálního matche (0 = žádný). Host updates jak iteruje.</summary>
    public int CurrentMatchIndex
    {
        get => (int)GetValue(CurrentMatchIndexProperty);
        set => SetValue(CurrentMatchIndexProperty, value);
    }

    public static readonly DependencyProperty TotalMatchesProperty =
        DependencyProperty.Register(nameof(TotalMatches), typeof(int), typeof(EDComponentToolbar),
            new PropertyMetadata(0));

    /// <summary>Celkový počet matchů. Host updates. 0 = counter neviditelný.</summary>
    public int TotalMatches
    {
        get => (int)GetValue(TotalMatchesProperty);
        set => SetValue(TotalMatchesProperty, value);
    }

    public static readonly DependencyProperty FindNextCommandProperty =
        DependencyProperty.Register(nameof(FindNextCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? FindNextCommand
    {
        get => (ICommand?)GetValue(FindNextCommandProperty);
        set => SetValue(FindNextCommandProperty, value);
    }

    public static readonly DependencyProperty FindPreviousCommandProperty =
        DependencyProperty.Register(nameof(FindPreviousCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? FindPreviousCommand
    {
        get => (ICommand?)GetValue(FindPreviousCommandProperty);
        set => SetValue(FindPreviousCommandProperty, value);
    }

    public static readonly DependencyProperty ClearSearchCommandProperty =
        DependencyProperty.Register(nameof(ClearSearchCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    /// <summary>Volitelný custom clear command. Pokud null, default = clear SearchText.</summary>
    public ICommand? ClearSearchCommand
    {
        get => (ICommand?)GetValue(ClearSearchCommandProperty);
        set => SetValue(ClearSearchCommandProperty, value);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        if (ClearSearchCommand?.CanExecute(null) == true)
            ClearSearchCommand.Execute(null);
        else
            SearchText = "";
    }

    // ─── Right-side actions (opt-in) ────────────────────────────────────────

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

