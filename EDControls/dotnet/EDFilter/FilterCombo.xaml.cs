using System.Collections;
using System.Windows;

namespace EDControls.EDFilter;

/// <summary>
/// Filter field jako combo box. Při změně <see cref="SelectedValue"/> vystřelí
/// <see cref="FilterFieldBase.ValueChanged"/> a EDFilter na něj reaguje reloadem.
///
/// Prázdná hodnota (null nebo "") → filter klíč se neposílá (= žádný filter pro tento key).
/// </summary>
public partial class FilterCombo : FilterFieldBase
{
    public FilterCombo()
    {
        InitializeComponent();
    }

    /// <summary>Zdroj dat pro combo (typicky <c>List&lt;T&gt;</c> nebo <c>ObservableCollection</c> z VM).</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(FilterCombo),
            new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Cesta k value property položky (např. <c>"Code"</c> nebo <c>"Id"</c>).</summary>
    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(FilterCombo),
            new PropertyMetadata(""));

    public string SelectedValuePath
    {
        get => (string)GetValue(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    /// <summary>Cesta k display property položky (např. <c>"Label"</c> nebo <c>"Name"</c>).</summary>
    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(FilterCombo),
            new PropertyMetadata(""));

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    /// <summary>Aktuální výběr. Null nebo prázdná hodnota → filter klíč se neposílá.</summary>
    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(FilterCombo),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedValueChanged));

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        (d as FilterCombo)?.RaiseValueChanged();

    public override string GetValue() => SelectedValue?.ToString() ?? "";
}
