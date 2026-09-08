using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Slučování přípojných řádků podle směru.
/// </summary>
/// <remarks>
/// Starý tisk dává přípoje téhož směru a druhu dopravy na <b>jeden řádek</b> s popiskem
/// „ze směru X" a časy rozdělí do sloupců podle spoje (`JRapiLinPrtWin01.mac:89`). My jsme dělali
/// řádek per spoj, takže u uzlu s pěti spoji z jednoho směru vyšlo pět řádků tam, kde starý má
/// jeden.
///
/// <para>Rozhodnutí Jan 4. 9.: <b>šetření místa váží víc</b> než to, aby cestující věděl, kterou
/// linkou přípoj přijede - potřebuje čas.</para>
///
/// <para>⚠️ Slučování mění <b>počet řádků</b>, tedy i výšku tabulky a kam padne řez stránky. Proto
/// se tu měří výška: kdyby ji počítala jiná cesta než ta, která kreslí, tabulka by se rozešla
/// s výpočtem - to už se u tohohle prvku jednou stalo.</para>
/// </remarks>
public class PripojeSlucovaniTests
{
    private const float VyskaRadku = 5f;

    private static (string, object?) Pripoj(string smer, string linka, string cas,
        string spoj = "21", string avl = "A") =>
        ("PRIPOJE", RenderSnapshot.Nested(
            [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
              ("CAS", cas), ("LINKA", linka), ("SPOJ", spoj), ("TEXT", smer), ("AVL", avl)]]));

    /// <summary>
    /// Tři zastávky; u prostřední mají tři spoje přípoj - dva ze stejného směru a druhu dopravy
    /// (jen jinou linkou), třetí z jiného směru.
    /// </summary>
    private static DataContext Linka() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Český Krumlov"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Velešín, aut.nádr."), ("PORADI", "2")),
            RenderSnapshot.Row(("ID", "3"), ("NAZEV_ZASTAVKY", "Kaplice"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"), Pripoj("Trhové Sviny", "320163", "10:25")),
            RenderSnapshot.Row(("CISLO_SPOJE", "4"), ("SKUPINA", "1"), Pripoj("Trhové Sviny", "320999", "12:25")),
            RenderSnapshot.Row(("CISLO_SPOJE", "6"), ("SKUPINA", "1"), Pripoj("České Budějovice", "330530", "14:25")),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static LineTableElement Prvek(PripojSlucovani rezim) => new()
    {
        X = 0, Y = 0, Width = 190,
        DataRowHeight = VyskaRadku,
        ShowPripojeRows = true,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
        PripojeSlucovani = rezim,
    };

    /// <summary>Kolik přípojných řádků vznikne - rozdíl výšky proti tabulce bez přípojů.</summary>
    private static float PocetPripojnychRadku(PripojSlucovani rezim, DataContext? data = null)
    {
        data ??= Linka();
        var prvek = Prvek(rezim);
        var bez = Prvek(rezim);
        bez.ShowPripojeRows = false;
        return (prvek.CalculateHeightFromData(data) - bez.CalculateHeightFromData(data)) / VyskaRadku;
    }

    [Fact]
    public void BezSlucovani_RadekProKazdySpoj()
    {
        Assert.Equal(3f, PocetPripojnychRadku(PripojSlucovani.Ne), precision: 2);
    }

    [Fact]
    public void PodleSmeru_DvaStejneSmeryNaJednomRadku()
    {
        // Dva přípoje z Trhových Svinů (různé linky) se sejdou na jednom řádku, České Budějovice
        // mají vlastní - ze tří řádků jsou dva.
        Assert.Equal(2f, PocetPripojnychRadku(PripojSlucovani.PodleSmeru), precision: 2);
    }

    [Fact]
    public void PodleSmeruALinky_RozdilneLinkyNesloucuje()
    {
        // Týž směr, ale jiná linka → vlastní řádek, tedy zpátky tři.
        Assert.Equal(3f, PocetPripojnychRadku(PripojSlucovani.PodleSmeruALinky), precision: 2);
    }

    [Fact]
    public void VychoziRezimJePodleSmeru()
    {
        // ⚠️ Výchozí se uplatní i na šabloně uložené dřív (hodnotu nemá) - je to záměr, dnešní tvar
        // byl odchylka od starého tisku, ne volba (rozhodnutí Jan 4. 9.).
        Assert.Equal(PripojSlucovani.PodleSmeru, new LineTableElement().PripojeSlucovani);
    }

    [Fact]
    public void RozdilnyDruhDopravy_SeNesloucuje()
    {
        // Autobus a vlak téhož směru patří na dva řádky - jinak by popisek neřekl, čím to jede.
        // Starý modul má druh dopravy v klíči vždy.
        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "A"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "B"), ("PORADI", "2")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"), Pripoj("Praha", "1", "10:25", avl: "A")),
                RenderSnapshot.Row(("CISLO_SPOJE", "4"), ("SKUPINA", "1"), Pripoj("Praha", "1", "11:25", avl: "V")),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

        Assert.Equal(2f, PocetPripojnychRadku(PripojSlucovani.PodleSmeru, data), precision: 2);
    }

    [Fact]
    public void DvaPripojeTehozSpoje_DostanouDvaRadky()
    {
        // ⚠️ Jeden řádek nese od jednoho spoje jen jeden čas, takže druhý přípoj musí založit řádek
        // další - jinak by se tiše přebil a jeden čas by z tabulky zmizel.
        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "A"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "B"), ("PORADI", "2")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "2"), ("SKUPINA", "1"),
                    ("PRIPOJE", RenderSnapshot.Nested(
                        [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                          ("CAS", "10:25"), ("LINKA", "1"), ("SPOJ", "21"), ("TEXT", "Praha"), ("AVL", "A")],
                         [("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                          ("CAS", "10:55"), ("LINKA", "1"), ("SPOJ", "23"), ("TEXT", "Praha"), ("AVL", "A")]]))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

        Assert.Equal(2f, PocetPripojnychRadku(PripojSlucovani.PodleSmeru, data), precision: 2);
    }
}
