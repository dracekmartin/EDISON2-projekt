using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace EDControls.EDList;

/// <summary>
/// Typové řazení EDList — sort klíče se staví z raw hodnot (server formát), ne z formátovaných
/// stringů, takže NUMBER/HHMM řadí numericky a DATE/DATETIME chronologicky.
/// </summary>
internal static class EDListSort
{
    /// <summary>
    /// Typovaný sort klíč buňky. Neparsovatelná hodnota spadne na formátovaný string —
    /// comparer pak mezi nestejnými typy porovnává stringově, takže se nic nerozbije.
    /// </summary>
    internal static object? BuildSortKey(string? raw, EListColumnType type, string? formatted)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        return type switch
        {
            EListColumnType.Number or EListColumnType.HoursMinutes =>
                decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? d : formatted,
            EListColumnType.Date or EListColumnType.DateTime =>
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    ? dt : formatted,
            EListColumnType.Bool => raw == "1",
            _ => formatted,
        };
    }
}

/// <summary>
/// Komparátor pro <c>ListCollectionView.CustomSort</c> — řadí podle sekvence (klíč, směr).
/// Prázdné hodnoty jdou na konec (asc); nestejné typy klíčů se porovnají stringově.
/// </summary>
internal sealed class EDListRowComparer(IReadOnlyList<(string Key, bool Descending)> specs)
    : IComparer, IComparer<EDListRow>
{
    public int Compare(object? x, object? y) => Compare(x as EDListRow, y as EDListRow);

    public int Compare(EDListRow? x, EDListRow? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;
        foreach (var (key, descending) in specs)
        {
            int cmp = CompareCell(x.GetSortKey(key), y.GetSortKey(key));
            if (cmp != 0) return descending ? -cmp : cmp;
        }
        return 0;
    }

    private static int CompareCell(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a is null) return 1;
        if (b is null) return -1;
        if (a.GetType() == b.GetType() && a is IComparable ca) return ca.CompareTo(b);
        return string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCultureIgnoreCase);
    }
}
