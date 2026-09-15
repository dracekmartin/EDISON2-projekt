using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EDControls.EDList;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Překlad legacy tabulky na vstup <c>EDList.SetData</c>. Legacy deskriptor typů nese formátování,
/// zarovnání i řazení - v EDListu to obstará <see cref="ListColumn.Type"/> + <c>Format</c>, takže
/// se tady nic z toho nepočítá podruhé.
/// </summary>
public static class DashboardListMapper
{
    /// <summary>Skrytý sloupec s barvou textu řádku - nerenderuje se ani nehledá, ale projde přes indexer řádku do CellStyle.</summary>
    public const string ForeColorKey = "_fore";

    public static IReadOnlyList<ListColumn> ToColumns(DashboardTable table)
    {
        var columns = table.Columns.Select((col, i) => new ListColumn
        {
            Key = ColumnKey(i),
            Label = col.Name,
            // D zůstává TEXT: wire posílá datum už naformátované (dd.MM.yyyy) a EDList ho parsuje
            // jen InvariantCulture, takže by propadlo na raw. Legacy ho taky řadil stringově.
            Type = col.Type == 'N' ? ListColumnType.Number : ListColumnType.Text,
            Format = col.Format,
            Sortable = true,
        }).ToList();
        columns.Add(new ListColumn { Key = ForeColorKey, Visible = false });
        return columns;
    }

    public static IReadOnlyList<IReadOnlyDictionary<string, string?>> ToRows(DashboardTable table) =>
        [.. table.Rows.Select((row, i) =>
        {
            // EDList zahodí řádek bez neprázdného id; legacy data žádné nenesou, takže pořadí.
            var values = new Dictionary<string, string?>
            {
                ["id"] = (i + 1).ToString(CultureInfo.InvariantCulture),
                [ForeColorKey] = row.ForeColorHtml,
            };
            for (int c = 0; c < row.Cells.Count; c++) values[ColumnKey(c)] = row.Cells[c];
            return (IReadOnlyDictionary<string, string?>)values;
        })];

    private static string ColumnKey(int index) => "c" + index.ToString(CultureInfo.InvariantCulture);
}
