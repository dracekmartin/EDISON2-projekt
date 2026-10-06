namespace EDControls;

/// <summary>
/// Výstup legacy Caché endpointů s numerickými subscripty (<c>object[]</c> wire): index 0 je ERR slot,
/// data od indexu 1, typicky řádky <c>klíč{del}hodnota{del}...</c>. Jediné místo produkčního kódu, které tu konvenci zná.
/// </summary>
public static class LegacyOutput
{
    /// <summary>Datové hodnoty od indexu 1 tak, jak přišly (null jako prázdný řetězec).</summary>
    public static IEnumerable<string> Values(object?[]? output)
    {
        if (output is null) yield break;
        for (int i = 1; i < output.Length; i++)
            yield return output[i]?.ToString() ?? "";
    }

    /// <summary>Neprázdné datové řádky rozdělené oddělovačem.</summary>
    public static IEnumerable<string[]> Rows(object?[]? output, char delimiter) =>
        Values(output).Where(v => v.Length > 0).Select(v => v.Split(delimiter));

    /// <summary>Hodnota za klíčem z prvního řádku <c>klíč{del}hodnota</c>; null = řádek chybí.</summary>
    public static string? Named(object?[]? output, char delimiter, string key) =>
        Rows(output, delimiter).FirstOrDefault(r => r.Length > 1 && r[0] == key)?[1];
}
