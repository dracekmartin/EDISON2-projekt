using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Expressions;

/// <summary>
/// Sdílená řádková podmínková logika (RowConditions) — viditelnost + barva zvýraznění nad daty
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

    // ── HighlightColor ────────────────────────────────────────────────────

    [Fact]
    public void HighlightColor_PodminkaSplnena_VratiBarvu() =>
        Assert.Equal("#FFFFCCCC", RowConditions.HighlightColor("{CENA} < 0", "#FFFFCCCC", Row("CENA", -5)));

    [Fact]
    public void HighlightColor_PodminkaNesplnena_Null() =>
        Assert.Null(RowConditions.HighlightColor("{CENA} < 0", "#FFFFCCCC", Row("CENA", 10)));

    [Fact]
    public void HighlightColor_ChybiVyrazNeboBarva_Null()
    {
        Assert.Null(RowConditions.HighlightColor(null, "#FFFFCCCC", Row("CENA", -5)));
        Assert.Null(RowConditions.HighlightColor("{CENA} < 0", null, Row("CENA", -5)));
        Assert.Null(RowConditions.HighlightColor("{CENA} < 0", "", Row("CENA", -5)));
    }
}
