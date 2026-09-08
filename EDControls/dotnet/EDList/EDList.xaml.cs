using ALWA.SYS;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace EDControls.EDList
{
    public partial class EDList : UserControl
    {

        // MLRest + UserPrefs ze sdíleného EDServices.Current - nezávislé na Window.DataContext.
        // Host hostující EDList tak nemusí mít VM dědící z EDViewModel jen kvůli těmto službám
        // (shodný pattern jako EDInputForm). EDServices.UserPrefs defaultuje na NullUserPrefsService.
        private IMLRest? MLRest => EDViewModel.EDServices.Current.MLRest;
        private IUserPrefsService UserPrefs => EDViewModel.EDServices.Current.UserPrefs;

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

        private const string PrefsScope = "EDControls.EDList";

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EDList),
                new PropertyMetadata(null, OnTitleOrSearchVisibleChanged));

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>Default: <c>null</c> = auto (true pokud Title set, jinak false). Lze nastavit explicitně true/false.</summary>
        public static readonly DependencyProperty SearchVisibleProperty =
            DependencyProperty.Register(nameof(SearchVisible), typeof(bool?), typeof(EDList),
                new PropertyMetadata(null, OnTitleOrSearchVisibleChanged));

        public bool? SearchVisible
        {
            get => (bool?)GetValue(SearchVisibleProperty);
            set => SetValue(SearchVisibleProperty, value);
        }

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(EDList),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private static void OnTitleOrSearchVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            (d as EDList)?.UpdateHeaderVisibility();

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            (d as EDList)?.RefreshFilter();

        private void UpdateHeaderVisibility()
        {
            bool showTitle  = !string.IsNullOrEmpty(Title);
            bool showSearch = SearchVisible ?? !string.IsNullOrEmpty(Title);   // null = auto z Title
            HeaderToolbar.ShowSearch = showSearch;
            HeaderToolbar.Visibility = (showTitle || showSearch) ? Visibility.Visible : Visibility.Collapsed;
        }

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
        }

        private IReadOnlyList<ListColumn>? _columns;
        public IReadOnlyList<ListColumn>? Columns => _columns;

        private readonly BulkObservableCollection<EDListRow> _rows = new();

        /// <summary>Aktuální načtené řádky (read-only pohled). Po <c>LoadList</c> obsahuje server data.</summary>
        public IReadOnlyList<EDListRow> Rows => _rows;

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
                ? _rows.Where(r => r.IsChecked).Select(r => r.Id).ToList()
                : SelectedRowId is { } id ? [id] : [];

        private bool _suppressCheckedEvents;

        public event EventHandler<string>? RowDoubleClicked;
        public event EventHandler<string?>? SelectionChanged;
        public event EventHandler<(string RowId, string ColKey)>? CellLinkClicked;
        public event EventHandler<IReadOnlyList<string>>? CheckedRowsChanged;

        public EDList()
        {
            InitializeComponent();
            DataGrid.ItemsSource = _rows;
            DataGrid.Sorting += OnDataGridSorting;
            Unloaded += OnUnloaded;
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Force save layout - pokud byl debouncer ještě běžel, dokončit teď.
            if (_saveDebouncer is { IsEnabled: true })
            {
                _saveDebouncer.Stop();
                await SaveLayoutAsync();
            }
            UnhookColumnEvents();
        }

        /// <summary>Nastaví checked stav konkrétního řádku.</summary>
        public void SetChecked(string rowId, bool isChecked)
        {
            var row = _rows.FirstOrDefault(r => r.Id == rowId);
            if (row is not null) row.IsChecked = isChecked;
        }

        /// <summary>Zaškrtne všechny aktuálně viditelné řádky (po LoadList).</summary>
        public void CheckAll() => SetAllChecked(true);

        /// <summary>Odznačí všechny řádky.</summary>
        public void UncheckAll() => SetAllChecked(false);

        private void SetAllChecked(bool value)
        {
            _suppressCheckedEvents = true;
            try
            {
                foreach (var row in _rows) row.IsChecked = value;
            }
            finally { _suppressCheckedEvents = false; }
            FireCheckedRowsChanged();
        }

        private void FireCheckedRowsChanged()
        {
            SyncHeaderCheckState();
            CheckedRowsChanged?.Invoke(this, SelectedRowIds);
        }

        private CheckBox? _headerCheckBox;

        // Kotva pro Shift+klik výběr rozsahu (poslední řádek, na jehož checkbox se kliklo / dvojklik).
        private EDListRow? _checkAnchor;

        /// <summary>User klikl na CheckBox v řádku. Bez modifikátoru přepne jen daný řádek; se
        /// Shiftem zaškrtne/odškrtne celý rozsah od kotvy po tento řádek (v zobrazeném pořadí).</summary>
        private void OnRowCheckClick(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.DataContext is not EDListRow row) return;
            bool target = cb.IsChecked == true;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _checkAnchor is not null)
            {
                // Rozsah v zobrazeném pořadí (respektuje řazení i filtr).
                var items = DataGrid.Items.OfType<EDListRow>().ToList();
                int a = items.IndexOf(_checkAnchor);
                int b = items.IndexOf(row);
                if (a >= 0 && b >= 0)
                {
                    if (a > b) (a, b) = (b, a);
                    _suppressCheckedEvents = true;
                    try { for (int i = a; i <= b; i++) items[i].IsChecked = target; }
                    finally { _suppressCheckedEvents = false; }
                    FireCheckedRowsChanged();
                }
                else row.IsChecked = target;   // kotva mimo aktuální pohled → fallback na jeden řádek
            }
            else
            {
                row.IsChecked = target;
            }
            _checkAnchor = row;
        }

        /// <summary>Aktualizuje header CheckBox podle aggregate stavu (true / false / null = mixed).</summary>
        private void SyncHeaderCheckState()
        {
            if (_headerCheckBox is null) return;
            int total = _rows.Count;
            int checkedCount = _rows.Count(r => r.IsChecked);
            bool? state = total == 0 ? false
                        : checkedCount == 0 ? false
                        : checkedCount == total ? true
                        : null;  // mixed
            if (_headerCheckBox.IsChecked != state) _headerCheckBox.IsChecked = state;
        }

        private void OnRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_suppressCheckedEvents) return;
            if (e.PropertyName == nameof(EDListRow.IsChecked))
                FireCheckedRowsChanged();
        }

        public void InitializeList() =>
            ApplyColumns(EnsureConfigured().QueryOrThrow(CacheClass, "InitializeList", new CVar()));

        /// <summary>
        /// Async varianta <see cref="InitializeList"/> - dotaz běží na threadpoolu, výsledek dosedne
        /// na UI vlákně. Preferuj ji všude, kde host běží nad pomalou linkou: synchronní varianta
        /// zmrazí **celou aplikaci** na dobu round-tripu, ne jen hostující okno.
        /// </summary>
        public async Task InitializeListAsync()
        {
            var mlRest = EnsureConfigured();
            var cacheClass = CacheClass;   // DependencyProperty → čti na UI vlákně, ne uvnitř Task.Run
            var output = await RunQueryAsync(() => mlRest.QueryOrThrow(cacheClass, "InitializeList", new CVar()));
            ApplyColumns(output);
        }

        public void LoadList() => LoadList(filter: null);

        /// <summary>
        /// Načte data z Caché s volitelným filtrem. Filter hodnoty jdou do Caché jako sub-subscript
        /// pod <c>filter</c>: <c>filter["agendaCode"] = "MHD"</c> → <c>^||ML("IN","filter","agendaCode") = "MHD"</c>.
        /// Caché endpoint čte přes <c>$g(^||ML("IN","filter","klic"))</c>. Detail viz
        /// <c>EDControls/DOCS/01-wire-formats.md §Filter konvence</c>.
        /// </summary>
        public void LoadList(IReadOnlyDictionary<string, string>? filter)
        {
            _loadGeneration++;   // sync načtení je autoritativní - případná async odpověď v letu se zahodí
            ApplyRows(EnsureConfigured().QueryOrThrow(CacheClass, "LoadList", EDListParsing.BuildLoadInput(filter)));
        }

        public Task LoadListAsync() => LoadListAsync(filter: null);

        /// <summary>
        /// Async varianta <see cref="LoadList(IReadOnlyDictionary{string, string}?)"/> - viz
        /// <see cref="InitializeListAsync"/>. Rychlé přepínání filtru je ošetřené: dotazy se serializují
        /// (sdílený <c>MLRest</c> jede přes procesní globály <c>^||ML</c>, dva souběžné by si je přepsaly)
        /// a odpověď staršího dotazu už do seznamu nedosedne.
        /// </summary>
        public async Task LoadListAsync(IReadOnlyDictionary<string, string>? filter)
        {
            var mlRest = EnsureConfigured();
            var cacheClass = CacheClass;   // DependencyProperty → čti na UI vlákně, ne uvnitř Task.Run
            var input = EDListParsing.BuildLoadInput(filter);
            var generation = ++_loadGeneration;

            var output = await RunQueryAsync(() => mlRest.QueryOrThrow(cacheClass, "LoadList", input));

            if (generation != _loadGeneration) return;   // mezitím přišel novější požadavek
            ApplyRows(output);
        }

        // Serializace dotazů - sdílený MLRest komunikuje přes procesní globály ^||ML, takže dva
        // souběžné dotazy z téhož EDListu by si vzájemně přepsaly vstup i výstup.
        private readonly System.Threading.SemaphoreSlim _queryGate = new(1, 1);
        private int _loadGeneration;

        private async Task<CVar> RunQueryAsync(Func<CVar> query)
        {
            await _queryGate.WaitAsync();
            try { return await Task.Run(query); }
            finally { _queryGate.Release(); }
        }

        private void ApplyColumns(CVar output)
        {
            _columns = EDListParsing.ParseColumns(output);
            RebuildColumns();
        }

        private void ApplyRows(CVar output)
        {
            // Unsubscribe stará data - žádné spying na zaniklé řádky
            foreach (var oldRow in _rows) oldRow.PropertyChanged -= OnRowPropertyChanged;
            _checkAnchor = null;

            // ToList: ParseRows je líný iterátor - druhý průchod by vyrobil jiné instance řádků
            // a odběr změn by visel mimo seznam.
            var newRows = EDListParsing.ParseRows(output, _columns).ToList();
            foreach (var row in newRows) row.PropertyChanged += OnRowPropertyChanged;
            _rows.ReplaceAll(newRows);

            FireCheckedRowsChanged();
            RefreshFilter();  // re-apply current search po LoadList (nový dataset)
            // Aplikuj persistent column layout asynchronně po data load - minor flicker (default
            // sloupce → persistované), ale UI neblokuje.
            _ = LoadLayoutAsync();
        }

        private void RebuildColumns()
        {
            UnhookColumnEvents();
            DataGrid.Columns.Clear();
            if (SelectionMode == ListSelectionMode.Check)
                DataGrid.Columns.Add(BuildCheckBoxColumn());
            if (_columns is null) return;
            foreach (var col in _columns.Where(c => c.Visible))
            {
                var dgCol = BuildDataGridColumn(col);
                dgCol.SetValue(FrameworkElement.TagProperty, col.Key);  // mapping zpět při SnapshotLayout
                DataGrid.Columns.Add(dgCol);
            }
            HookColumnEvents();
        }

        private DataGridColumn BuildCheckBoxColumn()
        {
            var checkBox = new FrameworkElementFactory(typeof(CheckBox));
            checkBox.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            // OneWay binding: source (EDListRow.IsChecked) → CheckBox (programmatic změny propagují
            // do UI přes INotifyPropertyChanged). User click řeší explicit ClickEvent handler níže -
            // DataGridTemplateColumn.CellTemplate má známé problémy s reverzní cestou TwoWay binding.
            checkBox.SetBinding(CheckBox.IsCheckedProperty,
                new Binding(nameof(EDListRow.IsChecked)) { Mode = BindingMode.OneWay });
            checkBox.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(OnRowCheckClick));

            _headerCheckBox = new CheckBox
            {
                ToolTip = "Označit / odznačit vše viditelné",
                IsThreeState = true  // null = mixed state (zobrazí jako čtvereček)
            };
            // Click (ne Checked/Unchecked) fires jen z user akce, ne z programmatic IsChecked.
            // Tím se vyhneme rekurzi: CheckAll → SyncHeaderCheckState → IsChecked set → Click NEFires.
            _headerCheckBox.Click += (_, _) =>
            {
                int total = _rows.Count;
                int checkedCount = _rows.Count(r => r.IsChecked);
                if (checkedCount == total && total > 0) UncheckAll();
                else CheckAll();
            };

            return new DataGridTemplateColumn
            {
                Header = _headerCheckBox,
                Width = new DataGridLength(32),
                CellTemplate = new DataTemplate { VisualTree = checkBox },
                CanUserSort = false,
                CanUserReorder = false,
            };
        }

        private DataGridColumn BuildDataGridColumn(ListColumn col)
        {
            var width = col.Width > 0
                ? new DataGridLength(col.Width)
                : new DataGridLength(1, DataGridLengthUnitType.Auto);

            var align = col.Align == ListColumnAlign.Auto ? EDListParsing.DefaultAlign(col.Type) : col.Align;

            if (col.IsLink)
            {
                var tb = new FrameworkElementFactory(typeof(TextBlock));
                tb.SetBinding(TextBlock.TextProperty, new Binding($"[{col.Key}]"));
                tb.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
                tb.SetValue(TextBlock.CursorProperty, Cursors.Hand);
                // SetResourceReference (= DynamicResource): SystemColors.HotTrackBrush je
                // systémová barva, kterou dark mode nepřepne.
                tb.SetResourceReference(TextBlock.ForegroundProperty, "EDTheme.Accent.Blue");
                if (align == ListColumnAlign.Right)
                    tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
                else if (align == ListColumnAlign.Center)
                    tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                tb.AddHandler(TextBlock.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((sender, _) =>
                {
                    if (sender is TextBlock el && el.DataContext is EDListRow row)
                        CellLinkClicked?.Invoke(this, (row.Id, col.Key));
                }));
                return new DataGridTemplateColumn
                {
                    Header = col.Label,
                    Width = width,
                    CellTemplate = new DataTemplate { VisualTree = tb },
                    CanUserSort = col.Sortable,
                };
            }

            if (col.Type == ListColumnType.Color)
            {
                var rect = new FrameworkElementFactory(typeof(Rectangle));
                rect.SetValue(Rectangle.HeightProperty, 16.0);
                rect.SetValue(Rectangle.WidthProperty, 30.0);
                rect.SetBinding(Rectangle.FillProperty,
                    new Binding($"[{col.Key}]") { Converter = new RgbStringToBrushConverter() });
                return new DataGridTemplateColumn
                {
                    Header = col.Label,
                    Width = width,
                    CellTemplate = new DataTemplate { VisualTree = rect },
                    CanUserSort = col.Sortable,
                };
            }

            var textCol = new DataGridTextColumn
            {
                Header = col.Label,
                Binding = new Binding($"[{col.Key}]"),
                Width = width,
                CanUserSort = col.Sortable,
            };

            if (align != ListColumnAlign.Left)
            {
                var ha = align == ListColumnAlign.Right
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center;
                // ElementStyle implicitní <Style TargetType="TextBlock"> nahrazuje, nedědí -
                // bez BasedOn by zarovnané sloupce minuly typografii z EDControlStyles.
                textCol.ElementStyle = new Style(typeof(TextBlock))
                {
                    BasedOn = Application.Current?.TryFindResource(typeof(TextBlock)) as Style,
                    Setters = { new Setter(TextBlock.HorizontalAlignmentProperty, ha) }
                };
            }

            return textCol;
        }

        private readonly List<(string Key, ListSortDirection Dir)> _sortSpecs = [];

        /// <summary>
        /// Vlastní řazení místo DataGrid defaultu - SortDescriptions by řadily formátované
        /// stringy (NUMBER/DATE lexikograficky). CustomSort porovnává typované sort klíče
        /// z raw hodnot. Shift+klik přidává další sloupec (multi-sort jako WPF default).
        /// </summary>
        private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
        {
            if (e.Column.GetValue(FrameworkElement.TagProperty) is not string key) return;
            e.Handled = true;
            var newDir = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                _sortSpecs.Clear();
                foreach (var col in DataGrid.Columns)
                    if (!ReferenceEquals(col, e.Column)) col.SortDirection = null;
            }
            _sortSpecs.RemoveAll(s => s.Key == key);
            _sortSpecs.Add((key, newDir));
            e.Column.SortDirection = newDir;   // vizuální šipka + trigger persist layoutu
            ApplySort();
        }

        private void ApplySort()
        {
            if (CollectionViewSource.GetDefaultView(DataGrid.ItemsSource) is not ListCollectionView view) return;
            view.CustomSort = _sortSpecs.Count == 0
                ? null
                : new EDListRowComparer([.. _sortSpecs.Select(s => (s.Key, s.Dir == ListSortDirection.Descending))]);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // V Check mode dvojklik na řádek přepne zaškrtnutí (rychlejší než trefit úzký checkbox).
            if (SelectionMode == ListSelectionMode.Check && SelectedRow is EDListRow row)
            {
                row.IsChecked = !row.IsChecked;
                _checkAnchor = row;
            }
            if (SelectedRowId is string id)
                RowDoubleClicked?.Invoke(this, id);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, SelectedRowId);
        }

        /// <summary>Stráž pro veřejné API - vrací MLRest, aby ho volající nemusel dereferencovat sám.</summary>
        private IMLRest EnsureConfigured()
        {
            if (MLRest is null || string.IsNullOrEmpty(CacheClass))
                throw new InvalidOperationException("EDList: MLRest a CacheClass musí být nastaveny před použitím.");
            return MLRest;
        }

        private DispatcherTimer? _saveDebouncer;
        private bool _suppressLayoutSave;  // true během ApplyLayout - eventy nesmí trigger save

        /// <summary>Subscribe na width/displayIndex/sort změny sloupců - trigger debounced save.</summary>
        private void HookColumnEvents()
        {
            foreach (var col in DataGrid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string) continue;
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn))
                    ?.AddValueChanged(col, OnColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
                    ?.AddValueChanged(col, OnColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.SortDirectionProperty, typeof(DataGridColumn))
                    ?.AddValueChanged(col, OnColumnPropertyChanged);
            }
        }

        private void UnhookColumnEvents()
        {
            foreach (var col in DataGrid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string) continue;
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn))
                    ?.RemoveValueChanged(col, OnColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn))
                    ?.RemoveValueChanged(col, OnColumnPropertyChanged);
                DependencyPropertyDescriptor.FromProperty(DataGridColumn.SortDirectionProperty, typeof(DataGridColumn))
                    ?.RemoveValueChanged(col, OnColumnPropertyChanged);
            }
        }

        private void OnColumnPropertyChanged(object? sender, EventArgs e)
        {
            if (_suppressLayoutSave) return;
            ScheduleSaveLayout();
        }

        private void ScheduleSaveLayout()
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
                await SaveLayoutAsync();
            };
            return timer;
        }

        /// <summary>Sebere current layout z DataGridu a uloží přes UserPrefs.</summary>
        private async Task SaveLayoutAsync()
        {
            if (string.IsNullOrEmpty(HostId)) return;
            try
            {
                var layout = SnapshotLayout();
                await UserPrefs.SaveAsync(PrefsScope, HostId, ColumnLayoutSerializer.Serialize(layout));
            }
            catch { /* best-effort */ }
        }

        private ColumnLayout SnapshotLayout()
        {
            var columns = new List<ColumnPref>();
            foreach (var col in DataGrid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string key) continue;
                columns.Add(new ColumnPref
                {
                    Key = key,
                    Width = col.ActualWidth,
                    DisplayIndex = col.DisplayIndex,
                });
            }
            var sort = DataGrid.Columns
                .Where(c => c.SortDirection.HasValue && c.GetValue(FrameworkElement.TagProperty) is string)
                .Select(c => new SortPref
                {
                    Key = (string)c.GetValue(FrameworkElement.TagProperty),
                    Direction = c.SortDirection == ListSortDirection.Descending ? "desc" : "asc",
                }).ToList();
            return new ColumnLayout { Columns = columns, Sort = sort.Count > 0 ? sort : null };
        }

        /// <summary>Načte layout z UserPrefs a aplikuje na aktuální sloupce. Best-effort.</summary>
        private async Task LoadLayoutAsync()
        {
            if (string.IsNullOrEmpty(HostId)) return;
            try
            {
                var json = await UserPrefs.LoadAsync(PrefsScope, HostId);
                var layout = ColumnLayoutSerializer.Deserialize(json);
                if (layout is null) return;
                ApplyLayout(layout);
            }
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
                    _sortSpecs.Clear();
                    foreach (var sort in layout.Sort)
                    {
                        var col = FindColumnByKey(sort.Key);
                        if (col is null) continue;
                        var dir = sort.Direction == "desc" ? ListSortDirection.Descending : ListSortDirection.Ascending;
                        col.SortDirection = dir;
                        _sortSpecs.Add((sort.Key, dir));
                    }
                    ApplySort();
                }
            }
            finally { _suppressLayoutSave = false; }
        }

        private DataGridColumn? FindColumnByKey(string key) =>
            DataGrid.Columns.FirstOrDefault(c => c.GetValue(FrameworkElement.TagProperty) as string == key);
    }

    internal class RgbStringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                var parts = s.Split(';');
                if (parts.Length == 3
                    && byte.TryParse(parts[0], out byte r)
                    && byte.TryParse(parts[1], out byte g)
                    && byte.TryParse(parts[2], out byte b))
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
