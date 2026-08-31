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
}
