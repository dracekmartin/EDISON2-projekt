namespace EDISON2.IntegrationTests;

/// <summary>Řádky legacy <c>object[]</c> protokolu: index 0 je chybový slot, data <c>klíč{del}hodnota...</c> od indexu 1.</summary>
internal static class WireRows
{
    public static Dictionary<string, string[]> ByKey(object[] output, char delimiter)
    {
        var rows = new Dictionary<string, string[]>();
        for (var i = 1; i < output.Length; i++)
        {
            var parts = (output[i]?.ToString() ?? "").Split(delimiter);
            rows.TryAdd(parts[0], parts);
        }
        return rows;
    }
}
