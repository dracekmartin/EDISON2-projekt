using System.Linq;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// R2 — svislé stránkování řádků. Trigger (nezávisle na výšce sekce): dynamická NEBO pevně-vyšší-než-
/// stránka DynamicTable na vrchu sekce, jejíž ŘÁDKOVÝ obsah přesáhne stránku → láme po CELÝCH řádcích
/// + opakuje záhlaví. Malá tabulka (obsah &lt; stránka) → pixel-ořez (zachová „sekce vyšší než stránka").
/// A4Portrait = 297 mm tisková plocha (okraje 0). RowHeight 6 + HeaderHeight 8.
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

    private static DynamicTableElement Table(float? height = null) => new()
    {
        DataSource = "POLOZKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f, Height = height,
        Columns = [new TableColumn { Header = "A", Field = "X" }]
    };

    private static Section SplitSection(float? designerHeight, DynamicTableElement table) => new()
    {
        Name = "Obsah",
        PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
        DesignerHeight = designerHeight,
        Elements = [table]
    };

    private static void AssertRowPaginated(LayoutResult result, int expectedUnits)
    {
        Assert.True(result.TotalPages >= 2, $"čekány ≥2 stránky, dostal {result.TotalPages}");
        int expectedNext = 0;
        foreach (var page in result.Pages)
        {
            var placed = Assert.Single(page.Sections);
            Assert.True(placed.StartRowUnit.HasValue, "chybí StartRowUnit (nebyla row-paginace)");
            Assert.True(placed.RepeatHeader, "chybí RepeatHeader");
            Assert.Equal(expectedNext, placed.StartRowUnit!.Value);
            Assert.True(placed.EndRowUnit!.Value > placed.StartRowUnit.Value, "prázdné okno");
            Assert.True(placed.Height <= 297f + 0.01f, $"chunk {placed.Height} přesahuje stránku");
            expectedNext = placed.EndRowUnit.Value;
        }
        Assert.Equal(expectedUnits, expectedNext);
    }

    [Fact]
    public void DynamickaTabulka_ObsahPresahujeStranku_R2()
    {
        // 50 řádků → obsah 8+50*6=308 > 297 → R2. (Height=null, sekce dynamická.)
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [SplitSection(null, Table())] };
        var result = new PageLayoutEngine(new FixedHeightCalculator(320f)).Layout(tpl, Data(50));
        AssertRowPaginated(result, 50);
    }

    [Fact]
    public void PevnaTabulkaVyssiNezStranka_R2()
    {
        // Uživatelův případ: tabulka roztažená na pevnou výšku 500 mm (> stránka), obsah 308 > 297 → R2
        // (dřív blokováno podmínkou Height=null → padalo na pixel-split bez opakování záhlaví).
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [SplitSection(null, Table(height: 500f))] };
        var result = new PageLayoutEngine(new FixedHeightCalculator(320f)).Layout(tpl, Data(50));
        AssertRowPaginated(result, 50);
    }

    [Fact]
    public void MalaTabulka_VysokaSekce_PixelOrez_NeR2()
    {
        // 5 řádků → obsah 8+5*6=38 < 297 → R2 se NEspustí; vysoká 400mm sekce → pixel-ořez → 2 stránky.
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [SplitSection(400f, Table())] };
        var result = new PageLayoutEngine(new FixedHeightCalculator(38f)).Layout(tpl, Data(5));

        Assert.True(result.TotalPages >= 2);
        foreach (var page in result.Pages)
        {
            var placed = Assert.Single(page.Sections);
            Assert.False(placed.StartRowUnit.HasValue, "nemělo se použít R2 (obsah tabulky se vejde na stránku)");
            Assert.True(placed.ClipTop.HasValue, "čekán pixel-ořez (ClipTop)");
        }
    }
}
