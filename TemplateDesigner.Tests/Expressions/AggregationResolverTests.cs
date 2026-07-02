using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Expressions;

public class AggregationResolverTests
{
    private static IReadOnlyList<DataContext> Rows(params string[] ceny) =>
        ceny.Select(c => new DataContext(new Dictionary<string, object?> { ["CENA"] = c })).ToList();

    [Theory]
    [InlineData("SUM({CENA})", "60")]
    [InlineData("COUNT()", "3")]
    [InlineData("AVG({CENA})", "20")]
    [InlineData("MIN({CENA})", "10")]
    [InlineData("MAX({CENA})", "30")]
    public void ZakladniAgregace(string template, string expected)
    {
        Assert.Equal(expected, AggregationResolver.Resolve(template, Rows("10", "20", "30")));
    }

    [Fact]
    public void AgregaceUvnitrTextu_NahradiJenFunkci()
    {
        Assert.Equal("Celkem: 60 Kč",
            AggregationResolver.Resolve("Celkem: SUM({CENA}) Kč", Rows("10", "20", "30")));
    }

    [Fact]
    public void PrazdneRadky_CountNulaSumNula()
    {
        Assert.Equal("0", AggregationResolver.Resolve("COUNT()", []));
        Assert.Equal("0", AggregationResolver.Resolve("SUM({CENA})", []));
        Assert.Equal("0", AggregationResolver.Resolve("COUNT()", null));
    }

    [Fact]
    public void NenumerickeHodnoty_SeIgnoruji()
    {
        var rows = Rows("10", "abc", "30");
        Assert.Equal("40", AggregationResolver.Resolve("SUM({CENA})", rows));
    }

    [Fact]
    public void TextBezAgregace_ZustavaBezeZmeny()
    {
        Assert.Equal("Jen text {CENA}", AggregationResolver.Resolve("Jen text {CENA}", Rows("10")));
    }

    [Fact]
    public void MaleFunkceNazvy_FungujiCaseInsensitive()
    {
        Assert.Equal("60", AggregationResolver.Resolve("sum({CENA})", Rows("10", "20", "30")));
    }
}
