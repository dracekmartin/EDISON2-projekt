using System.Linq;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// R2 přes REÁLNÝ SkiaHeightCalculator (ne test double) — ověřuje, že dlouhá dynamická DynamicTable
/// se skutečnou výškou z dat spustí řádkové stránkování. Doplňuje RowPaginationTest (FixedHeightCalculator).
/// </summary>
public class RowPaginationRealCalcTest
{
    [Fact]
    public void RealCalc_DynamickaTabulka85Radku_StrankujePoRadcich()
    {
        var table = new DynamicTableElement
        {
            DataSource = "POLOZKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f,
            Columns = [new TableColumn { Header = "A", Field = "X" }]
            // Height = null → dynamická
        };
        var section = new Section
        {
            Name = "Obsah", PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            Elements = [table]
        };
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [section] };  // 297 mm
        var rows = Enumerable.Range(0, 85)
            .Select(i => new DataContext(new Dictionary<string, object?> { ["X"] = i })).ToList();
        var data = new DataContext(rows: new Dictionary<string, IReadOnlyList<DataContext>> { ["POLOZKY"] = rows });

        // 8 + 85*6 = 518 mm > 297 → engine musí použít řádkové stránkování (StartRowUnit, ne ClipTop).
        var result = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(tpl, data);

        Assert.True(result.TotalPages >= 2);
        int next = 0;
        foreach (var page in result.Pages)
        {
            var placed = Assert.Single(page.Sections);
            Assert.True(placed.StartRowUnit.HasValue, "reálná cesta nespustila row-paginaci");
            Assert.Null(placed.ClipTop);
            Assert.Equal(next, placed.StartRowUnit!.Value);
            next = placed.EndRowUnit!.Value;
        }
        Assert.Equal(85, next);
    }
}
