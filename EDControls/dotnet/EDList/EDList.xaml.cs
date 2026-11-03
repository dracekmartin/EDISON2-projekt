using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EDControls.UserPrefs;

namespace EDControls.EDList
{
    /// <summary>View nad <see cref="EDListController"/>: mapuje stav na <c>DataGrid</c> a eventy na controller.</summary>
    public partial class EDList : UserControl, IEDFilterTarget
    {

        // MLRest + UserPrefs ze sdíleného EDServices.Current - nezávislé na Window.DataContext.
        // Host hostující EDList tak nemusí mít VM dědící z EDViewModel jen kvůli těmto službám
        // (shodný pattern jako EDInputForm). EDServices.UserPrefs defaultuje na NullUserPrefsService.
        /// <summary>Settable jen kvůli testům - produkce čte sdílené <c>EDServices.Current</c>.</summary>
        internal EDViewModel.EDServices? ServicesOverride { get; set; }
        private EDViewModel.EDServices Services => ServicesOverride ?? EDViewModel.EDServices.Current;

        private readonly EDListController _controller;

        public static readonly DependencyProperty CacheClassProperty =
            DependencyProperty.Register(nameof(CacheClass), typeof(string), typeof(EDList),
                new PropertyMetadata(""));

        public string CacheClass
        {
            get => (string)GetValue(CacheClassProperty);
            set => SetValue(CacheClassProperty, value);
        }

        /// <summary>
        /// Identifikátor hosta pro persist column layout (šířky + pořadí + sort) přes UserPrefs.
        /// Konvence: <c>"&lt;Project&gt;.&lt;ConsumerName&gt;"</c>, např. <c>"TemplateDesigner.TemplatePicker"</c>.
        /// Null nebo prázdné → persist je vypnutý (žádný Load/Save).
        /// </summary>
        public static readonly DependencyProperty HostIdProperty =
            DependencyProperty.Register(nameof(HostId), typeof(string), typeof(EDList),
                new PropertyMetadata(null));

        public string? HostId
        {
            get => (string?)GetValue(HostIdProperty);
            set => SetValue(HostIdProperty, value);
        }

        /// <summary>
        /// Styl buňky (<c>DataGridCell</c>) - pro vzhled řízený daty řádku, který sloupcové typy
        /// nepokrývají (barva textu z dat). Foreground patří na buňku, ne na řádek: implicitní
        /// <c>DataGridCell</c> styl si ho nastavuje setterem a přebije hodnotu děděnou z řádku.
        /// Vlastní styl musí mít <c>BasedOn</c>, jinak přijde o theming.
        /// </summary>
        public static readonly DependencyProperty CellStyleProperty =
            DependencyProperty.Register(nameof(CellStyle), typeof(Style), typeof(EDList),
                new PropertyMetadata(null, OnCellStyleChanged));

        public Style? CellStyle
        {
            get => (Style?)GetValue(CellStyleProperty);
            set => SetValue(CellStyleProperty, value);
        }

        private static void OnCellStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EDList list) list.DataGrid.CellStyle = (Style)e.NewValue;
        }

        /// <summary>Text vlevo v brandovém pruhu. Pruh je vidět i bez něj (jen se search boxem).</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EDList),
                new PropertyMetadata(null));

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>Akce nad řádky (Nový, Upravit, Odstranit…) jako ikony v brandovém pruhu, typicky
        /// <c>EDViewModel.Actions</c> okna - zkratky jim registruje <c>EDViewModel.AttachToWindow</c>.</summary>
        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(nameof(Actions), typeof(IEnumerable<EDViewModel.EDAction>), typeof(EDList),
                new PropertyMetadata(null));

        public IEnumerable<EDViewModel.EDAction>? Actions
        {
            get => (IEnumerable<EDViewModel.EDAction>?)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EDList),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            (d as EDList)?.RefreshFilter();

        private void RefreshFilter()
        {
            var view = CollectionViewSource.GetDefaultView(DataGrid.ItemsSource);
            if (view is null) return;
            var search = SearchText;
            // Filtruje napříč všemi viditelnými data sloupci (skipuje checkbox column, který nemá Tag=key)
            var searchableKeys = DataGrid.Columns
                .Select(c => c.GetValue(FrameworkElement.TagProperty) as string)
                .Where(k => !string.IsNullOrEmpty(k))
                .Select(k => k!)
                .ToList();
            view.Filter = string.IsNullOrWhiteSpace(search)
                ? null
                : row => row is EDListRow r && EDListFilter.Matches(r, search, searchableKeys);
            SyncHeaderCheckState();
        }

        /// <summary>Řádky v pořadí a s filtrem view - hledání skryté řádky do nich nepatří.</summary>
        private IReadOnlyList<EDListRow> DisplayedRows => [.. DataGrid.Items.OfType<EDListRow>()];

        public IReadOnlyList<ListColumn>? Columns => _controller.Columns;

        /// <summary>Aktuální načtené řádky (read-only pohled). Po <c>LoadList</c> obsahuje server data.</summary>
        public IReadOnlyList<EDListRow> Rows => _controller.Rows;

        public string? SelectedRowId =>
            DataGrid.SelectedItem is EDListRow row ? row.Id : null;

        /// <summary>Aktuálně vybraný řádek (jeho data přes indexer), nebo null.</summary>
        public EDListRow? SelectedRow => DataGrid.SelectedItem as EDListRow;

        /// <summary>Mode výběru: Single (klasický), Check (checkbox sloupec, multi-select).</summary>
        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(ListSelectionMode), typeof(EDList),
                new PropertyMetadata(ListSelectionMode.Single, OnSelectionModeChanged));

        public ListSelectionMode SelectionMode
        {
            get => (ListSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            (d as EDList)?.RebuildColumns();

        /// <summary>V Check mode: ID checknutých řádků (z <see cref="EDListRow.IsChecked"/>).
        /// V Single mode: vrátí <c>[SelectedRowId]</c> nebo prázdné.</summary>
        public IReadOnlyList<string> SelectedRowIds =>
            SelectionMode == ListSelectionMode.Check
                ? _controller.CheckedRowIds
                : SelectedRowId is { } id ? [id] : [];

        public event EventHandler<string>? RowDoubleClicked;
        public event EventHandler<string?>? SelectionChanged;
        public event EventHandler<IReadOnlyList<string>>? CheckedRowsChanged;

        public EDList()
        {
            _controller = new EDListController(() => Services);
            _controller.ColumnsChanged += RebuildColumns;
            _controller.RowsReplaced += OnRowsReplaced;
            _controller.CheckedRowsChanged += OnCheckedRowsChanged;
            _layoutWriter = new DebouncedUserPrefsWriter(() => Services.UserPrefs, EDListController.PrefsScope,
                () => HostId, () => ColumnLayoutSerializer.Serialize(SnapshotLayout()));
            InitializeComponent();
            DataGrid.ItemsSource = _controller.Rows;
            DataGrid.Sorting += OnDataGridSorting;
            Loaded += (_, _) => HookColumnEvents();
            Unloaded += OnUnloaded;
        }

        // Odhlásit před awaitem: rychlý návrat do stromu (přepnutí záložky) mezitím znovu přihlásí.
        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnhookColumnEvents();
            await _layoutWriter.FlushAsync();
        }

        // ── Veřejné API ───────────────────────────────────────────────────────

        public void InitializeList() => _controller.InitializeList(CacheClass);

        /// <summary>
        /// Async varianta <see cref="InitializeList"/> - dotaz běží na threadpoolu, výsledek dosedne
        /// na UI vlákně. Preferuj ji všude, kde host běží nad pomalou linkou: synchronní varianta
        /// zmrazí **celou aplikaci** na dobu round-tripu, ne jen hostující okno.
        /// </summary>
        public Task InitializeListAsync() => _controller.InitializeListAsync(CacheClass);

        public void LoadList() => LoadList(filter: null);

        /// <summary>
        /// Načte data z Caché s volitelným filtrem. Filter hodnoty jdou do Caché jako sub-subscript
        /// pod <c>filter</c>: <c>filter["agendaCode"] = "MHD"</c> → <c>^||ML("IN","filter","agendaCode") = "MHD"</c>.
        /// Caché endpoint čte přes <c>$g(^||ML("IN","filter","klic"))</c>. Detail viz
        /// <c>EDControls/docs/wire-formaty.md §Filter konvence</c>.
        /// </summary>
        public void LoadList(IReadOnlyDictionary<string, string>? filter) => _controller.LoadList(CacheClass, filter);

        public Task LoadListAsync() => LoadListAsync(filter: null);

        /// <summary>
        /// Async varianta <see cref="LoadList(IReadOnlyDictionary{string, string}?)"/> - viz
        /// <see cref="InitializeListAsync"/>. Dotaz jede na klonu MLRest (sdílenou instanci nesahá) a rychlé
        /// přepínání filtru je ošetřené: dotazy téhož seznamu jdou po sobě a odpověď staršího už nedosedne.
        /// </summary>
        public Task LoadListAsync(IReadOnlyDictionary<string, string>? filter) => _controller.LoadListAsync(CacheClass, filter);

        /// <summary><c>IEDFilterTarget</c> - napojení na <c>EDFilter</c>. Prázdný slovník = bez filtru.</summary>
        public Task ApplyFilterAsync(IReadOnlyDictionary<string, string> filter) => LoadListAsync(filter);

        /// <summary>
        /// Naplní seznam daty, která si sehnal host - EDList sám nic z Caché nečte, takže nepotřebuje
        /// ani <see cref="CacheClass"/>, ani MLRest. Pro hosty s vlastním transportem (dávkovaný
        /// request přes víc komponent, data z jiného API). Řádky jsou raw (serverové) hodnoty včetně
        /// klíče <c>id</c>; formátování i sort klíče staví stejná cesta jako <see cref="LoadList()"/>.
        /// </summary>
        public void SetData(IReadOnlyList<ListColumn> columns, IEnumerable<IReadOnlyDictionary<string, string?>> rows) =>
            _controller.SetData(columns, rows);

        /// <summary>Nastaví checked stav konkrétního řádku.</summary>
        public void SetChecked(string rowId, bool isChecked) => _controller.SetChecked(rowId, isChecked);

        // ── Reakce na controller ──────────────────────────────────────────────

        private void OnRowsReplaced()
        {
            RefreshFilter();  // re-apply current search po LoadList (nový dataset)
            // Aplikuj persistent column layout asynchronně po data load - minor flicker (default
            // sloupce → persistované), ale UI neblokuje.
            _ = LoadLayoutAsync();
        }

        private CheckBox? _headerCheckBox;

        private void OnCheckedRowsChanged()
        {
            SyncHeaderCheckState();
            CheckedRowsChanged?.Invoke(this, SelectedRowIds);
        }

        private void SyncHeaderCheckState()
        {
            if (_headerCheckBox is null) return;
            var state = _controller.HeaderCheckState(DisplayedRows);
            if (_headerCheckBox.IsChecked != state) _headerCheckBox.IsChecked = state;
        }

        private void RebuildColumns()
        {
            bool hooked = _columnEventsHooked;
            UnhookColumnEvents();
            DataGrid.Columns.Clear();
            _headerCheckBox = null;
            if (SelectionMode == ListSelectionMode.Check)
                DataGrid.Columns.Add(EDListColumnFactory.BuildCheckBoxColumn(OnRowCheckClick,
                    () => _controller.ToggleAllFromHeader(DisplayedRows), out _headerCheckBox));
            foreach (var col in _controller.Columns?.Where(c => c.Visible) ?? [])
                DataGrid.Columns.Add(EDListColumnFactory.BuildDataColumn(col));
            // Nové sloupce šipky nemají, CustomSort ale řadí dál - bez obnovy by po přepnutí
            // SelectionMode nebo dalším SetData byl seznam seřazený bez viditelného důvodu.
            if (_controller.PruneSort(HasColumn)) ApplySort();
            ShowSortArrows();
            if (hooked) HookColumnEvents();
        }

        // ── Zaškrtnutí, řazení, výběr ─────────────────────────────────────────

        /// <summary>User klikl na CheckBox v řádku; se Shiftem zaškrtne rozsah od kotvy v zobrazeném pořadí.</summary>
        private void OnRowCheckClick(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.DataContext is not EDListRow row) return;
            bool extend = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            _controller.CheckFromClick(row, cb.IsChecked == true, extend ? DisplayedRows : null);
        }

        /// <summary>
        /// Vlastní řazení místo DataGrid defaultu - SortDescriptions by řadily formátované
        /// stringy (NUMBER/DATE lexikograficky). CustomSort porovnává typované sort klíče
        /// z raw hodnot. Shift+klik přidává další sloupec (multi-sort jako WPF default).
        /// </summary>
        private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
        {
            if (e.Column.GetValue(FrameworkElement.TagProperty) is not string key) return;
            e.Handled = true;
            bool additive = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            if (!additive)
                foreach (var col in DataGrid.Columns)
                    if (!ReferenceEquals(col, e.Column)) col.SortDirection = null;
            // Vizuální šipka + trigger persist layoutu.
            e.Column.SortDirection = _controller.ToggleSort(key, e.Column.SortDirection, additive);
            ApplySort();
        }

        private void ApplySort()
        {
            if (CollectionViewSource.GetDefaultView(DataGrid.ItemsSource) is ListCollectionView view)
                view.CustomSort = _controller.SortComparer;
        }

        private void ShowSortArrows()
        {
            foreach (var (key, dir) in _controller.SortSpecs) FindColumnByKey(key)!.SortDirection = dir;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectionMode == ListSelectionMode.Check && SelectedRow is EDListRow row)
                _controller.ToggleChecked(row);
            if (SelectedRowId is string id)
                RowDoubleClicked?.Invoke(this, id);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, SelectedRowId);
        }

        // ── Persist layoutu ───────────────────────────────────────────────────

        private readonly DebouncedUserPrefsWriter _layoutWriter;
        private bool _suppressLayoutSave;  // true během ApplyLayout - eventy nesmí trigger save

        private static readonly DependencyProperty[] LayoutProperties =
            [DataGridColumn.ActualWidthProperty, DataGridColumn.DisplayIndexProperty, DataGridColumn.SortDirectionProperty];

        // Odběr drží list ve statické tabulce WPF, proto platí jen mezi Loaded a Unloaded.
        private bool _columnEventsHooked;

        /// <summary>Subscribe na width/displayIndex/sort změny sloupců - trigger debounced save.</summary>
        private void HookColumnEvents()
        {
            if (_columnEventsHooked) return;
            _columnEventsHooked = true;
            foreach (var col in DataGrid.Columns.Where(c => c.GetValue(FrameworkElement.TagProperty) is string))
                foreach (var dp in LayoutProperties)
                    DependencyPropertyDescriptor.FromProperty(dp, typeof(DataGridColumn))?.AddValueChanged(col, OnColumnPropertyChanged);
        }

        private void UnhookColumnEvents()
        {
            if (!_columnEventsHooked) return;
            _columnEventsHooked = false;
            foreach (var col in DataGrid.Columns.Where(c => c.GetValue(FrameworkElement.TagProperty) is string))
                foreach (var dp in LayoutProperties)
                    DependencyPropertyDescriptor.FromProperty(dp, typeof(DataGridColumn))?.RemoveValueChanged(col, OnColumnPropertyChanged);
        }

        private void OnColumnPropertyChanged(object? sender, EventArgs e)
        {
            if (_suppressLayoutSave) return;
            _layoutWriter.Schedule();
        }

        private ColumnLayout SnapshotLayout()
        {
            var keyed = DataGrid.Columns.Where(c => c.GetValue(FrameworkElement.TagProperty) is string).ToList();
            var columns = keyed.Select(c => new ColumnPref
            {
                Key = (string)c.GetValue(FrameworkElement.TagProperty),
                Width = c.ActualWidth,
                DisplayIndex = c.DisplayIndex,
            }).ToList();
            var sort = keyed.Where(c => c.SortDirection.HasValue).Select(c => new SortPref
            {
                Key = (string)c.GetValue(FrameworkElement.TagProperty),
                Direction = c.SortDirection == ListSortDirection.Descending ? "desc" : "asc",
            }).ToList();
            return new ColumnLayout { Columns = columns, Sort = sort.Count > 0 ? sort : null };
        }

        /// <summary>Načte layout z UserPrefs a aplikuje na aktuální sloupce. Best-effort.</summary>
        private async Task LoadLayoutAsync()
        {
            if (await _controller.LoadLayoutAsync(HostId) is not { } layout) return;
            try { ApplyLayout(layout); }
            catch { /* best-effort */ }
        }

        private void ApplyLayout(ColumnLayout layout)
        {
            _suppressLayoutSave = true;
            try
            {
                // 1) Šířky - apply v current pořadí
                foreach (var pref in layout.Columns)
                {
                    var col = FindColumnByKey(pref.Key);
                    if (col is null) continue;
                    if (pref.Width > 0) col.Width = new DataGridLength(pref.Width);
                }
                // 2) DisplayIndex - apply v cílovém pořadí (low → high), DataGrid přesype zbytek
                foreach (var pref in layout.Columns.OrderBy(p => p.DisplayIndex))
                {
                    var col = FindColumnByKey(pref.Key);
                    if (col is null) continue;
                    if (pref.DisplayIndex >= 0 && pref.DisplayIndex < DataGrid.Columns.Count)
                        col.DisplayIndex = pref.DisplayIndex;
                }
                // 3) Sort - vizuální šipka + typovaný CustomSort (viz OnDataGridSorting)
                if (layout.Sort is { Count: > 0 })
                {
                    // Šipka sloupce mimo uložený layout by svítila dál, ale neřadila by
                    // a příští uložení by ji zapsalo do sortu.
                    foreach (var col in DataGrid.Columns) col.SortDirection = null;
                    _controller.RestoreSort(layout.Sort, HasColumn);
                    ShowSortArrows();
                    ApplySort();
                }
            }
            finally { _suppressLayoutSave = false; }
        }

        private bool HasColumn(string key) => FindColumnByKey(key) is not null;

        // Prázdný klíč (poškozený uložený layout) by jinak trefil checkbox sloupec, který Tag nemá.
        private DataGridColumn? FindColumnByKey(string? key) => string.IsNullOrEmpty(key) ? null :
            DataGrid.Columns.FirstOrDefault(c => c.GetValue(FrameworkElement.TagProperty) as string == key);
    }
}
