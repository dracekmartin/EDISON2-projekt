using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// „Jízda pod minutu jako &lt;1" (TRANSDEV `TR8`). Časy jsou kumulativní od vývěskové zastávky,
/// takže nula pro *jízdu* vzniká jen na zastávkách hned za ní. Testy drží hlavně to, co se lehko
/// rozbije: že se „&lt;1" **neobjeví** u vývěskové zastávky, u projetých a u zastávek, které
/// reprezentativní spoj neobsluhuje (tam je nula taky, ale znamená „chybí údaj").
/// </summary>
public class StopListSubMinuteTravelTests
{
    private static ZastavkaData Stop(string nazev, int casJizdy, bool jeAktualni = false, params string[] varianty)
        => new() { Popis = nazev, CasJizdy = casJizdy.ToString(), JeAktualni = jeAktualni, TrasaMin = [.. varianty] };

    private static List<StopColumn> Casy() => [new StopColumn { Key = StopFields.TravelTime }];

    private static List<StopColumn> VariantniCasy(int pocet) =>
        [.. Enumerable.Range(0, pocet).Select(i => new StopColumn { Key = $"{StopFields.TravelTimeVariantPrefix}{i}" })];

    /// <summary>Vývěsková zastávka, za ní dvě do minuty a pak už reálné časy.</summary>
    private static List<ZastavkaData> Trasa() =>
    [
        Stop("Vývěsková", 0, jeAktualni: true),
        Stop("Hned za ní", 0),
        Stop("Taky do minuty", 0),
        Stop("Za tři minuty", 3),
        Stop("Za pět minut", 5),
    ];

    private static string[] Texty(StopListElement el, List<ZastavkaData> trasa, bool showPassed = false)
    {
        el.PrepareSubMinuteCells(trasa, showPassed, Casy());
        return [.. trasa.Select(z => el.GetCellText(z, StopFields.TravelTime))];
    }

    [Fact]
    public void Vypnuto_NulaZustavaPrazdna()
    {
        var trasa = Trasa();
        Assert.Equal(["", "", "", "3", "5"], Texty(new StopListElement(), trasa));
    }

    [Fact]
    public void Zapnuto_ZastavkyDoMinutyDostanouZnacku()
    {
        var trasa = Trasa();
        var texty = Texty(new StopListElement { ShowSubMinuteTravel = true }, trasa);

        Assert.Equal(["", "<1", "<1", "3", "5"], texty);
    }

    /// <summary>Na vývěskové zastávce nulová jízda není informace, ale definice - stojím tam.</summary>
    [Fact]
    public void Zapnuto_VyveskovaZastavkaZnackuNedostane()
    {
        var trasa = Trasa();
        var texty = Texty(new StopListElement { ShowSubMinuteTravel = true }, trasa);

        Assert.Equal("", texty[0]);
    }

    /// <summary>Past, kvůli které je tohle netriviální: `CasJizdyZMapy` vrací 0 i pro zastávku, kterou
    /// reprezentativní spoj neobsluhuje. Taková nula přijde **za** nenulovým časem, protože kumulativní
    /// časy rostou - a nesmí se z ní stát „&lt;1".</summary>
    [Fact]
    public void Zapnuto_NulaZaRealnymCasemJeChybejiciUdaj_NeJizdaPodMinutu()
    {
        List<ZastavkaData> trasa =
        [
            Stop("Vývěsková", 0, jeAktualni: true),
            Stop("Hned za ní", 0),
            Stop("Za tři minuty", 3),
            Stop("Spoj tu nestaví", 0),   // 0 = chybí údaj, ne jízda pod minutu
            Stop("Za deset minut", 10),
        ];

        var texty = Texty(new StopListElement { ShowSubMinuteTravel = true }, trasa);

        Assert.Equal(["", "<1", "3", "", "10"], texty);
    }

    /// <summary>Projeté zastávky mají nulu vynucenou agendou („údaj není relevantní", KOVED #13091).</summary>
    [Fact]
    public void Zapnuto_ProjetaZastavkaZnackuNedostane()
    {
        List<ZastavkaData> trasa =
        [
            Stop("Projetá", 0),
            Stop("Vývěsková", 0, jeAktualni: true),
            Stop("Hned za ní", 0),
        ];

        var el = new StopListElement { ShowSubMinuteTravel = true, PassedStopsTime = PassedStopsTimeDisplay.Show };
        var texty = Texty(el, trasa, showPassed: true);

        Assert.Equal(["", "", "<1"], texty);
    }

    // ── Variantní sloupce časů ────────────────────────────────────────────────

    [Fact]
    public void Zapnuto_VariantniSloupceMajiVlastniBeh()
    {
        List<ZastavkaData> trasa =
        [
            Stop("Vývěsková", 0, jeAktualni: true, "0", "0"),
            Stop("Hned za ní", 0, false, "0", "2"),      // varianta A do minuty, varianta B už 2 min
            Stop("Dál", 4, false, "0", "4"),             // varianta A pořád 0 → pořád jízda pod minutu
        ];

        var el = new StopListElement { ShowSubMinuteTravel = true };
        el.PrepareSubMinuteCells(trasa, false, VariantniCasy(2));

        string A(int radek) => el.GetCellText(trasa[radek], $"{StopFields.TravelTimeVariantPrefix}0");
        string B(int radek) => el.GetCellText(trasa[radek], $"{StopFields.TravelTimeVariantPrefix}1");

        Assert.Equal(["0", "<1", "<1"], new[] { A(0), A(1), A(2) });
        Assert.Equal(["0", "2", "4"], new[] { B(0), B(1), B(2) });
    }

    /// <summary>„&lt;" = varianta tu nestaví. Není to čas, takže běh nul neukončí ani nezaloží.</summary>
    [Fact]
    public void Zapnuto_ZnackaProjizdeniZustava()
    {
        List<ZastavkaData> trasa =
        [
            Stop("Vývěsková", 0, jeAktualni: true, "0"),
            Stop("Varianta tu nestaví", 0, false, "<"),
            Stop("Hned za ní", 0, false, "0"),
        ];

        var el = new StopListElement { ShowSubMinuteTravel = true };
        el.PrepareSubMinuteCells(trasa, false, VariantniCasy(1));

        Assert.Equal("<", el.GetCellText(trasa[1], $"{StopFields.TravelTimeVariantPrefix}0"));
        Assert.Equal("<1", el.GetCellText(trasa[2], $"{StopFields.TravelTimeVariantPrefix}0"));
    }

    // ── Zarovnání číslic (zavěšení „<") ───────────────────────────────────────

    private static float Odsazeni(StopListElement el, List<ZastavkaData> trasa, int radek)
    {
        var sloupce = Casy();
        el.PrepareSubMinuteCells(trasa, false, sloupce);
        return el.ResolveCellStyle(sloupce[0], trasa[radek], isCurrent: false, isPassed: false).PaddingLeft;
    }

    /// <summary>Ostatní čísla se odsadí o šířku „&lt;", aby jejich číslice zůstaly ve svislici
    /// s číslicí v „&lt;1". Samo „&lt;1" se neodsazuje - právě ono ten znak vlevo vystrkuje.</summary>
    [Fact]
    public void Zapnuto_CislaSeOdsadi_SamoZnaceniNe()
    {
        var trasa = Trasa();
        var el = new StopListElement { ShowSubMinuteTravel = true };

        float bezZnacky = Odsazeni(el, trasa, 3);       // „3" - odsazené
        float seZnackou = Odsazeni(el, trasa, 1);       // „<1" - neodsazené
        float vychozi = new StopListElement().ResolveCellStyle(Casy()[0], trasa[3], false, false).PaddingLeft;

        Assert.True(bezZnacky > seZnackou, $"číslo mělo být odsazené: {bezZnacky} vs {seZnackou}");
        Assert.Equal(vychozi, seZnackou, 3);
        Assert.True(bezZnacky - vychozi > 0.5f, $"odsazení je podezřele malé: {bezZnacky - vychozi} mm");
    }

    /// <summary>Při zarovnání vpravo se číslice srovnají samy - odsazení by je rozhodilo.</summary>
    [Fact]
    public void ZarovnaniVpravo_Neodsazuje()
    {
        var trasa = Trasa();
        var el = new StopListElement { ShowSubMinuteTravel = true };
        var sloupce = new List<StopColumn>
            { new() { Key = StopFields.TravelTime, Alignment = TextHorizontalAlignment.Right } };
        el.PrepareSubMinuteCells(trasa, false, sloupce);

        Assert.Equal(el.ResolveCellStyle(sloupce[0], trasa[1], false, false).PaddingLeft,
                     el.ResolveCellStyle(sloupce[0], trasa[3], false, false).PaddingLeft, 3);
    }

    /// <summary>Bez jízdy pod minutu se nemá odsazovat nic - vývěska musí vypadat jako dřív.</summary>
    [Fact]
    public void BezNuloveJizdy_NeodsazujeNic()
    {
        List<ZastavkaData> trasa =
        [
            Stop("Vývěsková", 0, jeAktualni: true),
            Stop("Za dvě minuty", 2),
            Stop("Za pět minut", 5),
        ];
        var el = new StopListElement { ShowSubMinuteTravel = true };
        float vychozi = new StopListElement().ResolveCellStyle(Casy()[0], trasa[1], false, false).PaddingLeft;

        Assert.Equal(vychozi, Odsazeni(el, trasa, 1), 3);
    }

    // Zarovnání a styl variantních sloupců se od `B80` berou z položky „Varianty tras", ne ze
    // sloupce „Čas" - kontrakt drží `StopListVariantColumnsTests`, tady byl jen shodou okolností.

    [Fact]
    public void Klonovani_NeseVolbuDal()
    {
        var klon = (StopListElement)new StopListElement { ShowSubMinuteTravel = true }.Clone();
        Assert.True(klon.ShowSubMinuteTravel);
    }
}
