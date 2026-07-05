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
    public void CalculateHeightFromData_BezDataContextu_PouzijeNahledoveRadky()
    {
        var el = new DynamicTableElement { DataSource = "ZASTAVKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f };
        // Bez DataContextu (návrhový canvas) → 3 náhledové řádky: 8 + 3*6 = 26
        Assert.Equal(26f, el.CalculateHeightFromData(null));
    }

    [Fact]
    public void CalculateHeightFromData_PrazdnaKolekceSDaty_JenZahlavi()
    {
        var el = new DynamicTableElement { DataSource = "ZASTAVKY", ShowHeader = true, HeaderHeight = 8f, RowHeight = 6f };
        // S DataContextem, ale prázdná kolekce → reálně prázdná tabulka (jen záhlaví), ne fake řádky: 8
        Assert.Equal(8f, el.CalculateHeightFromData(new DataContext()));
    }

    // ── RowVisibilityExpression (viditelnost řádku) ──────────────────────

    private static DataContext DataWithCena(params double[] ceny)
    {
        var rows = ceny.Select(c => new DataContext(new Dictionary<string, object?> { ["CENA"] = c })).ToList();
        return new DataContext(rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["POLOZKY"] = rows
        });
    }

    [Fact]
    public void RowVisibility_FiltrujeRadkyDoVysky()
    {
        var el = new DynamicTableElement
        {
            DataSource = "POLOZKY", ShowHeader = false, HeaderHeight = 8f, RowHeight = 6f,
            RowVisibilityExpression = "{CENA} > 0",
        };

        // 4 řádky, 2 nesplní podmínku (0, -5) → započítají se jen 2: 2*6 = 12
        Assert.Equal(12f, el.CalculateHeightFromData(DataWithCena(10, 0, 20, -5)));
    }

    [Fact]
    public void RowVisibility_PrazdnyVyraz_VsechnyRadky()
    {
        var el = new DynamicTableElement
        {
            DataSource = "POLOZKY", ShowHeader = false, RowHeight = 6f,
            RowVisibilityExpression = null,
        };
        Assert.Equal(18f, el.CalculateHeightFromData(DataWithCena(10, 0, -5)));   // 3*6, žádný filtr
    }

    [Fact]
    public void RowVisibility_ZadnyRadekNesplni_VyskaNula()
    {
        var el = new DynamicTableElement
        {
            DataSource = "POLOZKY", ShowHeader = false, RowHeight = 6f,
            RowVisibilityExpression = "{CENA} > 100",
        };
        // S daty, ale vše odfiltrované → reálně prázdná tabulka (žádné náhledové/fake řádky v tisku): 0.
        Assert.Equal(0f, el.CalculateHeightFromData(DataWithCena(10, 20, 30)));
    }

    // ── ResolveRowHighlight (podmíněné pozadí, 1 pravidlo) ───────────────

    private static DataContext RowWithCena(double cena) =>
        new(new Dictionary<string, object?> { ["CENA"] = cena });

    [Fact]
    public void RowHighlight_PodminkaSplnena_VratiBarvu()
    {
        var el = new DynamicTableElement { RowHighlightExpression = "{CENA} < 0", RowHighlightColor = "#FFFFCCCC" };
        Assert.Equal("#FFFFCCCC", el.ResolveRowHighlight(RowWithCena(-5)));
    }

    [Fact]
    public void RowHighlight_PodminkaNesplnena_Null()
    {
        var el = new DynamicTableElement { RowHighlightExpression = "{CENA} < 0", RowHighlightColor = "#FFFFCCCC" };
        Assert.Null(el.ResolveRowHighlight(RowWithCena(10)));
    }

    [Fact]
    public void RowHighlight_ChybiBarvaNeboVyraz_Null()
    {
        Assert.Null(new DynamicTableElement { RowHighlightExpression = "{CENA} < 0" }.ResolveRowHighlight(RowWithCena(-5)));
        Assert.Null(new DynamicTableElement { RowHighlightColor = "#FFFFCCCC" }.ResolveRowHighlight(RowWithCena(-5)));
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
            RowVisibilityExpression = "{CENA} > 0",
            RowHighlightExpression = "{CENA} < 0",
            RowHighlightColor = "#FFFFCCCC",
        };

        var clone = (DynamicTableElement)el.Clone();

        Assert.NotEqual(el.Id, clone.Id);                       // CopyBaseTo dává nové Guid
        Assert.Equal("ZASTAVKY", clone.DataSource);
        Assert.Equal("{CENA} > 0", clone.RowVisibilityExpression);
        Assert.Equal("{CENA} < 0", clone.RowHighlightExpression);
        Assert.Equal("#FFFFCCCC", clone.RowHighlightColor);
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
