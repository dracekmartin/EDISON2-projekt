using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using EDControls.UserPrefs;

namespace EDControls.EDFilter;

/// <summary>
/// Container pro deklarativní filter pole nad zobrazovací komponentou. Filter pole jsou
/// <see cref="EDFilterFieldBase"/> children. Při změně hodnoty kteréhokoliv pole
/// (a při Loaded eventu) sebere hodnoty a předá je cíli - <see cref="Target"/>
/// (<see cref="IEDFilterTarget"/>, implementuje ho např. EDList) nebo <see cref="FilterCommand"/>.
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
    }

    // UserPrefs ze sdíleného EDServices.Current - host nemusí mít VM dědící z EDViewModel
    // (shodný pattern jako EDList). Default je no-op NullUserPrefsService.
    /// <summary>Settable jen kvůli testům - produkce čte sdílené <c>EDServices.Current</c>.</summary>
    internal EDViewModel.EDServices? ServicesOverride { get; set; }
    private EDViewModel.EDServices Services => ServicesOverride ?? EDViewModel.EDServices.Current;
    private IUserPrefsService UserPrefs => Services.UserPrefs;

    /// <summary>
    /// Komponenta, na kterou se filter aplikuje (EDList, mapa, …). Volá se
    /// <c>Target.ApplyFilterAsync(filter)</c>. Pro cíl řízený ViewModelem je tu
    /// <see cref="FilterCommand"/>; nastavit jde obojí zároveň.
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
    /// Příkaz volaný při každém refreshi, parametrem je slovník hodnot
    /// (<c>IReadOnlyDictionary&lt;string, string&gt;</c>). Pro hosty, kde filtr zpracovává
    /// ViewModel, a ne komponenta.
    /// </summary>
    public static readonly DependencyProperty FilterCommandProperty =
        DependencyProperty.Register(nameof(FilterCommand), typeof(ICommand), typeof(EDFilter),
            new PropertyMetadata(null));

    public ICommand? FilterCommand
    {
        get => (ICommand?)GetValue(FilterCommandProperty);
        set => SetValue(FilterCommandProperty, value);
    }

    /// <summary>
    /// Pokud <c>true</c> (default), změna hodnoty pole okamžitě vyvolá refresh.
    /// Pokud <c>false</c>, refresh spustí až tlačítko nebo <see cref="RefreshAsync"/>.
    /// </summary>
    public static readonly DependencyProperty AutoTriggerProperty =
        DependencyProperty.Register(nameof(AutoTrigger), typeof(bool), typeof(EDFilter),
            new PropertyMetadata(true));

    public bool AutoTrigger
    {
        get => (bool)GetValue(AutoTriggerProperty);
        set => SetValue(AutoTriggerProperty, value);
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

    /// <summary>Popisek tlačítka vpravo.</summary>
    public static readonly DependencyProperty RefreshTextProperty =
        DependencyProperty.Register(nameof(RefreshText), typeof(string), typeof(EDFilter),
            new PropertyMetadata("Načíst"));

    public string RefreshText
    {
        get => (string)GetValue(RefreshTextProperty);
        set => SetValue(RefreshTextProperty, value);
    }

    /// <summary>Viditelnost tlačítka vpravo. Default <c>true</c> i při zapnutém auto-triggeru -
    /// data se mohla změnit na serveru, uživatel si vynutí znovunačtení.</summary>
    public static readonly DependencyProperty ShowRefreshProperty =
        DependencyProperty.Register(nameof(ShowRefresh), typeof(bool), typeof(EDFilter),
            new PropertyMetadata(true));

    public bool ShowRefresh
    {
        get => (bool)GetValue(ShowRefreshProperty);
        set => SetValue(ShowRefreshProperty, value);
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

    /// <summary>Vyvoláno před každým předáním filtru cíli. Host může observovat pro vlastní logiku.</summary>
    public event EventHandler<IReadOnlyDictionary<string, string>>? FilterChanged;

    private bool _initialLoadDone;
    private bool _suppressAutoTrigger;
    private DispatcherTimer? _saveDebouncer;

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

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Dokončit jen rozdělané uložení - bezpodmínečný save by poslal request při každém
        // zavření okna, i když uživatel filtrem nehnul.
        if (_saveDebouncer is { IsEnabled: true })
        {
            _saveDebouncer.Stop();
            await SavePrefsAsync();
        }
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
        if (_suppressAutoTrigger) return;
        SchedulePrefsSave();
        if (AutoTrigger) AutoRefresh();
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

    /// <summary>
    /// Sebere aktuální hodnoty a předá je cíli. Bez <see cref="Target"/> i bez
    /// <see cref="FilterCommand"/> je to no-op včetně <see cref="FilterChanged"/> - host
    /// by jinak reagoval na refresh, který neproběhl.
    /// </summary>
    public async Task RefreshAsync()
    {
        var target = Target;
        var command = FilterCommand;
        if (target is null && command is null) return;

        var filter = BuildFilterDict();
        FilterChanged?.Invoke(this, filter);

        IsBusy = true;
        try
        {
            if (target is not null) await target.ApplyFilterAsync(filter);
            if (command is not null && command.CanExecute(filter)) command.Execute(filter);
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

    private void SchedulePrefsSave()
    {
        if (string.IsNullOrEmpty(HostId)) return;
        _saveDebouncer ??= CreateDebouncer();
        _saveDebouncer.Stop();
        _saveDebouncer.Start();
    }

    private DispatcherTimer CreateDebouncer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await SavePrefsAsync();
        };
        return timer;
    }

    private async Task SavePrefsAsync()
    {
        var hostId = HostId;
        if (string.IsNullOrEmpty(hostId)) return;
        try
        {
            var prefs = new EDFilterPrefs { Values = BuildFilterDict() };
            await UserPrefs.SaveAsync(PrefsScope, hostId, EDFilterPrefsSerializer.Serialize(prefs));
        }
        catch { /* best-effort */ }
    }
}
