using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TemplateDesigner.Models;

/// <summary>
/// Řazení datových řádků dynamické tabulky podle hodnot polí.
/// <para>
/// **Typ řazení se neptáme uživatele, odvozuje se z dat** (rozhodnutí Jan 2026-08-04): volba
/// „abecedně × číselně" vypadá jako služba, ale špatná volba vyrobí výstup, který vypadá seřazeně
/// a přitom je v nesmyslném pořadí - a nikdo netuší, že textově je „12" před „9". Prvek navíc
/// schéma agendy při kreslení nevidí (<see cref="RenderContext"/> nese jen data), takže typ stejně
/// musí vyjít z hodnot.
/// </para>
/// Pravidla: čísla &gt; datum/čas &gt; text (česká abeceda, bez ohledu na velikost písmen);
/// prázdné hodnoty vždy na konci (v obou směrech); řazení je **stabilní**, takže při shodě klíčů
/// zůstane pořadí z dat.
/// </summary>
internal static class RowSorter
{
    /// <summary>Řadí podle polí v pořadí priority. Prázdný seznam klíčů = beze změny.</summary>
    internal static IReadOnlyList<DataContext> Sort(
        IReadOnlyList<DataContext> rows, CultureInfo? culture, params (string? Field, bool Descending)[] keys)
    {
        var active = keys.Where(k => !string.IsNullOrWhiteSpace(k.Field)).ToList();
        if (active.Count == 0 || rows.Count < 2) return rows;

        var ordered = rows.Select((row, index) => (row, index));

        // OrderBy je v .NET stabilní, ale index v klíči drží pořadí i po ThenBy řetězu.
        IOrderedEnumerable<(DataContext row, int index)>? sorted = null;
        foreach (var (field, desc) in active)
        {
            var kind = InferKind(rows, field!, culture);
            var comparer = new KeyComparer(kind, culture);
            Func<(DataContext row, int index), string> key = t => t.row.GetString(field!);

            // Prázdné dozadu musí být VLASTNÍ úroveň řazení, ne návratová hodnota porovnání -
            // sestupné řazení obrací komparátor, takže by prázdné vyplavaly nahoru.
            Func<(DataContext row, int index), bool> empty = t => string.IsNullOrWhiteSpace(key(t));
            sorted = sorted is null ? ordered.OrderBy(empty) : sorted.ThenBy(empty);

            sorted = desc ? sorted.ThenByDescending(key, comparer) : sorted.ThenBy(key, comparer);
        }

        return sorted!.ThenBy(t => t.index).Select(t => t.row).ToList();
    }

    private enum Kind { Text, Number, Date }

    /// <summary>Typ podle skutečných hodnot: číslo, jen když jsou čísla VŠECHNY neprázdné (jinak by
    /// se smíšený sloupec řadil podle toho, co se náhodou naparsovalo).</summary>
    private static Kind InferKind(IReadOnlyList<DataContext> rows, string field, CultureInfo? culture)
    {
        bool any = false, allNum = true, allDate = true;
        foreach (var r in rows)
        {
            string v = r.GetString(field);
            if (string.IsNullOrWhiteSpace(v)) continue;
            any = true;
            if (allNum && !TryNumber(v, culture, out _)) allNum = false;
            if (allDate && !TryDate(v, culture, out _)) allDate = false;
            if (!allNum && !allDate) break;
        }
        if (!any) return Kind.Text;
        return allNum ? Kind.Number : allDate ? Kind.Date : Kind.Text;
    }

    private static bool TryNumber(string v, CultureInfo? culture, out double d) =>
        double.TryParse(v, NumberStyles.Any, culture ?? CultureInfo.InvariantCulture, out d)
        || double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d);

    private static bool TryDate(string v, CultureInfo? culture, out DateTime dt) =>
        DateTime.TryParse(v, culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
        || DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);

    /// <summary>Porovnání hodnot jedné úrovně. Prázdné sem už nechodí jako rozlišující případ -
    /// odděluje je vlastní úroveň řazení výše (viz komentář v <see cref="Sort"/>).</summary>
    private sealed class KeyComparer(Kind kind, CultureInfo? culture) : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y)) return 0;

            switch (kind)
            {
                case Kind.Number when TryNumber(x!, culture, out var nx) && TryNumber(y!, culture, out var ny):
                    return nx.CompareTo(ny);
                case Kind.Date when TryDate(x!, culture, out var dx) && TryDate(y!, culture, out var dy):
                    return dx.CompareTo(dy);
                default:
                    return string.Compare(x, y, culture ?? CultureInfo.CurrentCulture,
                        CompareOptions.IgnoreCase);
            }
        }
    }
}
