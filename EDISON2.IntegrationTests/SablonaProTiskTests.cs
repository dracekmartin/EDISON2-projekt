using ALWA.SYS;
using TemplateDesigner.Services;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Pravidlo výběru šablony pro tisk (`API.SablonyProTisk`, `B190`) přes skutečné MLRest a parser, kterým odpověď
/// čte spuštění návrháře do PDF (<see cref="PrintTemplateChoice.FromOut"/>). Pravidlo samo hlídají Caché testy
/// (`SablonyProTiskTest`); tady jde o to, že klient z odpovědi pozná šablonu, nebo dostane důvod - ne prázdno.
/// Jen čtení, nic nezapisuje.
/// </summary>
[Trait("Category", "Integration")]
public class SablonaProTiskTests
{
    private const string Pravidlo = PrintTemplateChoice.CacheClass;

    [DevServerFact]
    public void VybranaSablonaKteraNeni_VratiDuvod()
    {
        var mlRest = DevServer.Connect();
        var vstup = new CVar();
        vstup["agendaCode"] = new CVar("LINKOVY_JR_AD");
        vstup["templateId"] = new CVar(Guid.NewGuid().ToString());

        var volba = PrintTemplateChoice.FromOut(mlRest.QueryOrThrow(Pravidlo, "GetTemplateForPrint", vstup));

        Assert.Null(volba.TemplateId);
        Assert.Contains("Vybraná šablona pro tisk v databázi není", volba.Duvod);
    }

    [DevServerFact]
    public void BezVolby_SablonaNeboDuvod()
    {
        var mlRest = DevServer.Connect();
        var vstup = new CVar();
        vstup["agendaCode"] = new CVar("LINKOVY_JR_AD");

        var volba = PrintTemplateChoice.FromOut(mlRest.QueryOrThrow(Pravidlo, "GetTemplateForPrint", vstup));

        // Výchozí šablona firmy ani nalitá šablona M-line na serveru být nemusí; prázdno ale nesmí přijít nikdy.
        if (volba.TemplateId is null)
            Assert.Contains("není nastavená šablona pro tisk", volba.Duvod);
        else
        {
            Assert.False(string.IsNullOrEmpty(volba.SourceIdFa), "šablona bez větve, ze které se načte");
            Assert.False(string.IsNullOrEmpty(volba.Name), "šablona bez jména");
        }
    }
}
