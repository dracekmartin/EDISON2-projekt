using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EDControls.EDViewModel;

namespace EDControls.EDComponentToolbar;

/// <summary>
/// Sdílený toolbar pro EDInputForm / EDList / tabulkové komponenty: brandový pruh s Title
/// vlevo a search boxem + ikonou nápovědy vpravo. Ctrl+F focusne search box komponenty, ve které
/// je fokus (jinak prvního viditelného toolbaru v okně). API viz DP níže.
/// </summary>
public partial class EDComponentToolbar : UserControl
{
    private Window? _attachedWindow;

    public EDComponentToolbar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Jedno okno = jeden Ctrl+F binding (WPF obslouží první shodný, další by se jen hromadily
    // v InputBindings), ale cíl se vybírá až při stisku: hledání komponenty, ve které je fokus,
    // jinak první viditelné. Pevný „první načtený" toolbar posílal Ctrl+F třeba do formuláře
    // na skryté záložce. V hlavním okně shellu je toolbarů desítky (každá tabulka miniaplikace).
    private sealed class CtrlFOkna
    {
        public KeyBinding Binding = null!;
        public readonly List<EDComponentToolbar> Toolbary = [];
    }

    private static readonly DependencyProperty CtrlFOknaProperty =
        DependencyProperty.RegisterAttached("CtrlFOkna", typeof(CtrlFOkna), typeof(EDComponentToolbar));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Odregistrace v OnUnloaded je nutná - jinak by se při re-loadu bindingy hromadily.
        var okno = Window.GetWindow(this);
        if (okno is not null && _attachedWindow is null)
        {
            if (okno.GetValue(CtrlFOknaProperty) is not CtrlFOkna stav)
            {
                stav = new CtrlFOkna();
                var cil = stav;
                stav.Binding = new KeyBinding(new RelayCommand(() => FocusSearchIn(cil)),
                    new KeyGesture(Key.F, ModifierKeys.Control));
                okno.SetValue(CtrlFOknaProperty, stav);
                okno.InputBindings.Add(stav.Binding);
            }
            stav.Toolbary.Add(this);
            _attachedWindow = okno;
        }
        AktualizujPlaceholder();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_attachedWindow?.GetValue(CtrlFOknaProperty) is CtrlFOkna stav)
        {
            stav.Toolbary.Remove(this);
            if (stav.Toolbary.Count == 0)
            {
                _attachedWindow.InputBindings.Remove(stav.Binding);
                _attachedWindow.ClearValue(CtrlFOknaProperty);
            }
        }
        _attachedWindow = null;
        AktualizujPlaceholder();
    }

    private static void FocusSearchIn(CtrlFOkna stav) =>
        CilCtrlF(stav.Toolbary, t => t.KomponentaMaFokus(), t => t.IsVisible)?.FocusSearch();

    /// <summary>Kam Ctrl+F: hledání komponenty, ve které je fokus, jinak první viditelné.</summary>
    internal static EDComponentToolbar? CilCtrlF(IReadOnlyList<EDComponentToolbar> toolbary,
        Func<EDComponentToolbar, bool> maFokus, Func<EDComponentToolbar, bool> jeVidet)
    {
        var kandidati = toolbary.Where(t => t.ShowSearch && jeVidet(t)).ToList();
        return kandidati.FirstOrDefault(maFokus) ?? kandidati.FirstOrDefault();
    }

    // Komponenta = nejbližší nadřazený UserControl (EDInputForm, EDList); toolbar je její hlavička.
    private bool KomponentaMaFokus()
    {
        DependencyObject? d = VisualTreeHelper.GetParent(this);
        while (d is not null and not UserControl) d = VisualTreeHelper.GetParent(d);
        return (d as UIElement ?? this).IsKeyboardFocusWithin;
    }

    // Nápověda „(Ctrl+F)" u každého toolbaru v okně - zkratka vede do hledání komponenty,
    // ve které uživatel je, takže z kteréhokoli z nich platí.
    private void AktualizujPlaceholder() =>
        SetValue(EffectiveSearchPlaceholderPropertyKey,
            _attachedWindow is null ? SearchPlaceholder : $"{SearchPlaceholder} (Ctrl+F)");

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

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(IEnumerable<EDAction>), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    /// <summary>Akce nad položkami komponenty jako ikony v pruhu (popisek a zkratka v bublině);
    /// typicky <c>EDViewModel.Actions</c> okna. Null = žádné.</summary>
    public IEnumerable<EDAction>? Actions
    {
        get => (IEnumerable<EDAction>?)GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
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

    private const string SearchPlaceholder = "Hledat…";

    private static readonly DependencyPropertyKey EffectiveSearchPlaceholderPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(EffectiveSearchPlaceholder), typeof(string),
            typeof(EDComponentToolbar), new PropertyMetadata(SearchPlaceholder));

    public static readonly DependencyProperty EffectiveSearchPlaceholderProperty =
        EffectiveSearchPlaceholderPropertyKey.DependencyProperty;

    /// <summary>Zobrazený placeholder hledání, u toolbaru se zkratkou Ctrl+F doplněný o „(Ctrl+F)".</summary>
    public string EffectiveSearchPlaceholder => (string)GetValue(EffectiveSearchPlaceholderProperty);

    public static readonly DependencyProperty ShowHelpProperty =
        DependencyProperty.Register(nameof(ShowHelp), typeof(bool), typeof(EDComponentToolbar),
            new PropertyMetadata(true));

    public bool ShowHelp
    {
        get => (bool)GetValue(ShowHelpProperty);
        set => SetValue(ShowHelpProperty, value);
    }

    public static readonly DependencyProperty HelpCommandProperty =
        DependencyProperty.Register(nameof(HelpCommand), typeof(ICommand), typeof(EDComponentToolbar),
            new PropertyMetadata(null));

    public ICommand? HelpCommand
    {
        get => (ICommand?)GetValue(HelpCommandProperty);
        set => SetValue(HelpCommandProperty, value);
    }
}

