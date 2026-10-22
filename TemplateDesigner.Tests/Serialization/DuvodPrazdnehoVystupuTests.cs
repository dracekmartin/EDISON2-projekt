using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Linka, ze které agenda nedá ani jednu stránku (KV_DP linka 20: spoje bez spočítaných časů), dřív
/// skončila jen „Žádná data" a při tisku víc linek zmizela potichu. Agenda teď pošle důvod a radu
/// (`DUVOD_PRAZDNE`, aditivní klíč) a návrhář ho ukáže; tisk ostatních linek nezastaví.
/// </summary>
public class DuvodPrazdnehoVystupuTests
{
    private const string Duvod = "Spoje linky nemají spočítané časy, nejspíš chybí jízdní doby. Doplňte jízdní doby linky v modulu MHD.";

    [Fact]
    public void DuvodSePrecte_AStrankouNeni()
    {
        var uzly = new List<(string, string?)> { (CacheAgendaProvider.DuvodPrazdnehoKlic, Duvod) };

        Assert.Equal(Duvod, CacheAgendaProvider.DuvodPrazdneho(uzly));
        Assert.Null(CacheAgendaProvider.ParsePages(uzly));
    }

    [Fact]
    public void BezDuvodu_Null()
    {
        var uzly = new List<(string, string?)> { ("1", """{ "values": { "SMER": "T" } }""") };

        Assert.Null(CacheAgendaProvider.DuvodPrazdneho(uzly));
        Assert.Single(CacheAgendaProvider.ParsePages(uzly)!);
    }

    [Fact]
    public void ZadnaLinkaNemaData_HlaskaNeseDuvody()
    {
        Assert.Equal($"Žádná vybraná linka nemá data: linka 20 - {Duvod}",
            PrintDialogViewModel.ZadnaData([$"linka 20 - {Duvod}"], "obecná hláška"));
        Assert.Equal("obecná hláška", PrintDialogViewModel.ZadnaData([], "obecná hláška"));
        Assert.Equal("obecná hláška", PrintDialogViewModel.ZadnaData(null, "obecná hláška"));
    }

    [Fact]
    public void VynechanaLinka_JeVidetUHotovehoVystupu()
    {
        Assert.Equal(" · bez dat vynecháno: linka 20 - x; linka 21",
            PrintDialogViewModel.DovetekVynechanych(["linka 20 - x", "linka 21"]));
        Assert.Equal("", PrintDialogViewModel.DovetekVynechanych([]));
    }
}
