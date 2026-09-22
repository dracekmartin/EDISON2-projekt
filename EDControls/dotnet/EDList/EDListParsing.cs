using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ALWA.SYS;

namespace EDControls.EDList
{
    /// <summary>
    /// Wire vrstva EDListu (JSON přes <c>^||ML</c>, kontrakt v <c>docs/wire-formaty.md</c>) -
    /// statická a bez WPF, aby šla testovat bez konstrukce controlu.
    /// </summary>
    internal static class EDListParsing
    {
        internal static CVar BuildLoadInput(IReadOnlyDictionary<string, string>? filter)
        {
            var input = new CVar();
            if (filter is not null)
            {
                foreach (var kv in filter)
                    input["filter"][kv.Key] = new CVar(kv.Value);
            }
            return input;
        }

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

        private static ListColumnType ParseType(string? s) => s?.ToUpperInvariant() switch
        {
            "NUMBER"   => ListColumnType.Number,
            "DATE"     => ListColumnType.Date,
            "DATETIME" => ListColumnType.DateTime,
            "BOOL"     => ListColumnType.Bool,
            "IMAGE"    => ListColumnType.Image,
            "COLOR"    => ListColumnType.Color,
            "HHMM"     => ListColumnType.HoursMinutes,
            "CODEBOOK" => ListColumnType.Codebook,
            _          => ListColumnType.Text,
        };

        private static ListColumnAlign ParseAlign(string? s) => s?.ToUpperInvariant() switch
        {
            "LEFT"   => ListColumnAlign.Left,
            "CENTER" => ListColumnAlign.Center,
            "RIGHT"  => ListColumnAlign.Right,
            _        => ListColumnAlign.Auto,
        };

        internal static IEnumerable<EDListRow> ParseRows(CVar output, IReadOnlyList<ListColumn>? columns)
        {
            foreach (KeyValuePair<string, CVar> kvp in output)
            {
                if (kvp.Key == "ERR") continue;
                var json = kvp.Value?.Value?.ToString();
                if (string.IsNullOrWhiteSpace(json)) continue;

                using var doc = JsonDocument.Parse(json);
                var raw = new Dictionary<string, string?>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    raw[prop.Name] = prop.Value.GetString();

                if (BuildRow(raw, columns) is { } row) yield return row;
            }
        }

        /// <summary>
        /// Postaví řádek z raw (serverových) hodnot - naformátuje je podle sloupce a zvlášť si drží
        /// typové sort klíče. Sdílí ho JSON cesta i režim „data zvenčí" (<c>EDList.SetData</c>), aby
        /// formátování a řazení žilo na jednom místě. Bez neprázdného <c>id</c> vrací null.
        /// </summary>
        internal static EDListRow? BuildRow(IReadOnlyDictionary<string, string?> raw, IReadOnlyList<ListColumn>? columns)
        {
            var dict = new Dictionary<string, string?>();
            var sortKeys = new Dictionary<string, object?>();
            string? id = null;
            foreach (var kv in raw)
            {
                var col = columns?.FirstOrDefault(c => c.Key == kv.Key);
                var formatted = FormatValue(kv.Value, col);
                if (kv.Key == "id") id = formatted;
                else
                {
                    dict[kv.Key] = formatted;
                    sortKeys[kv.Key] = EDListSort.BuildSortKey(kv.Value, col?.Type ?? ListColumnType.Text, formatted);
                }
            }
            return string.IsNullOrEmpty(id) ? null : new EDListRow(id, dict, sortKeys);
        }

        internal static string? FormatValue(string? raw, ListColumn? col)
        {
            if (raw is null) return null;
            if (col is null) return raw;

            return col.Type switch
            {
                ListColumnType.Bool         => raw == "1" ? "Ano" : "Ne",
                ListColumnType.HoursMinutes => FormatHHMM(raw),
                ListColumnType.Date         => FormatDate(raw, col.Format.Length > 0 ? col.Format : "dd.MM.yyyy"),
                ListColumnType.DateTime     => FormatDate(raw, col.Format.Length > 0 ? col.Format : "dd.MM.yyyy HH:mm"),
                ListColumnType.Number       => FormatNumber(raw, col.Format),
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

        internal static ListColumnAlign DefaultAlign(ListColumnType type) => type switch
        {
            ListColumnType.Number       => ListColumnAlign.Right,
            ListColumnType.HoursMinutes => ListColumnAlign.Right,
            ListColumnType.Bool         => ListColumnAlign.Center,
            ListColumnType.Image        => ListColumnAlign.Center,
            ListColumnType.Color        => ListColumnAlign.Center,
            _                            => ListColumnAlign.Left,
        };
    }

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
}
