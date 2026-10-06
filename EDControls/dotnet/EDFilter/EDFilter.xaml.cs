using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using EDControls.UserPrefs;

namespace EDControls.EDFilter;

/// <summary>
/// Container pro deklarativní filter pole nad zobrazovací komponentou. Filter pole jsou
/// <see cref="EDFilterFieldBase"/> children. Při změně hodnoty kteréhokoliv pole
/// (a při Loaded eventu) sebere hodnoty a předá je cíli - <see cref="Target"/>
/// (<see cref="IEDFilterTarget"/>, implementuje ho např. EDList).
///
/// Wire formát komponent nad Caché: filter hodnoty jdou pod <c>^||ML("IN","filter",Key)</c>
/// sub-subscript; detail v <c>EDControls/docs/wire-formaty.md §Filter konvence</c>.
/// </summary>
[ContentProperty(nameof(Fields))]
public partial class EDFilter : UserControl
{
    /// <summary>Jmenný prostor komponenty v <see cref="IUserPrefsService"/>.</summary>
    private const string PrefsScope = "EDControls.EDFilter";

    public EDFilter()
    {
        InitializeComponent();
        Fields = new ObservableCollection<EDFilterFieldBase>();
        Fields.CollectionChanged += OnFieldsCollectionChanged;
        FieldsHost.ItemsSource = Fields;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _prefsWriter = new DebouncedUserPrefsWriter(() => UserPrefs, PrefsScope, () => HostId,
            () => EDFilterPrefsSerializer.Serialize(new EDFilterPrefs { Values = BuildFilterDict() }));
    }

    // UserPrefs ze sdíleného EDServices.Current - host nemusí mít VM dědící z EDViewModel
    // (shodný pattern jako EDList). Default je no-op NullUserPrefsService.
    /// <summary>Settable jen kvůli testům - produkce čte sdílené <c>EDServices.Current</c>.</summary>
    internal EDViewModel.EDServices? ServicesOverride { get; set; }
    private EDViewModel.EDServices Services => ServicesOverride ?? EDViewModel.EDServices.Current;
    private IUserPrefsService UserPrefs => Services.UserPrefs;

    /// <summary>
    /// Komponenta, na kterou se filter aplikuje (EDList, mapa, …). Volá se
    /// <c>Target.ApplyFilterAsync(filter)</c>.
    /// </summary>
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(IEDFilterTarget), typeof(EDFilter),
            new PropertyMetadata(null));

    public IEDFilterTarget? Target
    {
        get => (IEDFilterTarget?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Identifikátor hosta pro pamatování hodnot filtru (konvence <c>"&lt;Projekt&gt;.&lt;Consumer&gt;"</c>).
    /// Prázdný = filtr si nic nepamatuje.
    /// </summary>
    public static readonly DependencyProperty HostIdProperty =
        DependencyProperty.Register(nameof(HostId), typeof(string), typeof(EDFilter),
            new PropertyMetadata(""));

    public string? HostId
    {
        get => (string?)GetValue(HostIdProperty);
        set => SetValue(HostIdProperty, value);
    }

    /// <summary>Běží refresh. Po tu dobu je tlačítko zakázané - cizí cíl nemusí mít
    /// ochranu proti souběžným dotazům, kterou má EDList.</summary>
    private static readonly DependencyPropertyKey IsBusyPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsBusy), typeof(bool), typeof(EDFilter),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsBusyProperty = IsBusyPropertyKey.DependencyProperty;

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        private set => SetValue(IsBusyPropertyKey, value);
    }

    /// <summary>Filter pole - typicky vyplněná v XAML přes &lt;edf:EDFilterCombo/&gt; atd.</summary>
    public ObservableCollection<EDFilterFieldBase> Fields { get; }

    private bool _initialLoadDone;
    private bool _suppressAutoTrigger;
    private readonly DebouncedUserPrefsWriter _prefsWriter;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadDone) return;
        _initialLoadDone = true;
        // Subscribe je zajištěn přes OnFieldsCollectionChanged (XAML [ContentProperty]
        // přidává child přes Fields.Add() který fires CollectionChanged).
        InitialLoad();
    }

    /// <summary>
    /// Obnova uložených hodnot **před** prvním refreshem - jinak by cíl načítal dvakrát,
    /// nejdřív bez filtru a hned znovu s obnovenými hodnotami.
    /// </summary>
    private async void InitialLoad()
    {
        try
        {
            await InitialLoadAsync();
        }
        catch (Exception ex)
        {
            EDViewModel.EDServices.Current.PopupDisplayer.Error(ex);
        }
    }

    /// <summary>Internal kvůli testům - <c>Loaded</c> ve zkušebním visual tree nefíruje.</summary>
    internal async Task InitialLoadAsync()
    {
        await RestorePrefsAsync();
        await RefreshAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e) => await _prefsWriter.FlushAsync();

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
        if (_suppressAutoTrigger) return;
        _prefsWriter.Schedule();
        AutoRefresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => AutoRefresh();

    /// <summary>
    /// Automatický refresh (Loaded / změna hodnoty pole / tlačítko) - jde přes
    /// <see cref="RefreshAsync"/>, aby pomalá linka nezmrazila celou aplikaci. Výjimku musí
    /// spolknout tady: je to fire-and-forget z event handleru, takže by jinak spadla mimo
    /// jakýkoli catch.
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

    /// <summary>Sebere hodnoty ze všech filter polí a vrátí dict neprázdných hodnot.</summary>
    internal Dictionary<string, string> BuildFilterDict()
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

    /// <summary>
    /// Sebere aktuální hodnoty a předá je cíli. Bez <see cref="Target"/> je to no-op.
    /// </summary>
    public async Task RefreshAsync()
    {
        var target = Target;
        if (target is null) return;

        var filter = BuildFilterDict();
        IsBusy = true;
        try
        {
            await target.ApplyFilterAsync(filter);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Pamatování hodnot (per uživatel × IdFa, přes Caché UserData) ──────────

    /// <summary>Načte uložené hodnoty a rozdá je polím. Best-effort - filtr bez nich funguje.</summary>
    private async Task RestorePrefsAsync()
    {
        var hostId = HostId;
        if (string.IsNullOrEmpty(hostId)) return;
        try
        {
            var prefs = EDFilterPrefsSerializer.Deserialize(await UserPrefs.LoadAsync(PrefsScope, hostId));
            if (prefs is null) return;

            // Bez suppressu by každé dosazené pole spustilo vlastní refresh i vlastní uložení.
            _suppressAutoTrigger = true;
            try
            {
                foreach (var field in Fields)
                {
                    if (string.IsNullOrEmpty(field.Key)) continue;
                    if (prefs.Values.TryGetValue(field.Key, out var value)) field.RestoreValue(value);
                }
            }
            finally
            {
                _suppressAutoTrigger = false;
            }
        }
        catch { /* best-effort */ }
    }
}
