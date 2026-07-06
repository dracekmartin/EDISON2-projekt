using System.Linq;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// R2 — svislé stránkování řádků: dlouhá DynamicTable ve SplitAcrossPages sekci se láme po CELÝCH
/// řádcích (žádný uříznutý řádek) a záhlaví se opakuje na každé stránce (RepeatHeader).
/// </summary>
public class RowPaginationTest
{
    private static DataContext Data(int rowCount)
    {
        var rows = Enumerable.Range(0, rowCount)
            .Select(i => new DataContext(new Dictionary<string, object?> { ["X"] = i }))
            .ToList();
        return new DataContext(rows: new Dictionary<string, IReadOnlyList<DataContext>> { ["POLOZKY"] = rows });
    }

    [Fact]
    public void DlouhaTabulka_StrankujePoRadcich_OpakujeZahlavi()
    {
        var table = new DynamicTableElement
        {
            DataSource = "POLOZKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f,
            Columns = [new TableColumn { Header = "A", Field = "X" }]
        };
        var section = new Section
        {
            Name = "Obsah",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = 400,          // > 297 → sekce se nevejde → spustí řádkové stránkování
            Elements = [table]
        };
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [section] };  // 297 mm plocha
        var data = Data(50);

        var result = new PageLayoutEngine(new FixedHeightCalculator(null)).Layout(tpl, data);

        Assert.True(result.TotalPages >= 2, $"čekány ≥2 stránky, dostal {result.TotalPages}");

        // Všechny umístěné části mají řádkové okno + opakované záhlaví; jednotky souvislé 0..50.
        int expectedNext = 0;
        foreach (var page in result.Pages)
        {
            var placed = Assert.Single(page.Sections);
            Assert.True(placed.StartRowUnit.HasValue, "chybí StartRowUnit");
            Assert.True(placed.RepeatHeader, "chybí RepeatHeader");
            Assert.Equal(expectedNext, placed.StartRowUnit!.Value);   // navazuje bez díry/překryvu
            Assert.True(placed.EndRowUnit!.Value > placed.StartRowUnit.Value, "prázdné okno");
            // chunk se vejde na stránku (header 8 + řádky*6 ≤ 297)
            Assert.True(placed.Height <= 297f + 0.01f, $"chunk {placed.Height} přesahuje stránku");
            expectedNext = placed.EndRowUnit.Value;
        }
        Assert.Equal(50, expectedNext);   // pokryty všechny řádky, žádný navíc
    }
}
