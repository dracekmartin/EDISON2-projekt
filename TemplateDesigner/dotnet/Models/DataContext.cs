using System.Globalization;
using System.Text.RegularExpressions;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Models;

/// <summary>
/// Přenáší data do elementů při renderování (print engine) nebo preview (mock data).
/// V designeru je null — elementy renderují samy sebe bez dat.
/// </summary>
public class DataContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DataContext>> _rows;

    public CultureInfo Culture { get; }

    public DataContext(
        IReadOnlyDictionary<string, object?>? values = null,
        IReadOnlyDictionary<string, IReadOnlyList<DataContext>>? rows = null,
        CultureInfo? culture = null)
    {
        _values = values ?? new Dictionary<string, object?>();
        _rows = rows ?? new Dictionary<string, IReadOnlyList<DataContext>>();
        Culture = culture ?? CultureInfo.CurrentCulture;
    }

    /// <summary>Vrátí hodnotu pole, nebo null pokud neexistuje.</summary>
    public object? GetValue(string key) =>
        _values.TryGetValue(key, out var v) ? v : null;

    /// <summary>Vrátí hodnotu pole jako string, nebo fallback.</summary>
    public string GetString(string key, string fallback = "") =>
        GetValue(key)?.ToString() ?? fallback;

    /// <summary>Vrátí řádky tabulkového zdroje, nebo prázdný seznam.</summary>
    public IReadOnlyList<DataContext> GetRows(string dataSource) =>
        _rows.TryGetValue(dataSource, out var rows) ? rows : [];

    /// <summary>
    /// Nahradí placeholdery ve tvaru {NAZEV_POLE} nebo {NAZEV_POLE:format} hodnotami z kontextu.
    /// Příklad: "Řidič: {JMENO}" → "Řidič: Jan Novák"
    /// Příklad: "Cena: {CENA:N2}" → "Cena: 1 234,50"
    /// </summary>
    public string ResolvePlaceholders(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return Regex.Replace(template, @"\{(\w+)(?::([^}]+))?\}", m =>
        {
            var field = m.Groups[1].Value;
            var value = GetString(field, m.Value);
            if (value == m.Value) return value; // pole nenalezeno — vrátit placeholder
            var format = m.Groups[2].Success ? m.Groups[2].Value : null;
            return PlaceholderFormatter.Format(value, format, Culture);
        });
    }

    /// <summary>
    /// Naformátuje hodnotu podle formátovací instrukce.
    /// Pokud format je null/prázdný, vrátí hodnotu beze změny.
    /// </summary>
    public static string FormatValue(string value, string? format, CultureInfo? culture = null) =>
        PlaceholderFormatter.Format(value, format, culture);

    /// <summary>
    /// Vrátí nový DataContext s hodnotami z tohoto plus extra hodnoty.
    /// Extra hodnoty přepíší existující klíče.
    /// </summary>
    public DataContext WithExtraValues(IReadOnlyDictionary<string, object?> extra)
    {
        var merged = new Dictionary<string, object?>(_values, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in extra)
            merged[k] = v;
        return new DataContext(merged, _rows, Culture);
    }

    /// <summary>
    /// Vrátí nový DataContext se stejnými daty, ale jinou kulturou.
    /// Kultura se propaguje rekurzivně do všech řádkových DataContextů.
    /// </summary>
    public DataContext WithCulture(CultureInfo culture)
    {
        if (_rows.Count == 0) return new(_values, _rows, culture);
        var newRows = _rows.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<DataContext>)kvp.Value.Select(r => r.WithCulture(culture)).ToList(),
            StringComparer.OrdinalIgnoreCase);
        return new DataContext(_values, newRows, culture);
    }

    public static DataContext Empty { get; } = new();
}
