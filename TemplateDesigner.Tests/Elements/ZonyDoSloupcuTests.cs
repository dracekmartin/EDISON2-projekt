using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Zóny hraniční zastávky každá ve vlastním sloupci (`N1`, #8576 KU HK).
/// </summary>
/// <remarks>
/// <para>Zastávka na hranici zón nese všechny své zóny v jedné hodnotě. Zákazník s IDS je chce
/// vedle sebe, jako to umí kilometry.</para>
/// <para>⚠️ <b>Starý modul to neumí</b> - zóny do sloupců nedělí, má na ně jeden sloupec, který jen
/// zesílí podle délky obsahu. Není to tedy parita, ale nová věc, a proto je tu nejdůležitější test
/// ten, který hlídá <b>nezměněné chování s vypnutou volbou</b>: víc zón má 300 tisíc zastávek
/// měřených firem, takže by se jinak překreslily hotové sestavy i tomu, kdo o to nežádal.</para>
/// </remarks>
public class ZonyDoSloupcuTests
{
    /// <summary>Dvě zastávky: jedna hraniční se třemi zónami, druhá s jednou.</summary>
    private static DataContext Data(string zonaPrvni = "037,066,051") => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("PORADI", "1"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "Kadov,Pole"),
                ("ZONY", zonaPrvni), ("TC", "1")),
            RenderSnapshot.Row(("PORADI", "2"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "Lnáře"),
                ("ZONY", "066"), ("TC", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T")),
        ]));

    private static LineTableElement Prvek(bool rozdelit) => new()
    {
        X = 0, Y = 0, Width = 120f, Height = 60f,
        RozdelitZonyDoSloupcu = rozdelit,
    };

    private static List<(string Klic, string Hodnota)> Zony(bool rozdelit, string zonaPrvni = "037,066,051") =>
        [.. Prvek(rozdelit).PopisneSloupceProTest(Data(zonaPrvni)).Where(c => c.Klic == ColumnKeys.Zone)];

    /// <summary>
    /// <b>Vypnutá volba = přesně to, co bylo dosud</b>: jeden sloupec a v něm celá hodnota z dat.
    /// </summary>
    /// <remarks>
    /// Tohle je ta pojistka, kvůli které je volba vůbec vypínatelná. Kdyby se rozpad choval jako
    /// výchozí, změnil by tisk 300 tisícům zastávek, které mají víc zón - a nikdo z těch zákazníků
    /// o to nežádal.
    /// </remarks>
    [Fact]
    public void VypnutaVolba_JedenSloupecSCelouHodnotou()
    {
        var zony = Zony(rozdelit: false);

        Assert.Single(zony);
        Assert.Equal("037,066,051", zony[0].Hodnota);
    }

    /// <summary>Zapnutá volba rozpadne zónu na tolik sloupců, kolik jich má nejbohatší zastávka.</summary>
    [Fact]
    public void ZapnutaVolba_KazdaZonaVlastnimSloupcem()
    {
        var zony = Zony(rozdelit: true);

        Assert.Equal(3, zony.Count);
        Assert.Equal(["037", "066", "051"], zony.Select(z => z.Hodnota));
    }

    /// <summary>
    /// Zastávka s jednou zónou nechá zbylé sloupce prázdné - nedoplňuje se do nich nic cizího.
    /// </summary>
    [Fact]
    public void ZastavkaSJednouZonou_ZbylesSloupceZustanouPrazdne()
    {
        var sloupce = Prvek(rozdelit: true).PopisneSloupceProTest(Data(), radek: 1);
        var zony = sloupce.Where(c => c.Klic == ColumnKeys.Zone).ToList();

        Assert.Equal(3, zony.Count);
        Assert.Equal(["066", "", ""], zony.Select(z => z.Hodnota));
    }

    /// <summary>
    /// Dělí se čárkou i lomítkem.
    /// </summary>
    /// <remarks>
    /// ⚠️ Není to opatrnost, ale měření (31. 8., šest firem): čárka 301 334×, lomítko 404× - lomítko
    /// používá KV_DP („23/5"). Kdyby se dělilo jen čárkou, u téhle firmy by volba tiše nedělala nic.
    /// </remarks>
    [Fact]
    public void DeliSeCarkouILomitkem()
    {
        var zony = Zony(rozdelit: true, zonaPrvni: "23/5");

        Assert.Equal(2, zony.Count);
        Assert.Equal(["23", "5"], zony.Select(z => z.Hodnota));
    }

    // ── Nadpis nad rozpadlými zónami ─────────────────────────────────────────

    /// <summary>
    /// Rozpadlé zóny mají <b>jeden společný nadpis</b> přes všechny své díly, ne „Zóna" nad každým.
    /// </summary>
    /// <remarks>
    /// Nález Jan 1. 9. při zkoušení: opakovaný nadpis vypadal jako několik různých sloupců, přestože
    /// je to pořád táž věc, jen rozložená. Sloučení je jedna širší buňka, takže v řádku nadpisu
    /// zároveň zmizí čáry mezi díly.
    /// </remarks>
    [Fact]
    public void RozpadleZony_MajiJedenSpolecnyNadpis()
    {
        var nadpisy = Prvek(rozdelit: true).NadpisyProTest(Data());

        Assert.Single(nadpisy, n => n.Text == "Zóna");
    }

    /// <summary>Sloučený nadpis je široký jako všechny díly dohromady - jinak by nesedělo záhlaví.</summary>
    [Fact]
    public void SloucenyNadpis_JeSirokyJakoVsechnyDily()
    {
        var prvek = Prvek(rozdelit: true);
        var sloupcu = prvek.PopisneSloupceProTest(Data()).Count(c => c.Klic == ColumnKeys.Zone);
        var nadpisZona = prvek.NadpisyProTest(Data()).Single(n => n.Text == "Zóna");
        var nadpisTc = prvek.NadpisyProTest(Data()).Single(n => n.Text == "Tč");

        Assert.Equal(3, sloupcu);
        Assert.Equal(8f * 3, nadpisZona.Sirka, 3);   // výchozí šířka sloupce zóny je 8 mm
        Assert.Equal(6f, nadpisTc.Sirka, 3);         // ostatní sloupce se neslučují
    }

    /// <summary>
    /// Kilometry se ve výchozím stavu <b>neslučují</b> - jejich díly jsou různé hodnoty se stejným názvem.
    /// </summary>
    /// <remarks>
    /// ⚠️ Není to opomenutí: sloučení nadpisu km by změnilo hotové sestavy všem, kdo km sloupce mají,
    /// a na rozdíl od zón tam každý díl znamená něco jiného (km jednotlivých úseků). Sloučit je jde
    /// jen volbou <see cref="LineTableElement.JednoZahlaviKm"/>.
    /// </remarks>
    [Fact]
    public void Kilometry_SeNeslucuji()
    {
        var nadpisy = Prvek(rozdelit: true).NadpisyProTest(DataSKm());

        Assert.Equal(2, nadpisy.Count(n => n.Text == "km"));
    }

    /// <summary>
    /// Se zapnutou volbou je nad km sloupci jedno záhlaví přes všechny (#13610, KOVED) - široké jako
    /// všechny km sloupce dohromady, jinak by nesedělo nad hodnoty.
    /// </summary>
    [Fact]
    public void JednoZahlaviKm_JedenNadpisPresVsechnyKmSloupce()
    {
        var prvek = Prvek(rozdelit: false);
        prvek.JednoZahlaviKm = true;

        var nadpisy = prvek.NadpisyProTest(DataSKm());

        var km = Assert.Single(nadpisy, n => n.Text == "km");
        Assert.Equal(6f * 2, km.Sirka, 3);   // výchozí šířka km sloupce je 6 mm
        Assert.Equal(2, prvek.PopisneSloupceProTest(DataSKm()).Count(c => c.Klic == ColumnKeys.Km));
    }

    private static DataContext DataSKm() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("PORADI", "1"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "A"),
                ("ZONY", "037"), ("KM", new JsonArray("0", "5"))),
        ]),
        ("SPOJE", [RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"))]));

    /// <summary>Prázdná zóna nevyrobí sloupec s prázdným textem navíc.</summary>
    [Fact]
    public void PrazdnaZona_NevyrobiSloupec()
    {
        Assert.Empty(LineTableElement.RozdelZony(""));
        Assert.Empty(LineTableElement.RozdelZony("  "));
    }

    /// <summary>Mezery kolem oddělovače se ořežou - v datech se vyskytují oba tvary.</summary>
    [Fact]
    public void MezeryKolemOddelovace_SeOreznou()
    {
        Assert.Equal(["037", "066"], LineTableElement.RozdelZony("037, 066"));
    }

    /// <summary>
    /// Pomlčka (zastávka bez zóny) stojí v <b>každém</b> dílu, ne jen v prvním - jinak druhý díl
    /// vypadal nevyplněný (nález Jan 23. 9. na 823955). Zastávka s jednou zónou má druhý díl
    /// dál prázdný, protože tam zóna je, jen druhá ne.
    /// </summary>
    [Fact]
    public void ZastavkaBezZony_MaPomlckuVKazdemDilu()
    {
        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("PORADI", "1"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "Zlín"),
                    ("ZONY", "-"), ("TC", "1")),
                RenderSnapshot.Row(("PORADI", "2"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "Kroměříž"),
                    ("ZONY", "420,201"), ("TC", "2")),
                RenderSnapshot.Row(("PORADI", "3"), ("TYP", "zastavka"), ("NAZEV_ZASTAVKY", "Hulín"),
                    ("ZONY", "425"), ("TC", "3")),
            ]),
            ("SPOJE", [RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"))]));
        List<string> Zony(int radek) => [.. Prvek(rozdelit: true).PopisneSloupceProTest(data, radek)
            .Where(c => c.Klic == ColumnKeys.Zone).Select(c => c.Hodnota)];

        Assert.Equal(["-", "-"], Zony(0));
        Assert.Equal(["425", ""], Zony(2));
    }
}
