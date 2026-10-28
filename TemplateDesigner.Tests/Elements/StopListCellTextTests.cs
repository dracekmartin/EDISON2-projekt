using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Režimy sloupce „Označník" (<see cref="StopListElement.GetCellText"/>, KOVED #13048 bod 4):
/// Z trasy linky / Nejčetnější dle spojů / Výčet všech - včetně fallbacku na hodnotu z trasy,
/// když per-spoj data chybí.
/// </summary>
public class StopListCellTextTests
{
    private static ZastavkaData Stop(string zTrasy, string dleSpoju) => new()
    {
        Popis = "Zastávka",
        Nastupiste = zTrasy,
        NastupisteSpoju = dleSpoju,
    };

    private static string Text(MultiPlatformDisplay mode, ZastavkaData z) =>
        new StopListElement { MultiPlatformMode = mode }.GetCellText(z, StopFields.Nastupiste);

    [Fact]
    public void ZTrasyLinky_IgnorujeDataSpoju()
    {
        Assert.Equal("11", Text(MultiPlatformDisplay.FromRoute, Stop("11", "22,11")));
    }

    [Fact]
    public void Nejcetnejsi_BereProvniPolozkuCsv()
    {
        Assert.Equal("22", Text(MultiPlatformDisplay.MostFrequent, Stop("11", "22,11")));
    }

    [Fact]
    public void VycetVsech_ZobraziCsvSMezerami()
    {
        Assert.Equal("3, 2, 4", Text(MultiPlatformDisplay.All, Stop("1", "3,2,4")));
    }

    [Fact]
    public void BezDatSpoju_PadaNaHodnotuZTrasy()
    {
        Assert.Equal("5", Text(MultiPlatformDisplay.MostFrequent, Stop("5", "")));
        Assert.Equal("5", Text(MultiPlatformDisplay.All, Stop("5", "")));
    }

    // ── Sloupek (KOVED #13088) ────────────────────────────────────────────────

    /// <summary>Sloupek je jiný údaj než označník (číslo z číselníku zastávky vs. volný text
    /// z trasy) - zákazníci, kteří evidují oboje, mají v datech obě hodnoty a nesmí se míchat.</summary>
    [Fact]
    public void Sloupek_JeNezavislyNaOznacniku()
    {
        var z = Stop("11", "22,11");
        z.Sloupek = "2";

        Assert.Equal("2", new StopListElement().GetCellText(z, StopFields.Sloupek));
        Assert.Equal("11", new StopListElement().GetCellText(z, StopFields.Nastupiste));
    }

    /// <summary>Režimy platí pro ten údaj, který je zvolený jako stanoviště. Když je zvolený označník,
    /// sloupec Sloupek ukazuje prostou hodnotu z trasy - jinak by pod hlavičkou „Sloupek" vyšlo číslo
    /// spočítané z označníků.</summary>
    [Fact]
    public void Sloupek_BezPrepnutiIgnorujeRezimy()
    {
        var z = Stop("11", "22,11");
        z.Sloupek = "2";
        z.SloupekSpoju = "3,2";

        Assert.Equal("2", new StopListElement { MultiPlatformMode = MultiPlatformDisplay.MostFrequent }
            .GetCellText(z, StopFields.Sloupek));
        Assert.Equal("2", new StopListElement { MultiPlatformMode = MultiPlatformDisplay.All }
            .GetCellText(z, StopFields.Sloupek));
    }

    /// <summary>Po přepnutí parametru sestavy na Sloupek přeberou režimy sloupky a označník se naopak
    /// přestane počítat - přepínač je jeden a platí pro oba sloupce zrcadlově (KOVED #13088).</summary>
    [Fact]
    public void PoPrepnutiNaSloupek_RezimyPlatiProSloupek()
    {
        var z = Stop("11", "22,11");
        z.Sloupek = "2";
        z.SloupekSpoju = "3,2";

        var nejcetnejsi = new StopListElement
        {
            MultiPlatformMode = MultiPlatformDisplay.MostFrequent,
            SloupekJakoStanoviste = true,
        };
        Assert.Equal("3", nejcetnejsi.GetCellText(z, StopFields.Sloupek));
        Assert.Equal("11", nejcetnejsi.GetCellText(z, StopFields.Nastupiste));

        var vycet = new StopListElement
        {
            MultiPlatformMode = MultiPlatformDisplay.All,
            SloupekJakoStanoviste = true,
        };
        Assert.Equal("3, 2", vycet.GetCellText(z, StopFields.Sloupek));
        Assert.Equal("11", vycet.GetCellText(z, StopFields.Nastupiste));
    }

    /// <summary>Zdroj stanoviště je odvozený z dat, ne uživatelské nastavení prvku - do šablony se
    /// nesmí uložit, jinak by přebil parametr sestavy (past odklizená v `B41`).</summary>
    [Fact]
    public void ZdrojStanoviste_SeNeserializuje()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new StopListElement { SloupekJakoStanoviste = true });

        Assert.DoesNotContain("SloupekJakoStanoviste", json);
    }

    /// <summary>MHD sloupek nevydává (tam je totéž stanoviště „označník") - prázdná buňka, ne pád.</summary>
    [Fact]
    public void Sloupek_BezDatJePrazdny()
    {
        Assert.Equal("", new StopListElement().GetCellText(Stop("11", ""), StopFields.Sloupek));
        Assert.Equal("", new StopListElement().GetCellText(Stop("11", ""), StopFields.SloupekPopis));
    }

    /// <summary>Popis sloupku se v nabídce sloupců nenabízí (nikdo si o něj neřekl), ale data ho nesou
    /// a vykreslí se, kdykoli se ten klíč jako sloupec použije - dynamickou tabulkou nebo až ho někdo
    /// bude chtít zpět v nabídce.</summary>
    [Fact]
    public void PopisSloupku_SeVykresliKdyzSePouzije()
    {
        var z = Stop("11", "");
        z.Sloupek = "2";
        z.SloupekPopis = "směr Brno přes UH";

        Assert.Equal("směr Brno přes UH", new StopListElement().GetCellText(z, StopFields.SloupekPopis));
        Assert.Equal("2", new StopListElement().GetCellText(z, StopFields.Sloupek));
    }

    // ── Zóny (KOVED #13091 bod 4) ─────────────────────────────────────────────

    /// <summary>Hraniční zastávka leží ve dvou zónách. Starý modul je odděloval čárkou, takže
    /// stejně musí vypadat i vývěska z návrháře - jinak se zákazníkovi změní tvar údaje.</summary>
    [Fact]
    public void ViceZonJeOddelenoCarkou()
    {
        var z = new ZastavkaData { Popis = "Zastávka", Zony = ["11", "12"] };

        Assert.Equal("11,12", new StopListElement().GetCellText(z, StopFields.Zone));
    }

    [Fact]
    public void JednaZonaSeVypiseSama()
    {
        var z = new ZastavkaData { Popis = "Zastávka", Zony = ["101"] };

        Assert.Equal("101", new StopListElement().GetCellText(z, StopFields.Zone));
    }

    // ── Čas jízdy u projeté zastávky (KOVED #13091) ───────────────────────────
    // Časy jsou kumulativní od vývěskové zastávky, takže u projetých vycházejí záporné.

    private static ZastavkaData StopSCasy(int casJizdy, params string[] varianty) => new()
    {
        Popis = "Zastávka",
        CasJizdy = casJizdy.ToString(),
        TrasaMin = [.. varianty],
    };

    private static string VariantniCas(bool zobrazit, ZastavkaData z, bool projeta) =>
        new StopListElement { PassedStopsShowTime = zobrazit }
            .GetCellText(z, StopFields.TravelTimeVariantPrefix + "0", projeta);

    [Fact]
    public void VychozeSeCasProjeteZastavkyNetiskne()
    {
        // Data dávají u projetých záporný čas (kumulativní od vývěskové zastávky).
        Assert.Equal("", VariantniCas(false, StopSCasy(0, "-7"), projeta: true));
        Assert.Equal("", VariantniCas(false, StopSCasy(0, "4"), projeta: true));
    }

    [Fact]
    public void NeprojetaZastavkaCasySiDrzi()
    {
        var z = StopSCasy(12, "12");
        Assert.Equal("12", VariantniCas(false, z, projeta: false));
        Assert.Equal("12", VariantniCas(true, z, projeta: false));
    }

    [Fact]
    public void ZobrazitZachovaDosavadniChovani()
    {
        var z = StopSCasy(0, "-7");
        Assert.Equal("-7", VariantniCas(true, z, projeta: true));
    }

    /// <summary>`B148` - vypnuté „Projeté: Zobrazit čas" vyprázdní buňku celou, i značku „&lt;".
    /// Do 11. 9. se zahazovala jen čísla, takže sloupec „Čas" byl u projetých prázdný a variantní
    /// vedle něj plný „&lt;" (hlásil TRANSDEV). Se „Zobrazit" značka zůstává.</summary>
    [Fact]
    public void ZnackaProjizdiSeUProjetychNetiskne()
    {
        var z = StopSCasy(0, "<");
        Assert.Equal("", VariantniCas(false, z, projeta: true));
        Assert.Equal("<", VariantniCas(true, z, projeta: true));
    }

    [Fact]
    public void ZakladniSloupecCasu_SeChovaStejneJakoVariantni()
    {
        var el = new StopListElement { PassedStopsShowTime = false };
        Assert.Equal("", el.GetCellText(StopSCasy(9), StopFields.TravelTime, isPassed: true));
        Assert.Equal("9", el.GetCellText(StopSCasy(9), StopFields.TravelTime, isPassed: false));
    }

    /// <summary>Volba „Zobrazit" u projetých zastávek platí i pro základní sloupec. Ten do 13. 8.
    /// zahazoval záporný čas dřív, než se k němu volba dostala, takže zůstal prázdný, i když si
    /// uživatel zobrazení výslovně zapnul - a variantní sloupce vedle něj číslo měly.</summary>
    [Fact]
    public void ZakladniSloupecCasu_ZobrazitPlatiIProNej()
    {
        var el = new StopListElement { PassedStopsShowTime = true };
        var z = StopSCasy(0, "-7");
        z.CasJizdy = "-7";

        Assert.Equal("-7", el.GetCellText(z, StopFields.TravelTime, isPassed: true));
        Assert.Equal("-7", el.GetCellText(z, StopFields.TravelTimeVariantPrefix + "0", isPassed: true));
    }

    // ── Tři stavy základního sloupce časů (B67) ───────────────────────────────
    // Sloupec „Čas jízdy" nese totéž co variantní: číslo, „<" (základní trasa projíždí bez stání)
    // nebo prázdno (mimo základní trasu). Dřív bylo prázdno i pro projíždění, takže z jediného
    // sloupce nebylo poznat, že tudy spoj vůbec jede.

    private static string ZakladniCas(string raw) =>
        new StopListElement().GetCellText(new ZastavkaData { Popis = "Zastávka", CasJizdy = raw }, StopFields.TravelTime);

    [Fact]
    public void ZakladniCas_ProjizdiSeTiskneZnackou()
    {
        Assert.Equal("<", ZakladniCas("<"));
    }

    [Fact]
    public void ZakladniCas_MimoTrasuJePrazdno()
    {
        Assert.Equal("", ZakladniCas(""));
    }

    /// <summary>Nula se tiskne. Prázdno v seznamu znamená „tudy tenhle průběh nejede", takže
    /// potlačená nula dělala ze zastávky s jízdou pod minutu neobsluhovanou - a hned vedle měla
    /// ve variantním sloupci „0" (nález Jan 13. 8. na lince 9, zastávka Bizetova). Kdo chce místo
    /// nuly čitelnější tvar, zapne si „Jízda pod minutu jako &lt;1".</summary>
    [Fact]
    public void ZakladniCas_NulaSeTiskne()
    {
        Assert.Equal("0", ZakladniCas("0"));
    }

    /// <summary>Základní sloupec musí dát na tutéž hodnotu totéž co variantní - rozdílné zacházení
    /// je vidět vedle sebe na jednom řádku.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("12")]
    [InlineData("<")]
    [InlineData("")]
    public void ZakladniCas_JeShodnySeSloupcemVarianty(string hodnota)
    {
        var el = new StopListElement();
        var z = new ZastavkaData { Popis = "Zastávka", CasJizdy = hodnota, TrasaMin = [hodnota] };

        Assert.Equal(
            el.GetCellText(z, StopFields.TravelTimeVariantPrefix + "0"),
            el.GetCellText(z, StopFields.TravelTime));
    }

    [Fact]
    public void ZakladniCas_CisloProjde()
    {
        Assert.Equal("12", ZakladniCas("12"));
    }
}
