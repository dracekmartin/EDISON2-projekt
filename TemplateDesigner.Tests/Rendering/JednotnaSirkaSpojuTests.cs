using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Volba „Stejně široké sloupce spojů" - sloupec se značkou návaznosti je jinak širší než ostatní
/// a tabulka působí roztřepeně.
/// </summary>
/// <remarks>
/// Testuje se přes <c>Rozvrzeni</c>, tedy nad daty a se skutečnými šířkami - ne dosazením do vzorce.
/// Důvod je tentýž jako u <see cref="LineTableBlokyTests"/>: rozřezání a šířky se rozešly právě tam,
/// kde se počítalo z jiných vstupů, než jaké jdou do kreslení.
/// </remarks>
public class JednotnaSirkaSpojuTests
{
    /// <summary>Linka, kde má značku návaznosti jen část spojů - jinak by rozdíl nebyl vidět.
    /// <paramref name="znackyJenPrvnich"/> omezí značky na začátek linky, takže pozdější bloky
    /// žádnou nemají.</summary>
    private static DataContext LinkaSeZnackami(int pocetSpoju, int znackaKazdy = 5,
        int znackyJenPrvnich = int.MaxValue)
    {
        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i =>
            {
                var pole = new List<(string, object?)>
                {
                    ("CISLO_SPOJE", (i * 2).ToString()),
                    ("SKUPINA", "1"),
                    // Značka se počítá jen tam, kde je i čas - bez časů by fixtura neuměla
                    // vyrobit různě široké sloupce a test by neověřoval nic.
                    ("CASY", new JsonArray("8:00", "8:20", "8:40")),
                };
                if (i % znackaKazdy == 0 && i <= znackyJenPrvnich)
                    pole.Add(("PRIPOJE", RenderSnapshot.Nested(
                        [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                          ("CAS", "12:30"), ("LINKA", "330520"), ("SPOJ", "7"), ("TEXT", "Kaplice")]])));
                return RenderSnapshot.Row([.. pole]);
            })
            .ToArray();
        var zastavky = Enumerable.Range(1, 3)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}, rozcestí"),
                                            ("PORADI", i.ToString())))
            .ToArray();
        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    private static LineTableElement Prvek(bool jednotna)
    {
        var el = ElementRenderFixtures.LineTable();
        el.JednotnaSirkaSpoju = jednotna;
        return el;
    }

    /// <summary>
    /// ⚠️ Podstata: se zapnutou volbou jsou sloupce širší, takže se jich do řádku vejde méně.
    /// Kdyby se rezerva na značky nepřepočítala, blok by se spočítal užší, než se nakreslí,
    /// a přetekl by přes hranu prvku - táž třída chyby jako `B122`.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(41)]
    [InlineData(120)]
    public void KazdyBlokSeVejdeDoSirky(int pocetSpoju)
    {
        var prvek = Prvek(jednotna: true);

        var bloky = prvek.Rozvrzeni(LinkaSeZnackami(pocetSpoju));

        Assert.NotEmpty(bloky);
        Assert.All(bloky, b => Assert.True(b.SirkaMm <= prvek.Width + 0.01f,
            $"blok od {b.Od} ({b.Pocet} sloupců) je {b.SirkaMm:0.0} mm, prvek má {prvek.Width} mm"));
    }

    /// <summary>
    /// Zapnutá volba sloupce rozšíří (nese místo na značku i tam, kde žádná není), takže se jich
    /// do řádku vejde méně. Bez toho by volba nic nedělala.
    /// </summary>
    [Fact]
    public void ZapnutaVolbaRozsiriSloupceATimJichDoRadkuVejdeMene()
    {
        var data = LinkaSeZnackami(60);

        var bez = Prvek(jednotna: false).Rozvrzeni(data);
        var s   = Prvek(jednotna: true).Rozvrzeni(data);

        // Bez volby jsou sloupce různě široké, s volbou všechny stejné.
        // ⚠️ Šířky se mezi oběma nastaveními **neporovnávají**: se zapnutou volbou se do bloku vejde
        // méně sloupců, takže se zbylá šířka dělí jinak a přepočítá se i základ sloupce. Porovnávat
        // konkrétní milimetry napříč nastaveními by měřilo dvě různá rozvržení.
        Assert.True(bez[0].SirkySloupcu.Distinct().Count() > 1, "fixtura musí mít různě široké sloupce");
        Assert.Single(s[0].SirkySloupcu.Distinct());
        // Všechny sloupce dostanou šířku toho nejširšího, takže zabere víc místa a spojů se do
        // bloku vejde nejvýš tolik co bez volby.
        Assert.Equal(bez[0].SirkySloupcu.Max(), s[0].SirkySloupcu[0], precision: 2);
        Assert.True(s[0].Pocet <= bez[0].Pocet,
            $"se srovnanými sloupci se vešlo {s[0].Pocet} spojů proti {bez[0].Pocet} bez volby");
    }

    /// <summary>
    /// Linka, kde značku nemá žádný spoj: volba nesmí nic změnit. Jinak by zbytečně zúžila
    /// tabulku každému, kdo si ji zapne „pro jistotu".
    /// </summary>
    [Fact]
    public void BezZnacekVolbaNicNemeni()
    {
        var data = LinkaSeZnackami(40, znackaKazdy: int.MaxValue);

        var bez = Prvek(jednotna: false).Rozvrzeni(data);
        var s   = Prvek(jednotna: true).Rozvrzeni(data);

        Assert.Equal(bez.Count, s.Count);
        Assert.Equal(bez[0].Pocet, s[0].Pocet);
        Assert.Equal(bez[0].SirkySloupcu.Max(), s[0].SirkySloupcu.Max(), 3);
    }

    /// <summary>
    /// Šířka sloupce musí být stejná ve **všech** blocích - bloky se kreslí pod sebe a zastávky
    /// v nich musí svisle sedět. Proto se nejširší značkový sloupec bere přes celou linku, ne
    /// přes blok: kdyby se počítal per blok, blok bez značek by měl užší sloupce než blok se
    /// značkami a tabulka by se pod sebou rozjela.
    /// </summary>
    [Fact]
    public void SirkaSloupceJeStejnaVeVsechBlocich()
    {
        // ⚠️ Značky **jen u prvních tří spojů**: bloky dál po lince žádnou nemají, takže kdyby se
        // nejširší bral per blok, vyšly by užší. S fixturou, která má značky rozeseté po celé lince,
        // je per-blok i přes-linku maximum totéž a test by neověřoval nic (odhaleno mutací).
        var data = LinkaSeZnackami(80, znackaKazdy: 1, znackyJenPrvnich: 3);

        var bloky = Prvek(jednotna: true).Rozvrzeni(data);

        Assert.True(bloky.Count > 1, "fixtura musí vyrobit víc bloků");
        float sirka = bloky[0].SirkySloupcu[0];
        Assert.All(bloky, b => Assert.All(b.SirkySloupcu, w => Assert.Equal(sirka, w, 3)));
    }
}
