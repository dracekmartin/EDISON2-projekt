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
        ["PRVNI_BEZ_PISMENE"] = true,
        ["HODINY_OD"]       = 5,
    };

    [Fact]
    public void ComposeGetDataParams_PropustiParametrySestavy()
    {
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", null, null, null);

        Assert.Equal(true, p["PRVNI_BEZ_PISMENE"]);
        Assert.Equal(5, p["HODINY_OD"]);
    }

    [Fact]
    public void ComposeGetDataParams_PridaInstanciISkupinu()
    {
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", "5", null, null);

        Assert.Equal("876401", p["LINKA_ID"]);
        Assert.Equal("5", p["SKUPINA_LINEK_ID"]);
    }

    [Fact]
    public void ComposeGetDataParams_BezSkupiny_KlicVubecNeposila()
    {
        // Prázdná skupina znamená „server si vybere default", ne „skupina je prázdný řetězec".
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", "", null, null);

        Assert.False(p.ContainsKey("SKUPINA_LINEK_ID"));
    }

    [Fact]
    public void ComposeGetDataParams_InstancePrebijeKlicLinkyZParametru()
    {
        var src = Params();
        src["LINKA_ID"] = "stara";

        var p = TemplateDesignerViewModel.ComposeGetDataParams(src, "LINKA_ID", "876401", null, null, null);

        Assert.Equal("876401", p["LINKA_ID"]);
    }

    [Fact]
    public void ComposeGetDataParams_NemeniZdrojovyDictionary()
    {
        var src = Params();
        TemplateDesignerViewModel.ComposeGetDataParams(src, "LINKA_ID", "876401", "5", null, null);

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
            Assert.Equal("True", p.GetString("PRVNI_BEZ_PISMENE"));
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

    // ── Jazyk textů značek (#12843) ───────────────────────────────────────────

    /// <summary>Jazyk jde do dat jako `JAZYK` - texty značek generuje server, takže bez něj by
    /// slovenská vývěska měla české popisky u správných piktogramů.</summary>
    [Fact]
    public void ComposeGetDataParams_PosleJazyk()
    {
        var p = TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", null, null, "sk");

        Assert.Equal("sk", p["JAZYK"]);
    }

    /// <summary>Bez jazyka se klíč neposílá - server zůstane na češtině (starší chování).</summary>
    [Fact]
    public void ComposeGetDataParams_BezJazyka_KlicNeposila()
    {
        Assert.DoesNotContain("JAZYK",
            TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", null, null, null).Keys);
        Assert.DoesNotContain("JAZYK",
            TemplateDesignerViewModel.ComposeGetDataParams(Params(), "LINKA_ID", "876401", null, null, "").Keys);
    }

    /// <summary>Přepnutí kultury šablony musí vypadat jako změna dat - jinak zůstane v náhledu
    /// česká legenda a znovu se načte až po výběru jiné instance.</summary>
    [Fact]
    public void WireSnapshot_SeMeniSJazykem()
    {
        Assert.NotEqual(
            TemplateDesignerViewModel.ComposeWireSnapshot(null, "cs"),
            TemplateDesignerViewModel.ComposeWireSnapshot(null, "sk"));
    }

    /// <summary>Beze změny se přenačítat nesmí - fetch je jednotky sekund na instanci.</summary>
    [Fact]
    public void WireSnapshot_BezeZmenyJeShodny()
    {
        Assert.Equal(
            TemplateDesignerViewModel.ComposeWireSnapshot("{\"a\":1}", "cs"),
            TemplateDesignerViewModel.ComposeWireSnapshot("{\"a\":1}", "cs"));
    }

    /// <summary>Definice sloupců zůstává ve snapshotu i po přidání jazyka - jazyk ji nesmí vytlačit.</summary>
    [Fact]
    public void WireSnapshot_ZachytiIZmenuSloupcu()
    {
        Assert.NotEqual(
            TemplateDesignerViewModel.ComposeWireSnapshot("{\"a\":1}", "cs"),
            TemplateDesignerViewModel.ComposeWireSnapshot("{\"a\":2}", "cs"));
    }

    /// <summary>Jazyk se bere ze šablony, ne z parametrů sestavy - persistovaná hodnota parametru
    /// ho nesmí přebít (třída chyb B24).</summary>
    [Fact]
    public void ComposeGetDataParams_JazykZeSablonyPrebijeParametry()
    {
        var params2 = new Dictionary<string, object?>(Params()) { ["JAZYK"] = "cs" };

        var p = TemplateDesignerViewModel.ComposeGetDataParams(params2, "LINKA_ID", "876401", null, null, "sk");

        Assert.Equal("sk", p["JAZYK"]);
    }
}
