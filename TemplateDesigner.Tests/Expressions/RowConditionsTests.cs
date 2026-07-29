using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Expressions;

/// <summary>
/// Sdílená řádková podmínková logika (RowConditions) - viditelnost + barva zvýraznění nad daty
/// jednoho řádku. Sdíleno DynamicTable/StopList/LineTable; DepartureGrid mimo (pivot).
/// </summary>
public class RowConditionsTests
{
    private static DataContext Row(string field, object? value) =>
        new(new Dictionary<string, object?> { [field] = value });

    // ── IsVisible ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("{CENA} > 0", 10, true)]
    [InlineData("{CENA} > 0", -5, false)]
    [InlineData("{CENA} > 0", 0, false)]
    public void IsVisible_VyhodnotiVyrazNadRadkem(string expr, double cena, bool expected) =>
        Assert.Equal(expected, RowConditions.IsVisible(expr, Row("CENA", cena)));

    [Fact]
    public void IsVisible_PrazdnyVyraz_VzdyViditelny()
    {
        Assert.True(RowConditions.IsVisible(null, Row("CENA", -5)));
        Assert.True(RowConditions.IsVisible("", Row("CENA", -5)));
    }

    // ── VisibleRows (sdílený filtr) ───────────────────────────────────────

    [Fact]
    public void VisibleRows_FiltrujeDleVyrazu()
    {
        var rows = new List<DataContext> { Row("CENA", 10), Row("CENA", -5), Row("CENA", 20) };
        Assert.Equal(2, RowConditions.VisibleRows(rows, "{CENA} > 0").Count);
    }

    [Fact]
    public void VisibleRows_PrazdnyVyraz_VratiVstupBezAlokace()
    {
        var rows = new List<DataContext> { Row("CENA", 10) };
        Assert.Same(rows, RowConditions.VisibleRows(rows, null));
        Assert.Same(rows, RowConditions.VisibleRows(rows, ""));
    }

    [Fact]
    public void VisibleRows_Genericke_FiltrujePresRowOf()
    {
        var items = new[] { (name: "a", row: Row("V", 1)), (name: "b", row: Row("V", 0)) };
        var visible = RowConditions.VisibleRows(items, x => x.row, "{V} > 0");
        Assert.Equal("a", Assert.Single(visible).name);
    }

    // ── HighlightColor (seznam pravidel, první splněné vyhrává) ────────────

    private static List<HighlightRule> Rules(params (string cond, string color)[] r) =>
        r.Select(x => new HighlightRule { Condition = x.cond, BackgroundColor = x.color }).ToList();

    [Fact]
    public void HighlightColor_PodminkaSplnena_VratiBarvu() =>
        Assert.Equal("#FFFFCCCC",
            RowConditions.HighlightColor(Rules(("{CENA} < 0", "#FFFFCCCC")), Row("CENA", -5)));

    [Fact]
    public void HighlightColor_PodminkaNesplnena_Null() =>
        Assert.Null(RowConditions.HighlightColor(Rules(("{CENA} < 0", "#FFFFCCCC")), Row("CENA", 10)));

    [Fact]
    public void HighlightColor_PrvniSplnenaVyhrava()
    {
        // Obě podmínky platí (cena -5) → vrátí barvu PRVNÍHO pravidla
        var rules = Rules(("{CENA} < 0", "#FF111111"), ("{CENA} < 100", "#FF222222"));
        Assert.Equal("#FF111111", RowConditions.HighlightColor(rules, Row("CENA", -5)));
    }

    [Fact]
    public void HighlightColor_PreskociNesplnena_VezmeDalsi()
    {
        // První neplatí (cena 10 není < 0), druhé platí → barva druhého
        var rules = Rules(("{CENA} < 0", "#FF111111"), ("{CENA} < 100", "#FF222222"));
        Assert.Equal("#FF222222", RowConditions.HighlightColor(rules, Row("CENA", 10)));
    }

    [Fact]
    public void HighlightColor_ZadnaPravidla_Null()
    {
        Assert.Null(RowConditions.HighlightColor(null, Row("CENA", -5)));
        Assert.Null(RowConditions.HighlightColor([], Row("CENA", -5)));
    }

    [Fact]
    public void HighlightColor_PraviloBezVyrazuNeboBarvy_Preskoci()
    {
        Assert.Null(RowConditions.HighlightColor(
            [new HighlightRule { Condition = null, BackgroundColor = "#FFFFCCCC" }], Row("CENA", -5)));
        Assert.Null(RowConditions.HighlightColor(
            [new HighlightRule { Condition = "{CENA} < 0", BackgroundColor = "" }], Row("CENA", -5)));
    }
}
