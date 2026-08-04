using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Náhled, export a tisk musí serveru poslat tytéž parametry a domíchat je do stránek stejně -
/// jinak vyjede jiný výstup, než ukazuje náhled, a nikdo si toho nevšimne. Vzniklo z `B50`:
/// export z lišty neposílal parametry sestavy vůbec (vyjel s defaulty) a tisková cesta naopak
/// zapomínala skupinu linek (u AD je linka klíčovaná dvojicí skupina+linka).
/// </summary>
public class GetDataParamsTests
{
    private static Dictionary<string, object?> Params() => new()
    {
        ["PISMENA_VARIANT"] = true,
        ["HODINY_OD"]       = 5,
    };

    [Fact]
    public void ComposeGetDataParams_PropustiParametrySestavy()
    {
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", null);

        Assert.Equal(true, p["PISMENA_VARIANT"]);
        Assert.Equal(5, p["HODINY_OD"]);
    }

    [Fact]
    public void ComposeGetDataParams_PridaInstanciISkupinu()
    {
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", "5");

        Assert.Equal("876401", p["LINKA_ID"]);
        Assert.Equal("5", p["SKUPINA_LINEK_ID"]);
    }

    [Fact]
    public void ComposeGetDataParams_BezSkupiny_KlicVubecNeposila()
    {
        // Prázdná skupina znamená „server si vybere default", ne „skupina je prázdný řetězec".
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", "");

        Assert.False(p.ContainsKey("SKUPINA_LINEK_ID"));
    }

    [Fact]
    public void ComposeGetDataParams_InstancePrebijeKlicLinkyZParametru()
    {
        var src = Params();
        src["LINKA_ID"] = "stara";

        var p = TemplateDesignerViewModel.ComposeGetDataParams(src, "LINKA_ID", "876401", null);

        Assert.Equal("876401", p["LINKA_ID"]);
    }

    [Fact]
    public void ComposeGetDataParams_NemeniZdrojovyDictionary()
    {
        var src = Params();
        TemplateDesignerViewModel.ComposeGetDataParams(src, "LINKA_ID", "876401", "5");

        Assert.False(src.ContainsKey("LINKA_ID"));
        Assert.False(src.ContainsKey("SKUPINA_LINEK_ID"));
    }

    [Fact]
    public void MergeParamsIntoPages_DomichaHodnotyDoKazdeStranky()
    {
        var pages = new List<DataContext> { new(), new() };

        var merged = TemplateDesignerViewModel.MergeParamsIntoPages(pages, Params());

        Assert.All(merged, p =>
        {
            Assert.Equal("True", p.GetString("PISMENA_VARIANT"));
            Assert.Equal("5", p.GetString("HODINY_OD"));
        });
    }

    [Fact]
    public void MergeParamsIntoPages_NullHodnotyNeposila()
    {
        var pages = new List<DataContext> { new() };

        var merged = TemplateDesignerViewModel.MergeParamsIntoPages(
            pages, new Dictionary<string, object?> { ["NECO"] = null });

        Assert.Same(pages, merged);   // nic k domíchání → beze změny, bez alokace nových stránek
    }
}
