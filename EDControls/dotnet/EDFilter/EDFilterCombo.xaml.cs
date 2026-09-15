using System.Collections;
using System.Collections.Specialized;
using System.Windows;

namespace EDControls.EDFilter;

/// <summary>
/// Filter field jako combo box. Při změně <see cref="SelectedValue"/> vystřelí
/// <see cref="EDFilterFieldBase.ValueChanged"/> a EDFilter na něj reaguje reloadem.
///
/// Prázdná hodnota (null nebo "") → filter klíč se neposílá (= žádný filter pro tento key).
/// </summary>
public partial class EDFilterCombo : EDFilterFieldBase
{
    public EDFilterCombo()
    {
        InitializeComponent();
        // Odpojené pole nemá co viset na cizí kolekci (host ji může sdílet napříč okny);
        // nedokončená obnova se po návratu do stromu rozjede znovu.
        Loaded += (_, _) => TryApplyPendingRestore();
        Unloaded += (_, _) => WatchSource(null);
    }

    /// <summary>Zdroj dat pro combo (typicky <c>List&lt;T&gt;</c> nebo <c>ObservableCollection</c> z VM).</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(EDFilterCombo),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Cesta k value property položky (např. <c>"Code"</c> nebo <c>"Id"</c>).</summary>
    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(EDFilterCombo),
            new PropertyMetadata(""));

    public string SelectedValuePath
    {
        get => (string)GetValue(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    /// <summary>Cesta k display property položky (např. <c>"Label"</c> nebo <c>"Name"</c>).</summary>
    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(EDFilterCombo),
            new PropertyMetadata(""));

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    /// <summary>Aktuální výběr. Null nebo prázdná hodnota → filter klíč se neposílá.</summary>
    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(EDFilterCombo),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedValueChanged));

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EDFilterCombo combo) return;
        // Skutečný výběr ruší čekající obnovu; null chodí i z ComboBoxu při plnění položek.
        if (e.NewValue is not null) combo._pendingRestore = null;
        combo.RaiseValueChanged();
    }

    public override string GetValue() => SelectedValue?.ToString() ?? "";

    // ── Obnova uložené hodnoty ────────────────────────────────────────────────

    /// <summary>Hodnota z preferencí, která zatím nenašla položku v <see cref="ItemsSource"/>.</summary>
    private string? _pendingRestore;
    private INotifyCollectionChanged? _watchedSource;

    /// <summary>
    /// Hledá položku, jejíž hodnota na <see cref="SelectedValuePath"/> odpovídá uloženému
    /// stringu - přiřadit hodnotu přímo nelze, uložená je vždy string, kdežto položka může
    /// mít kód jako číslo. Prázdné <c>ItemsSource</c> (VM ho plní až po Loaded) obnovu
    /// odloží, dokud položky nedorazí.
    /// </summary>
    public override void RestoreValue(string value)
    {
        _pendingRestore = string.IsNullOrEmpty(value) ? null : value;
        TryApplyPendingRestore();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        (d as EDFilterCombo)?.TryApplyPendingRestore();

    private void TryApplyPendingRestore()
    {
        if (_pendingRestore is null || ItemsSource is null) return;

        foreach (var item in ItemsSource)
        {
            var value = ValueOf(item);
            if (value is null || value.ToString() != _pendingRestore) continue;
            _pendingRestore = null;
            SelectedValue = value;
            WatchSource(null);
            return;
        }

        // Kolekce dorazila prázdná nebo se plní postupně - zkusíme to znovu, až přibudou položky.
        WatchSource(ItemsSource as INotifyCollectionChanged);
    }

    private void WatchSource(INotifyCollectionChanged? source)
    {
        if (ReferenceEquals(_watchedSource, source)) return;
        if (_watchedSource is not null) _watchedSource.CollectionChanged -= OnSourceCollectionChanged;
        _watchedSource = source;
        if (_watchedSource is not null) _watchedSource.CollectionChanged += OnSourceCollectionChanged;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        TryApplyPendingRestore();

    /// <summary>Hodnota položky podle <see cref="SelectedValuePath"/>; prázdná cesta = položka sama.</summary>
    private object? ValueOf(object? item)
    {
        if (item is null) return null;
        if (string.IsNullOrEmpty(SelectedValuePath)) return item;
        return item.GetType().GetProperty(SelectedValuePath)?.GetValue(item);
    }
}
