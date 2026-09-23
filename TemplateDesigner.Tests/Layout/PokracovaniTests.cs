using System.Globalization;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Pole {POKRACOVANI} - nápis „pokračování / opačný směr" ze starého Win tisku (#9088), dosazovaný
/// sazbou podle stránky a směru.
/// </summary>
public class PokracovaniTests
{
    private static DataContext Data(string smer, string kultura = "cs-CZ") =>
        new DataContext(new Dictionary<string, object?> { ["SMER"] = smer })
            .WithCulture(CultureInfo.GetCultureInfo(kultura));

    [Theory]
    [InlineData("T", 1, "")]
    [InlineData("T", 2, "pokračování")]
    [InlineData("Z", 1, "opačný směr")]
    [InlineData("Z", 3, "opačný směr - pokračování")]
    [InlineData("", 2, "pokračování")]
    public void Cesky(string smer, int stranka, string ocekavano)
    {
        Assert.Equal(ocekavano, PageLayoutEngine.TextPokracovani(stranka, Data(smer)));
    }

    [Theory]
    [InlineData("T", 2, "pokračovanie")]
    [InlineData("Z", 1, "opačný smer")]
    [InlineData("Z", 2, "opačný smer - pokračovanie")]
    public void Slovensky(string smer, int stranka, string ocekavano)
    {
        Assert.Equal(ocekavano, PageLayoutEngine.TextPokracovani(stranka, Data(smer, "sk-SK")));
    }

    [Fact]
    public void BezDat_PrvniStrankaPrazdna_DalsiPokracovani()
    {
        Assert.Equal("", PageLayoutEngine.TextPokracovani(1, null));
        Assert.Equal("pokračování", PageLayoutEngine.TextPokracovani(2, null));
    }

    [Fact]
    public void HodnotyStranky_NesouVsechnaTriPole()
    {
        var h = PageLayoutEngine.HodnotyStranky(2, 5, Data("Z"));

        Assert.Equal(2, h["PAGE"]);
        Assert.Equal(5, h["TOTAL_PAGES"]);
        Assert.Equal("opačný směr - pokračování", h["POKRACOVANI"]);
    }
}
