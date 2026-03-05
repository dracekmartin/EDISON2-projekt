using ALWA.SYS;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EDControls.EDList
{
    public partial class EDList : UserControl
    {
        // ─── Konfigurace ──────────────────────────────────────────────────────────

        public MLRest? MLRest { get; set; }
        public string CacheClass { get; set; } = "";

        // ─── Stav ─────────────────────────────────────────────────────────────────

        private IReadOnlyList<ListColumn>? _columns;
        public IReadOnlyList<ListColumn>? Columns => _columns;

        private readonly ObservableCollection<Dictionary<string, string?>> _rows = new();

        public string? SelectedRowId =>
            dataGrid.SelectedItem is Dictionary<string, string?> row &&
            row.TryGetValue("id", out var id) ? id : null;

        // ─── Eventy ───────────────────────────────────────────────────────────────

        public event EventHandler<string>? RowDoubleClicked;
        public event EventHandler<string?>? SelectionChanged;
        public event EventHandler<(string RowId, string ColKey)>? CellLinkClicked;

        // ─── Inicializace ─────────────────────────────────────────────────────────

        public EDList()
        {
            InitializeComponent();
            dataGrid.ItemsSource = _rows;
        }

        // ─── Veřejné API ──────────────────────────────────────────────────────────

        public void InitializeList()
        {
            EnsureConfigured();
            var output = MLRest!.Query(CacheClass, "InitializeList", Array.Empty<object>());
            MLRest.RestStatus.ThrowIfError();
            _columns = ParseColumns(output);
            RebuildColumns();
        }

        public void LoadList()
        {
            EnsureConfigured();
            var output = MLRest!.Query(CacheClass, "LoadList", Array.Empty<object>());
            MLRest.RestStatus.ThrowIfError();
            _rows.Clear();
            foreach (var row in ParseRows(output))
                _rows.Add(row);
        }

        // ─── Parsování ────────────────────────────────────────────────────────────

        private static IReadOnlyList<ListColumn> ParseColumns(object[] output)
        {
            if (output.Length < 2) return [];
            var json = output[1]?.ToString() ?? "[]";
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

        private IEnumerable<Dictionary<string, string?>> ParseRows(object[] output)
        {
            for (int i = 1; i < output.Length; i++)
            {
                var json = output[i]?.ToString();
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var dict = new Dictionary<string, string?>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    dict[prop.Name] = FormatValue(prop.Value.GetString(), prop.Name);
                yield return dict;
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

        private static string? FormatHHMM(string? raw)
        {
            if (!int.TryParse(raw, out int val)) return raw;
            return $"{val / 100:D2}:{val % 100:D2}";
        }

        private static string? FormatDate(string? raw, string format)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt.ToString(format, CultureInfo.CurrentCulture)
                : raw;
        }

        private static string? FormatNumber(string? raw, string format)
        {
            if (string.IsNullOrEmpty(format)) return raw;
            return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double d)
                ? d.ToString(format, CultureInfo.CurrentCulture)
                : raw;
        }

        // ─── Generování sloupců DataGridu ─────────────────────────────────────────

        private void RebuildColumns()
        {
            dataGrid.Columns.Clear();
            if (_columns is null) return;
            foreach (var col in _columns.Where(c => c.Visible))
                dataGrid.Columns.Add(BuildDataGridColumn(col));
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
                    if (sender is TextBlock el &&
                        el.DataContext is Dictionary<string, string?> row &&
                        row.TryGetValue("id", out var rowId) && rowId is not null)
                        CellLinkClicked?.Invoke(this, (rowId, col.Key));
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
