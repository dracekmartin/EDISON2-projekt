using System.Text.Json.Nodes;
using ALWA.SYS;
using EDControls.EDList;
using TemplateDesigner.Services;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Strom šablon a seznam šablon podle uzlu (`API.StromSablon`, `EDList.Sablony`, `B190`) přes skutečné MLRest
/// a parser, kterým odpověď čte okno šablon v návrháři (<see cref="TemplateCatalog.Parse"/>). Tvar stromu a výchozí
/// šablonu hlídají Caché testy (`StromSablonTest`); tady jde o to, že klient strom z odpovědi přečte a seznam
/// pošle filtr, kterému server rozumí. Jen čtení, nic nezapisuje.
/// </summary>
[Trait("Category", "Integration")]
public class StromSablonTests
{
    [DevServerFact]
    public void Strom_MaJizdniRadyAObecne()
    {
        var mlRest = DevServer.Connect();

        var strom = TemplateCatalog.Parse(
            mlRest.QueryOrThrow(TemplateCatalog.CacheClass, "GetStrom", new CVar())["1"]?.Value?.ToString());

        var jizdniRady = Assert.Single(strom.Areas, a => a.Code == "JIZDNI_RADY");
        Assert.Equal("Jízdní řády", jizdniRady.Label);
        var linkovy = Assert.Single(jizdniRady.Agendas, a => a.Code == "LINKOVY_JR");
        Assert.Contains(linkovy.Variants, v => v.Code == "LINKOVY_JR_AD" && v.Label == "AD" && v.CanPrint);

        var obecna = Assert.Single(Assert.Single(strom.Areas, a => a.Code == "OBECNE").Agendas).Variants.Single();
        Assert.Equal("GENERAL", obecna.Code);
        Assert.False(obecna.CanPrint, "Obecná agenda se z modulu netiskne");
        Assert.Equal("Obecné", strom.Areas[^1].Label);
    }

    [DevServerFact]
    public void Seznam_FiltrUzlu_JenJehoAgendy()
    {
        var mlRest = DevServer.Connect();
        // Týž vstup, jaký posílá okno šablon (EDList.LoadListAsync s filtrem vybraného uzlu).
        var vstup = EDListParsing.BuildLoadInput(new Dictionary<string, string> { ["agendaCodes"] = "LINKOVY_JR_AD,LINKOVY_JR_MHD" });

        CVar vystup = mlRest.QueryOrThrow("ALVA.TemplateDesigner.EDList.Sablony", "LoadList", vstup);

        var radky = new List<JsonNode>();
        foreach (KeyValuePair<string, CVar> radek in vystup)
            if (int.TryParse(radek.Key, out _) && radek.Value.Value?.ToString() is { Length: > 0 } json)
                radky.Add(JsonNode.Parse(json)!);
        Assert.NotEmpty(radky);
        Assert.All(radky, r => Assert.Contains(r["agendaCode"]?.ToString(), new[] { "LINKOVY_JR_AD", "LINKOVY_JR_MHD" }));
        Assert.All(radky, r => Assert.NotNull(r["vychozi"]));
    }
}
