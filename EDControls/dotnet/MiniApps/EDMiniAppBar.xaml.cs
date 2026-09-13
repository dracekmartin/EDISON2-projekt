using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace EDControls.MiniApps;

/// <summary>
/// Pruh nad obsahem miniaplikace: filtrační kontrolky, akční tlačítko a status vpravo.
/// Sám nic nenačítá - jen hostí kontrolky a vystřelí <see cref="ActionCommand"/>.
/// Záměrně není rozšířením <c>EDComponentToolbar</c>: ten registruje window-level Ctrl+F
/// (N panelů = N bindingů v shellu) a kreslí brandový pruh, na kterém vsazené ComboBoxy
/// vypadají rozbitě.
/// </summary>
[ContentProperty(nameof(Filters))]
public partial class EDMiniAppBar : UserControl
{
    public EDMiniAppBar()
    {
        InitializeComponent();
        Filters = new ObservableCollection<object>();
        Filters.CollectionChanged += OnFiltersChanged;
    }

    /// <summary>Filtrační kontrolky - píšou se jako děti v XAML (vzor <c>EDFilter</c>).</summary>
    public ObservableCollection<object> Filters { get; }

    /// <summary>
    /// Rozestup a zarovnání na střed (ComboBox, DatePicker a CheckBox mají různou výšku).
    /// Přes <c>ItemContainerStyle</c> to nejde - kontrolka je sama sobě kontejnerem, takže
    /// styl cílený na <c>ContentPresenter</c> WPF odmítne. Hodnotu nastavenou konzumentem
    /// nepřepisujeme.
    /// </summary>
    private static void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (var item in e.NewItems.OfType<FrameworkElement>())
        {
            if (item.ReadLocalValue(MarginProperty) == DependencyProperty.UnsetValue)
                item.Margin = new Thickness(0, 0, 6, 0);
            if (item.ReadLocalValue(VerticalAlignmentProperty) == DependencyProperty.UnsetValue)
                item.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EDMiniAppBar),
            new PropertyMetadata("Načíst"));

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EDMiniAppBar),
            new PropertyMetadata(null));

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public static readonly DependencyProperty ShowActionProperty =
        DependencyProperty.Register(nameof(ShowAction), typeof(bool), typeof(EDMiniAppBar),
            new PropertyMetadata(true));

    public bool ShowAction
    {
        get => (bool)GetValue(ShowActionProperty);
        set => SetValue(ShowActionProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(EDMiniAppBar),
            new PropertyMetadata(null));

    /// <summary>Text vpravo (např. „Aktualizováno: …"). Prázdný = skrytý.</summary>
    public string? StatusText
    {
        get => (string?)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(EDMiniAppBar),
            new PropertyMetadata(false));

    /// <summary>Po dobu načítání nahradí status hláškou; tlačítko si hlídá VM přes CanExecute.</summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }
}
