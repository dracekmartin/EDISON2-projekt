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
        // ─── Konfigurace ──────────────────────────────────────────────────────────

        private IMLRest? MLRest => (Window.GetWindow(this)?.DataContext as EDViewModel.EDViewModel)?.MLRest;
        private IUserPrefsService UserPrefs =>
            (Window.GetWindow(this)?.DataContext as EDViewModel.EDViewModel)?.UserPrefs
            ?? new NullUserPrefsService();

        public string CacheClass { get; set; } = "";

        /// <summary>
        /// Identifikátor hosta pro persist column layout (šířky + pořadí + sort) přes UserPrefs.
        /// Konvence: <c>"&lt;Project&gt;.&lt;ConsumerName&gt;"</c>, např. <c>"TemplateDesigner.TemplatePicker"</c>.
        /// Null nebo prázdné → persist je vypnutý (žádný Load/Save).
        /// </summary>
        public string? HostId { get; set; }
        private const string PrefsScope = "EDControls.EDList";

        // ─── Title + Search (L3) ──────────────────────────────────────────────────

        private string? _title;
        public string? Title
        {
            get => _title;
            set { _title = value; titleText.Text = value ?? ""; UpdateHeaderVisibility(); }
        }

        public string SearchText
        {
            get => searchBox.Text;
            set { searchBox.Text = value; /* TextChanged event triggers re-filter */ }
        }

        private bool? _searchVisible;
        /// <summary>Default: true pokud Title set, jinak false. Lze nastavit explicitně.</summary>
        public bool SearchVisible
        {
            get => _searchVisible ?? !string.IsNullOrEmpty(_title);
            set { _searchVisible = value; UpdateHeaderVisibility(); }
        }

        private void UpdateHeaderVisibility()
        {
            bool showTitle  = !string.IsNullOrEmpty(_title);
            bool showSearch = SearchVisible;
            titleText.Visibility  = showTitle  ? Visibility.Visible : Visibility.Collapsed;
            searchBox.Visibility  = showSearch ? Visibility.Visible : Visibility.Collapsed;
            headerBar.Visibility  = (showTitle || showSearch) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFilter();

        private void RefreshFilter()
        {
            var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
            if (view is null) return;
            var search = searchBox.Text;
            // Filtruje napříč všemi viditelnými data sloupci (skipuje checkbox column, který nemá Tag=key)
            var searchableKeys = dataGrid.Columns
                .Select(c => c.GetValue(FrameworkElement.TagProperty) as string)
                .Where(k => !string.IsNullOrEmpty(k))
                .Select(k => k!)
                .ToList();
            view.Filter = string.IsNullOrWhiteSpace(search)
                ? null
                : row => row is EDListRow r && EDListFilter.Matches(r, search, searchableKeys);
        }

        // ─── Stav ─────────────────────────────────────────────────────────────────

        private IReadOnlyList<ListColumn>? _columns;
        public IReadOnlyList<ListColumn>? Columns => _columns;

        private readonly ObservableCollection<EDListRow> _rows = new();

        /// <summary>Aktuální načtené řádky (read-only pohled). Po <c>LoadList</c> obsahuje server data.</summary>
        public IReadOnlyList<EDListRow> Rows => _rows;

        public string? SelectedRowId =>
            dataGrid.SelectedItem is EDListRow row ? row.Id : null;

        /// <summary>Aktuálně vybraný řádek (jeho data přes indexer), nebo null.</summary>
        public EDListRow? SelectedRow => dataGrid.SelectedItem as EDListRow;

        // ─── Multi-select (L1) ────────────────────────────────────────────────────

        /// <summary>Mode výběru: Single (klasický), Check (checkbox sloupec, multi-select).</summary>
        public EListSelectionMode SelectionMode
        {
            get => _selectionMode;
            set { if (_selectionMode != value) { _selectionMode = value; RebuildColumns(); } }
        }
        private EListSelectionMode _selectionMode = EListSelectionMode.Single;

        /// <summary>V Check mode: ID checknutých řádků (z <see cref="EDListRow.IsChecked"/>).
        /// V Single mode: vrátí <c>[SelectedRowId]</c> nebo prázdné.</summary>
        public IReadOnlyList<string> SelectedRowIds =>
            _selectionMode == EListSelectionMode.Check
                ? _rows.Where(r => r.IsChecked).Select(r => r.Id).ToList()
                : SelectedRowId is { } id ? [id] : [];

        private bool _suppressCheckedEvents;

        // ─── Eventy ───────────────────────────────────────────────────────────────

        public event EventHandler<string>? RowDoubleClicked;
        public event EventHandler<string?>? SelectionChanged;
        public event EventHandler<(string RowId, string ColKey)>? CellLinkClicked;
        public event EventHandler<IReadOnlyList<string>>? CheckedRowsChanged;

        // ─── Inicializace ─────────────────────────────────────────────────────────

        public EDList()
        {
            InitializeComponent();
            dataGrid.ItemsSource = _rows;
            Unloaded += OnUnloaded;
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Force save layout — pokud byl debouncer ještě běžel, dokončit teď.
            if (_saveDebouncer is { IsEnabled: true })
            {
                _saveDebouncer.Stop();
                await SaveLayoutAsync();
            }
            UnhookColumnEvents();  // cleanup DPD subscriptions
        }

        // ─── Multi-select API ─────────────────────────────────────────────────────

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

        /// <summary>User klikl na CheckBox v řádku — toggleval CheckBox.IsChecked. Propaguj do EDListRow.</summary>
        private void OnRowCheckClick(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is EDListRow row)
                row.IsChecked = cb.IsChecked == true;
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

        // ─── Veřejné API ──────────────────────────────────────────────────────────

        public void InitializeList()
        {
            EnsureConfigured();
            var output = MLRest!.QueryOrThrow(CacheClass, "InitializeList", new CVar());
            _columns = ParseColumns(output);
            RebuildColumns();
        }

        public void LoadList() => LoadList(filter: null);

        /// <summary>
        /// Načte data z Caché s volitelným filtrem. Filtr se pošle do Caché jako
        /// pojmenované IN klíče: <c>filter["agendaCode"] = "MHD"</c> → <c>^||ML("IN","agendaCode") = "MHD"</c>.
        /// Server může (ale nemusí) filtr aplikovat — strana .NET předá co dostane.
        /// </summary>
        public void LoadList(IReadOnlyDictionary<string, string>? filter)
        {
            EnsureConfigured();
            var input = new CVar();
            if (filter is not null)
            {
                foreach (var kv in filter)
                    input[kv.Key] = new CVar(kv.Value);
            }
            var output = MLRest!.QueryOrThrow(CacheClass, "LoadList", input);
            // Unsubscribe stará data — žádné spying na zaniklé řádky
            foreach (var oldRow in _rows) oldRow.PropertyChanged -= OnRowPropertyChanged;
            _rows.Clear();
            foreach (var row in ParseRows(output))
            {
                row.PropertyChanged += OnRowPropertyChanged;
                _rows.Add(row);
            }
            FireCheckedRowsChanged();
            RefreshFilter();  // re-apply current search po LoadList (nový dataset)
            // Aplikuj persistent column layout asynchronně po data load — minor flicker (default
            // sloupce → persistované), ale UI neblokuje.
            _ = LoadLayoutAsync();
        }

        // ─── Parsování ────────────────────────────────────────────────────────────

        internal static IReadOnlyList<ListColumn> ParseColumns(CVar output)
        {
            var json = output["1"]?.Value?.ToString();
            if (string.IsNullOrEmpty(json)) return [];
            using var doc = JsonDocument.Parse(json);
            return [.. doc.RootElement.EnumerateArray().Select(ParseColumn)];
        }

        private static ListColumn ParseColumn(JsonElement el) => new()
        {
            Key      = el.Str("key"),
            Label    = el.Str("label"),
            Type     = ParseType(el.Str("type")),
            Format   = el.Str("format"),
            Width    = el.Int("width"),
            Align    = ParseAlign(el.Str("align")),
            Sortable = el.Bool("sortable"),
            Visible  = el.Bool("visible", defaultValue: true),
            IsLink   = el.Bool("isLink"),
        };

        private static EListColumnType ParseType(string? s) => s?.ToUpperInvariant() switch
        {
            "NUMBER"   => EListColumnType.Number,
            "DATE"     => EListColumnType.Date,
            "DATETIME" => EListColumnType.DateTime,
            "BOOL"     => EListColumnType.Bool,
            "IMAGE"    => EListColumnType.Image,
            "COLOR"    => EListColumnType.Color,
            "HHMM"     => EListColumnType.HoursMinutes,
            "CODEBOOK" => EListColumnType.Codebook,
            _          => EListColumnType.Text,
        };

        private static EListColumnAlign ParseAlign(string? s) => s?.ToUpperInvariant() switch
        {
            "LEFT"   => EListColumnAlign.Left,
            "CENTER" => EListColumnAlign.Center,
            "RIGHT"  => EListColumnAlign.Right,
            _        => EListColumnAlign.Auto,
        };

        private IEnumerable<EDListRow> ParseRows(CVar output)
        {
            foreach (KeyValuePair<string, CVar> kvp in output)
            {
                if (kvp.Key == "ERR") continue;
                var json = kvp.Value?.Value?.ToString();
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, string?>();
                string? id = null;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var formatted = FormatValue(prop.Value.GetString(), prop.Name);
                    if (prop.Name == "id") id = formatted;
                    else dict[prop.Name] = formatted;
                }
                if (!string.IsNullOrEmpty(id))
                    yield return new EDListRow(id, dict);
            }
        }

        // ─── Formátování hodnot ───────────────────────────────────────────────────

        private string? FormatValue(string? raw, string colKey)
        {
            if (raw is null) return null;
            var col = _columns?.FirstOrDefault(c => c.Key == colKey);
            if (col is null) return raw;

            return col.Type switch
            {
                EListColumnType.Bool         => raw == "1" ? "Ano" : "Ne",
                EListColumnType.HoursMinutes => FormatHHMM(raw),
                EListColumnType.Date         => FormatDate(raw, col.Format.Length > 0 ? col.Format : "dd.MM.yyyy"),
                EListColumnType.DateTime     => FormatDate(raw, col.Format.Length > 0 ? col.Format : "dd.MM.yyyy HH:mm"),
                EListColumnType.Number       => FormatNumber(raw, col.Format),
                _                            => raw,
            };
        }

        internal static string? FormatHHMM(string? raw)
        {
            if (!int.TryParse(raw, out int val)) return raw;
            return $"{val / 100:D2}:{val % 100:D2}";
        }

        internal static string? FormatDate(string? raw, string format)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt.ToString(format, CultureInfo.CurrentCulture)
                : raw;
        }

        internal static string? FormatNumber(string? raw, string format)
        {
            if (string.IsNullOrEmpty(format)) return raw;
            return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)
                ? d.ToString(format, CultureInfo.CurrentCulture)
                : raw;
        }

        // ─── Generování sloupců DataGridu ─────────────────────────────────────────

        private void RebuildColumns()
        {
            UnhookColumnEvents();
            dataGrid.Columns.Clear();
            if (_selectionMode == EListSelectionMode.Check)
                dataGrid.Columns.Add(BuildCheckBoxColumn());
            if (_columns is null) return;
            foreach (var col in _columns.Where(c => c.Visible))
            {
                var dgCol = BuildDataGridColumn(col);
                dgCol.SetValue(FrameworkElement.TagProperty, col.Key);  // mapping zpět při SnapshotLayout
                dataGrid.Columns.Add(dgCol);
            }
            HookColumnEvents();
        }

        private DataGridColumn BuildCheckBoxColumn()
        {
            var checkBox = new FrameworkElementFactory(typeof(CheckBox));
            checkBox.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBox.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            // OneWay binding: source (EDListRow.IsChecked) → CheckBox (programmatic změny propagují
            // do UI přes INotifyPropertyChanged). User click řeší explicit ClickEvent handler níže —
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

            var align = col.Align == EListColumnAlign.Auto ? DefaultAlign(col.Type) : col.Align;

            if (col.IsLink)
            {
                var tb = new FrameworkElementFactory(typeof(TextBlock));
                tb.SetBinding(TextBlock.TextProperty, new Binding($"[{col.Key}]"));
                tb.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
                tb.SetValue(TextBlock.CursorProperty, Cursors.Hand);
                tb.SetValue(TextBlock.ForegroundProperty, SystemColors.HotTrackBrush);
                if (align == EListColumnAlign.Right)
                    tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right);
                else if (align == EListColumnAlign.Center)
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

            if (col.Type == EListColumnType.Color)
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

            if (align != EListColumnAlign.Left)
            {
                var ha = align == EListColumnAlign.Right
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center;
                textCol.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = { new Setter(TextBlock.HorizontalAlignmentProperty, ha) }
                };
            }

            return textCol;
        }

        private static EListColumnAlign DefaultAlign(EListColumnType type) => type switch
        {
            EListColumnType.Number       => EListColumnAlign.Right,
            EListColumnType.HoursMinutes => EListColumnAlign.Right,
            EListColumnType.Bool         => EListColumnAlign.Center,
            EListColumnType.Image        => EListColumnAlign.Center,
            EListColumnType.Color        => EListColumnAlign.Center,
            _                            => EListColumnAlign.Left,
        };

        // ─── Obsluha eventů DataGridu ─────────────────────────────────────────────

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedRowId is string id)
                RowDoubleClicked?.Invoke(this, id);
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, SelectedRowId);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private void EnsureConfigured()
        {
            if (MLRest is null || string.IsNullOrEmpty(CacheClass))
                throw new InvalidOperationException("EDList: MLRest a CacheClass musí být nastaveny před použitím.");
        }

        // ─── Column layout persist (L2) ──────────────────────────────────────────

        private DispatcherTimer? _saveDebouncer;
        private bool _suppressLayoutSave;  // true během ApplyLayout — eventy nesmí trigger save

        /// <summary>Subscribe na width/displayIndex/sort změny sloupců — trigger debounced save.</summary>
        private void HookColumnEvents()
        {
            foreach (var col in dataGrid.Columns)
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
            foreach (var col in dataGrid.Columns)
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
            foreach (var col in dataGrid.Columns)
            {
                if (col.GetValue(FrameworkElement.TagProperty) is not string key) continue;
                columns.Add(new ColumnPref
                {
                    Key = key,
                    Width = col.ActualWidth,
                    DisplayIndex = col.DisplayIndex,
                });
            }
            var sort = dataGrid.Columns
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
                // 1) Šířky — apply v current pořadí
                foreach (var pref in layout.Columns)
                {
                    var col = FindColumnByKey(pref.Key);
                    if (col is null) continue;
                    if (pref.Width > 0) col.Width = new DataGridLength(pref.Width);
                }
                // 2) DisplayIndex — apply v cílovém pořadí (low → high), DataGrid přesype zbytek
                foreach (var pref in layout.Columns.OrderBy(p => p.DisplayIndex))
                {
                    var col = FindColumnByKey(pref.Key);
                    if (col is null) continue;
                    if (pref.DisplayIndex >= 0 && pref.DisplayIndex < dataGrid.Columns.Count)
                        col.DisplayIndex = pref.DisplayIndex;
                }
                // 3) Sort — visual indicator + ICollectionView sort descriptions
                if (layout.Sort is { Count: > 0 })
                {
                    var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                    view?.SortDescriptions.Clear();
                    foreach (var sort in layout.Sort)
                    {
                        var col = FindColumnByKey(sort.Key);
                        if (col is null) continue;
                        var dir = sort.Direction == "desc" ? ListSortDirection.Descending : ListSortDirection.Ascending;
                        col.SortDirection = dir;
                        view?.SortDescriptions.Add(new SortDescription($"[{sort.Key}]", dir));
                    }
                }
            }
            finally { _suppressLayoutSave = false; }
        }

        private DataGridColumn? FindColumnByKey(string key) =>
            dataGrid.Columns.FirstOrDefault(c => c.GetValue(FrameworkElement.TagProperty) as string == key);
    }

    // ─── Extension metody pro JsonElement ─────────────────────────────────────────

    internal static class JsonElementExtensions
    {
        public static string Str(this JsonElement el, string key) =>
            el.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";

        public static int Int(this JsonElement el, string key) =>
            el.TryGetProperty(key, out var p) && p.TryGetInt32(out int v) ? v : 0;

        public static bool Bool(this JsonElement el, string key, bool defaultValue = false) =>
            el.TryGetProperty(key, out var p)
                ? p.ValueKind switch
                {
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Number => p.GetInt32() != 0,
                    _                    => defaultValue,
                }
                : defaultValue;
    }

    // ─── Converter: "R;G;B" → SolidColorBrush ────────────────────────────────────

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
