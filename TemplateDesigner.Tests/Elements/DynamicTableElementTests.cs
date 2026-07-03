using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Čistá logika DynamicTableElementu (bez WPF/SkiaSharp): seed sloupců z polí kolekce,
/// výpočet přirozené výšky, Clone (deep), ApplyScale. Auto-šířka = Width 0 (rozdělí se za běhu).
/// </summary>
public class DynamicTableElementTests
{
    private static DataContext DataWithZastavky(int rowCount)
    {
        var rows = new List<DataContext>();
        for (int i = 0; i < rowCount; i++)
            rows.Add(new DataContext(new Dictionary<string, object?> { ["POPIS"] = $"Zastávka {i}" }));
        return new DataContext(rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["ZASTAVKY"] = rows
        });
    }

    // ── BuildColumnsFromFields ────────────────────────────────────────────

    [Fact]
    public void BuildColumnsFromFields_MapujePoleNaSloupceSAutoSirkou()
    {
        var fields = new List<DataField>
        {
            new() { FieldName = "POPIS", Label = "Název zastávky" },
            new() { FieldName = "TC",    Label = "Tarifní číslo" },
        };

        var cols = DynamicTableElement.BuildColumnsFromFields(fields);

        Assert.Equal(2, cols.Count);
        Assert.Equal("Název zastávky", cols[0].Header);
        Assert.Equal("POPIS", cols[0].Field);
        Assert.Equal(0f, cols[0].Width);                       // auto — rozdělí šířku za běhu
        Assert.Equal(ColumnWidthMode.Mm, cols[0].WidthMode);
        Assert.True(cols[0].Visible);
        Assert.Equal("TC", cols[1].Field);
    }

    // ── CalculateHeightFromData (přirozená výška) ─────────────────────────

    [Fact]
    public void CalculateHeightFromData_ZahlaviPlusRadkyPlusSouhrn()
    {
        var el = new DynamicTableElement
        {
            DataSource = "ZASTAVKY",
            ShowHeader = true,
            HeaderHeight = 8f,
            RowHeight = 6f,
            SummaryRows = [new TableRow()],   // 1 souhrnný řádek
        };

        // 4 datové řádky: 8 (záhlaví) + 4*6 (data) + 1*6 (souhrn) = 38
        Assert.Equal(38f, el.CalculateHeightFromData(DataWithZastavky(4)));
    }

    [Fact]
    public void CalculateHeightFromData_BezZahlavi_NepocitaHeaderHeight()
    {
        var el = new DynamicTableElement { DataSource = "ZASTAVKY", ShowHeader = false, HeaderHeight = 8f, RowHeight = 6f };
        Assert.Equal(12f, el.CalculateHeightFromData(DataWithZastavky(2)));   // 2*6, žádné záhlaví
    }

    [Fact]
    public void CalculateHeightFromData_BezDat_PouzijeNahledoveRadky()
    {
        var el = new DynamicTableElement { DataSource = "ZASTAVKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f };
        // Prázdná data → 3 náhledové řádky: 8 + 3*6 = 26
        Assert.Equal(26f, el.CalculateHeightFromData(new DataContext()));
    }

    // ── Clone (deep) ──────────────────────────────────────────────────────

    [Fact]
    public void Clone_JeHlubokaKopieSNovymId()
    {
        var el = new DynamicTableElement
        {
            DataSource = "ZASTAVKY",
            Columns = [new TableColumn { Header = "A", Field = "POPIS", Width = 20f }],
            HeaderStyle = new TextStyle { Bold = true },
        };

        var clone = (DynamicTableElement)el.Clone();

        Assert.NotEqual(el.Id, clone.Id);                       // CopyBaseTo dává nové Guid
        Assert.Equal("ZASTAVKY", clone.DataSource);
        Assert.NotSame(el.Columns[0], clone.Columns[0]);        // sloupec je vlastní instance
        clone.Columns[0].Header = "B";
        Assert.Equal("A", el.Columns[0].Header);                // změna klonu neovlivní originál
        Assert.NotSame(el.HeaderStyle, clone.HeaderStyle);
    }

    // ── ApplyScale ────────────────────────────────────────────────────────

    [Fact]
    public void ApplyScale_SkalujeVyskyIPevneSirky_AutoZustava()
    {
        var el = new DynamicTableElement
        {
            RowHeight = 6f,
            HeaderHeight = 8f,
            Columns =
            [
                new TableColumn { Width = 20f, WidthMode = ColumnWidthMode.Mm },   // pevná
                new TableColumn { Width = 0f,  WidthMode = ColumnWidthMode.Mm },   // auto
            ],
        };

        el.ApplyScale(2f);

        Assert.Equal(12f, el.RowHeight);
        Assert.Equal(16f, el.HeaderHeight);
        Assert.Equal(40f, el.Columns[0].Width);   // pevná × 2
        Assert.Equal(0f,  el.Columns[1].Width);   // auto zůstává auto
    }
}
