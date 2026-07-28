using System.Text.Json.Nodes;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// DataContext.GetString - mj. spojení vnořeného skalárního pole (ZONY) do stringu, ať ho umí
/// zobrazit i plochá buňka DynamicTable (specializované elementy si pole parsují samy).
/// </summary>
public class DataContextTests
{
    [Fact]
    public void GetString_SkalarniPole_VratiHodnotu()
    {
        var ctx = new DataContext(new Dictionary<string, object?> { ["POPIS"] = "Praha hl.n." });
        Assert.Equal("Praha hl.n.", ctx.GetString("POPIS"));
    }

    [Fact]
    public void GetString_ChybejiciPole_VratiFallback()
    {
        var ctx = new DataContext(new Dictionary<string, object?>());
        Assert.Equal("", ctx.GetString("NEEXISTUJE"));
        Assert.Equal("-", ctx.GetString("NEEXISTUJE", "-"));
    }

    [Fact]
    public void GetString_SkalarniArray_SpojiLomitkem()
    {
        var ctx = new DataContext(new Dictionary<string, object?> { ["ZONY"] = new JsonArray("1", "2", "3") });
        Assert.Equal("1/2/3", ctx.GetString("ZONY"));
    }

    [Fact]
    public void GetString_ObjektovyArray_PreskociObjekty()
    {
        // ATRIBUTY = pole objektů {ZNAK,POPIS} → do ploché buňky se nevejde, join přeskočí objekty
        var atributy = new JsonArray(new JsonObject { ["ZNAK"] = "x" });
        var ctx = new DataContext(new Dictionary<string, object?> { ["ATRIBUTY"] = atributy });
        Assert.Equal("", ctx.GetString("ATRIBUTY"));
    }
}
