using TemplateDesigner.Models;
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

    private static readonly NazevInstance Linka = NazevInstance.Z("linka,linky,linku,linky,linek", "F");

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
    public void ZadnaLinkaNemaData_StavJmenujeLinky()
    {
        Assert.Equal("Žádná vybraná linka nemá data: linka 20, linka 21",
            PrintDialogViewModel.ZadnaData([("linka 20", Duvod), ("linka 21", null)], "obecná hláška", Linka));
        Assert.Equal("obecná hláška", PrintDialogViewModel.ZadnaData([], "obecná hláška", Linka));
        Assert.Equal("obecná hláška", PrintDialogViewModel.ZadnaData(null, "obecná hláška", Linka));
    }

    [Fact]
    public void VynechanaLinka_JeVidetUHotovehoVystupu()
    {
        Assert.Equal(" · bez dat vynecháno: linka 20, linka 21",
            PrintDialogViewModel.DovetekVynechanych([("linka 20", "x"), ("linka 21", null)]));
        Assert.Equal("", PrintDialogViewModel.DovetekVynechanych([]));
    }

    /// <summary>`B193g`: linka bez dat je hláška (problém, ne text dole) - s důvodem od agendy jako radou
    /// a s linkou, na kterou klik přepne; bez důvodu obecná rada.</summary>
    [Fact]
    public void LinkaBezDat_JeProblemSDuvodemJakoRadou()
    {
        var h = KontrolaVystupu.LinkaBezDat("linka 20", Duvod, Linka);

        Assert.Equal(ZavaznostHlaseni.Chyba, h.Zavaznost);
        Assert.Equal("linka 20", h.Kde);
        Assert.Equal("nemá data, nevytiskne se", h.Co);
        Assert.Equal(Duvod, h.Rada);
        Assert.Equal("linka 20", h.Linka);
        Assert.False(string.IsNullOrWhiteSpace(KontrolaVystupu.LinkaBezDat("linka 21", null, Linka).Rada));
    }

    [Fact]
    public void LinkyBezDat_JsouPredOstatnimiHlaskami()
    {
        var ostatni = new Hlaseni(ZavaznostHlaseni.Upozorneni, "Kód (QR, čárový)", "je malý");

        var vse = KontrolaVystupu.SBezDat([("linka 20", Duvod)], [ostatni], Linka);

        Assert.Equal(2, vse.Count);
        Assert.Equal("linka 20", vse[0].Linka);
        Assert.Same(ostatni, vse[1]);
        Assert.Single(KontrolaVystupu.SBezDat(null, [ostatni], Linka));
    }
}
