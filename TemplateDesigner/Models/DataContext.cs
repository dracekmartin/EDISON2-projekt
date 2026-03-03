using System.Text.RegularExpressions;

namespace TemplateDesigner.Models;

/// <summary>
/// Přenáší data do elementů při renderování (print engine) nebo preview (mock data).
/// V designeru je null — elementy renderují samy sebe bez dat.
/// </summary>
public class DataContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<DataContext>> _rows;

    public DataContext(
        IReadOnlyDictionary<string, object?>? values = null,
        IReadOnlyDictionary<string, IReadOnlyList<DataContext>>? rows = null)
    {
        _values = values ?? new Dictionary<string, object?>();
        _rows = rows ?? new Dictionary<string, IReadOnlyList<DataContext>>();
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
    /// Nahradí placeholdery ve tvaru {NAZEV_POLE} hodnotami z kontextu.
    /// Příklad: "Řidič: {JMENO}" → "Řidič: Jan Novák"
    /// </summary>
    public string ResolvePlaceholders(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return Regex.Replace(template, @"\{(\w+)\}", m => GetString(m.Groups[1].Value, m.Value));
    }

    public static DataContext Empty { get; } = new();
}
