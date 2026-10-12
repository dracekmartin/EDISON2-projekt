using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Poznámka spoje zadaná zákazníkem volným textem se v řádku platnosti kreslí písmem, ne
/// piktogramem (`B191b`). Městské linky mají takové značky jednopísmenné („A", „K", „H")
/// a prvek by je jinak přeložil na autobus, kolo a vozíček - starý tisk je sází jako text.
/// </summary>
public class TextoveZnackyTests
{
    [Fact]
    public void TextovaZnackaJdePismem_SymbolDalPiktogramem()
    {
        var spoj = Spoj(platnost: new JsonArray("BEV", "A"), texty: new JsonArray("A"));
        var resolve = LineTableElement.ResolverZnacek(spoj);

        Assert.Equal((SymbolRenderMode.Text, "A"), resolve("A"));
        Assert.Equal(SymbolMap.ResolveLinkovy("BEV"), resolve("BEV"));
        Assert.Equal(SymbolRenderMode.FontGlyf, resolve("BEV").Mode);
    }

    /// <summary>Bez klíče (AD, starší server) se nic nemění - „A" zůstává piktogramem autobusu.</summary>
    [Fact]
    public void BezTextovychZnacek_PlatiMapaLinkoveho()
    {
        var spoj = Spoj(platnost: new JsonArray("A"), texty: null);

        Assert.Equal(SymbolMap.ResolveLinkovy("A"), LineTableElement.ResolverZnacek(spoj)("A"));
    }

    private static LineTableElement.LineTripData Spoj(JsonArray platnost, JsonArray? texty)
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 60, Height = null };
        var radek = texty is null
            ? RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
                ("PLATNOST", platnost), ("CASY", new JsonArray("8:00")))
            : RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
                ("PLATNOST", platnost), ("TEXTOVE_ZNACKY", texty), ("CASY", new JsonArray("8:00")));
        var data = RenderSnapshot.Data(
            ("ZASTAVKY", [RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Náměstí"), ("PORADI", "1"))]),
            ("SPOJE", [radek]));
        return Assert.Single(prvek.SpojeVUseku(data, 0, 1000));
    }
}
