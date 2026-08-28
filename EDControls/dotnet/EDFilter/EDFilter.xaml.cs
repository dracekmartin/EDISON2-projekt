using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using EDControls.EDList;

namespace EDControls.EDFilter;

/// <summary>
/// Container pro deklarativní filter pole nad <see cref="EDList"/>. Filter pole jsou
/// <see cref="EDFilterFieldBase"/> children. Při změně hodnoty kteréhokoliv pole
/// (a při Loaded eventu) sebere hodnoty a zavolá <see cref="EDList.LoadList(System.Collections.Generic.IReadOnlyDictionary{string, string})"/>.
///
/// Wire formát: filter hodnoty se posílají pod <c>^||ML("IN","filter",Key)</c> sub-subscript.
/// Detail viz <c>EDControls/DOCS/03-EDFilter.md</c> a <c>01-wire-formats.md §Filter konvence</c>.
/// </summary>
[ContentProperty(nameof(Fields))]
public partial class EDFilter : UserControl
{
    public EDFilter()
    {
        InitializeComponent();
        Fields = new ObservableCollection<EDFilterFieldBase>();
        Fields.CollectionChanged += OnFieldsCollectionChanged;
        FieldsHost.ItemsSource = Fields;
        Loaded += OnLoaded;
    }

    /// <summary>EDList, na kterém se má filter aplikovat. Volá se <c>Target.LoadList(filter)</c>.</summary>
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(EDList.EDList), typeof(EDFilter),
            new PropertyMetadata(null));

    public EDList.EDList? Target
    {
        get => (EDList.EDList?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Pokud <c>true</c> (default), změna hodnoty pole okamžitě vyvolá <c>Target.LoadList(filter)</c>.
    /// Pokud <c>false</c>, reload se musí spustit ručně přes <see cref="Refresh"/>.
    /// </summary>
    public static readonly DependencyProperty AutoTriggerProperty =
        DependencyProperty.Register(nameof(AutoTrigger), typeof(bool), typeof(EDFilter),
            new PropertyMetadata(true));

    public bool AutoTrigger
    {
        get => (bool)GetValue(AutoTriggerProperty);
        set => SetValue(AutoTriggerProperty, value);
    }

    /// <summary>Filter pole - typicky vyplněná v XAML přes &lt;el:EDFilterCombo/&gt; atd.</summary>
    public ObservableCollection<EDFilterFieldBase> Fields { get; }

    /// <summary>Vyvoláno před každým <c>Target.LoadList(filter)</c>. Host může observovat pro vlastní logiku.</summary>
    public event EventHandler<IReadOnlyDictionary<string, string>>? FilterChanged;

    private bool _initialLoadDone;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadDone) return;
        _initialLoadDone = true;
        // Subscribe je zajištěn přes OnFieldsCollectionChanged (XAML [ContentProperty]
        // přidává child přes Fields.Add() který fires CollectionChanged).
        AutoRefresh();   // initial load - bez filteru nebo s default hodnotami fields
    }

    private void OnFieldsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (EDFilterFieldBase f in e.NewItems)
                f.ValueChanged += OnFieldValueChanged;
        if (e.OldItems is not null)
            foreach (EDFilterFieldBase f in e.OldItems)
                f.ValueChanged -= OnFieldValueChanged;
    }

    private void OnFieldValueChanged(object? sender, EventArgs e)
    {
        if (AutoTrigger) AutoRefresh();
    }

    /// <summary>
    /// Automatický reload (Loaded / změna hodnoty pole) - jde přes <see cref="RefreshAsync"/>, aby
    /// pomalá linka nezmrazila celou aplikaci. Výjimku musí spolknout tady: je to
    /// fire-and-forget z event handleru, takže by jinak spadla mimo jakýkoli catch.
    /// </summary>
    private async void AutoRefresh()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            EDViewModel.EDServices.Current.PopupDisplayer.Error(ex);
        }
    }

    /// <summary>Sebere hodnoty ze všech FilterX children a vrátí dict neprázdných hodnot.</summary>
    public Dictionary<string, string> BuildFilterDict()
    {
        var dict = new Dictionary<string, string>();
        foreach (var field in Fields)
        {
            if (string.IsNullOrEmpty(field.Key)) continue;
            var value = field.GetValue();
            if (string.IsNullOrEmpty(value)) continue;
            dict[field.Key] = value;
        }
        return dict;
    }

    /// <summary>Manuální trigger reload - sebere aktuální hodnoty a zavolá <c>Target.LoadList(filter)</c>.</summary>
    public void Refresh()
    {
        if (Target is null) return;
        var filter = BuildFilterDict();
        FilterChanged?.Invoke(this, filter);
        Target.LoadList(filter.Count == 0 ? null : filter);
    }

    /// <summary>
    /// Async varianta <see cref="Refresh"/> - tou jede i automatický reload. Synchronní
    /// varianta drží UI vlákno po celý round-trip, takže na pomalé lince zmrazí **celou aplikaci**,
    /// ne jen okno s filtrem.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (Target is null) return;
        var filter = BuildFilterDict();
        FilterChanged?.Invoke(this, filter);
        await Target.LoadListAsync(filter.Count == 0 ? null : filter);
    }
}

