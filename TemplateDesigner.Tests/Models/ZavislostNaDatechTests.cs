using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Nabídka stránek dat nad náhledem se ukáže, jen když přepnutí změní výstup (`B187`, nález Jan
/// 28. 9.: stazka pod agendou linkového JŘ nabízela „Směr tam / zpět" a přepínání nic nedělalo).
/// </summary>
public class ZavislostNaDatechTests
{
    private static DataContext Data(string linka, string smer) =>
        new(new Dictionary<string, object?> { ["CISLO_LINKY"] = linka, ["SMER"] = smer });

    private static Template Sablona(params TemplateElement[] prvky) =>
        new() { Sections = [new Section { Elements = [.. prvky] }] };

    private static readonly DataContext[] DvaSmeryJedneLinky = [Data("410103", "tam"), Data("410103", "zpět")];

    [Fact]
    public void StatickaSablona_BezPoli_NemaCoPrepinat() =>
        Assert.False(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new TextElement { Text = "STRÁNKA 1 · strana {STRANA}" }), DvaSmeryJedneLinky));

    [Fact]
    public void PoleSeStejnouHodnotou_NemaCoPrepinat() =>
        Assert.False(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new TextElement { Text = "linka {CISLO_LINKY}" }), DvaSmeryJedneLinky));

    [Fact]
    public void PoleSRuznouHodnotou_Prepina() =>
        Assert.True(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new TextElement { Text = "směr {SMER:X}" }), DvaSmeryJedneLinky));

    [Fact]
    public void PoleVPodminceViditelnosti_Prepina() =>
        Assert.True(ZavislostNaDatech.PrepinaniMaSmysl(
            Sablona(new TextElement { Text = "x", VisibilityExpression = "{SMER} = \"tam\"" }), DvaSmeryJedneLinky));

    [Fact]
    public void PrvekPlnenyZDat_PrepinaVzdy() =>
        Assert.True(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new StopListElement()), [Data("1", "tam")]));

    [Fact]
    public void NeznamyTvarVeZavorkach_RozhodneProPrepinani() =>
        Assert.True(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new TextElement { Text = "{SUM(CENA)}" }), DvaSmeryJedneLinky));

    [Fact]
    public void BezDat_NemaCoPrepinat() =>
        Assert.False(ZavislostNaDatech.PrepinaniMaSmysl(Sablona(new StopListElement()), []));
}
