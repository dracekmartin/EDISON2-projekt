using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// KOVED #13048 bod 4 - režim „Duplikace řádku per stanoviště" (<see cref="StopListElement.ExpandPlatformRows"/>).
/// Zastávka obsluhovaná z více stanovišť (různá NASTUPISTE_VARIANT napříč variantami) se rozvine na N
/// řádků; časy zůstanou jen u variant daného stanoviště; vývěska a jednostanovištní zastávka = 1 řádek.
/// </summary>
public class StopListPlatformDuplicationTests
{
    private static ZastavkaData Stop(string name, bool current, string[] trasaMin, string[] nastVar) =>
        new()
        {
            Popis = name,
            JeAktualni = current,
            TrasaMin = [.. trasaMin],
            NastupisteVariant = [.. nastVar],
        };

    private static List<ZastavkaData> Expand(params ZastavkaData[] stops) =>
        new StopListElement { MultiPlatformMode = MultiPlatformDisplay.RowPerPlatform }
            .ExpandPlatformRows([.. stops]);

    /// <summary>Zastávka **před** vývěskou (projetá) se rozpadá stejně jako ta za ní. Nitra 9,
    /// vývěska Fraňa Mojtu: CENTRUM, Mlyny má dvě stanoviště, ale leží v projeté části - a rozpad
    /// tam nebyl vidět (nahlásil Jan 13. 8.). Testy do té doby kryly jen zastávky za vývěskou.</summary>
    [Fact]
    public void ZastavkaPredVyveskou_SeRozviTaky()
    {
        var r = Expand(
            Stop("CENTRUM, Mlyny", current: false, ["-9", "-9"], ["A", "B"]),
            Stop("Fraňa Mojtu", current: true, ["0", "0"], ["B", "B"]),
            Stop("Predmostie", current: false, ["2", "2"], ["B", "B"]));

        Assert.Equal(["CENTRUM, Mlyny", "CENTRUM, Mlyny", "Fraňa Mojtu", "Predmostie"], r.Select(z => z.Popis));
    }

    /// <summary>Po přepnutí na sloupek se řádky rozvíjejí podle sloupků variant a hodnota jde do
    /// sloupce Sloupek; označník zůstane, jak byl. Jinak by duplikované řádky nesly cizí číslo
    /// (KOVED #13088).</summary>
    [Fact]
    public void PoPrepnutiNaSloupek_RozviRadkyPodleSloupku()
    {
        var s = Stop("Medlovice", current: false, ["5", "6"], ["9", "9"]);
        s.Nastupiste = "9";
        s.SloupekVariant = ["3", "2"];

        var r = new StopListElement
        {
            MultiPlatformMode = MultiPlatformDisplay.RowPerPlatform,
            SloupekJakoStanoviste = true,
        }.ExpandPlatformRows([s]);

        Assert.Equal(2, r.Count);
        Assert.Equal(["3", "2"], r.Select(x => x.Sloupek));
        Assert.All(r, x => Assert.Equal("9", x.Nastupiste));   // označník se nepřepisuje
        Assert.Equal(["5", ""], r[0].TrasaMin);                // časy jen varianty sloupku 3
        Assert.Equal(["", "6"], r[1].TrasaMin);
    }

    /// <summary>Základní čas jízdy není vázaný na stanoviště (je to doba linky), takže zůstane jen na
    /// prvním z rozvinutých řádků - stejně jako značka „projíždí". Zopakovaný na každém řádku vypadá
    /// jako chyba dat (B67, sloupec časů se od variant oddělil a je vidět vždy).</summary>
    [Fact]
    public void ZakladniCasZustaneJenNaPrvnimRadku()
    {
        var s = Stop("Medlovice", current: false, ["5", "6"], ["1", "2"]);
        s.CasJizdy = "5";

        var r = Expand(s);

        Assert.Equal(2, r.Count);
        Assert.Equal(["5", ""], r.Select(x => x.CasJizdy));
    }

    /// <summary>Bez přepnutí se sloupky na řádky nerozvíjejí, i když je v datech má - řídí to
    /// označník, jak to bylo dosud.</summary>
    [Fact]
    public void BezPrepnuti_SloupkyRadkyNerozvineji()
    {
        var s = Stop("Medlovice", current: false, ["5", "6"], ["9", "9"]);
        s.SloupekVariant = ["3", "2"];

        var r = Expand(s);

        Assert.Single(r);   // označníky jsou shodné (9, 9) → jeden řádek
    }

    [Fact]
    public void JinyRezim_NechaSeznamBezeZmeny()
    {
        var stops = new List<ZastavkaData> { Stop("A", false, ["1"], ["2"]) };
        var vysledek = new StopListElement { MultiPlatformMode = MultiPlatformDisplay.All }
            .ExpandPlatformRows(stops);
        Assert.Same(stops, vysledek);
    }

    [Fact]
    public void BezVariantnichSloupcu_SeNerozpada()
    {
        // Skrytý sloupec „Varianty tras" = jediný sloupec času (stejný na všech duplikátech)
        // → rozpad by vypadal jako chyba; režim se pak neaplikuje.
        var stops = new List<ZastavkaData> { Stop("A", false, ["5", "6"], ["1", "2"]) };
        var el = new StopListElement { MultiPlatformMode = MultiPlatformDisplay.RowPerPlatform };
        el.Columns.Single(c => c.Key == StopFields.TravelTimeVariants).Visible = false;

        Assert.Same(stops, el.ExpandPlatformRows(stops));
    }

    /// <summary>Rozpad na řádky visí na sloupci variant, ne na sloupci „Čas jízdy" - ten si smí
    /// uživatel skrýt, aniž tím tiše vypne něco jiného (past zrušená v `B80`).</summary>
    [Fact]
    public void SkrytySloupecCasu_RozpadNezrusi()
    {
        var stops = new List<ZastavkaData> { Stop("A", false, ["5", "6"], ["1", "2"]) };
        var el = new StopListElement { MultiPlatformMode = MultiPlatformDisplay.RowPerPlatform };
        el.Columns.Single(c => c.Key == StopFields.TravelTime).Visible = false;

        Assert.Equal(2, el.ExpandPlatformRows(stops).Count);
    }

    [Fact]
    public void DownstreamSViceStanovisti_SeRozviNaRadkyVPoradiPrvnihoVyskytu()
    {
        // Varianty na stanovištích 3, 2, 4 (pořadí prvního výskytu = 3, 2, 4), časy 5/6/7/8.
        var s = Stop("Medlovice", current: false, ["5", "6", "7", "8"], ["3", "2", "4", "3"]);
        var r = Expand(s);

        Assert.Equal(3, r.Count);
        Assert.Equal("3", r[0].Nastupiste);   // první výskyt = 3
        Assert.Equal("2", r[1].Nastupiste);
        Assert.Equal("4", r[2].Nastupiste);

        // Řádek stanoviště 3 → časy jen sloupců s nv==3 (index 0 a 3), ostatní prázdné.
        Assert.Equal(["5", "", "", "8"], r[0].TrasaMin);
        // Stanoviště 2 → jen index 1.
        Assert.Equal(["", "6", "", ""], r[1].TrasaMin);
        // Stanoviště 4 → jen index 2.
        Assert.Equal(["", "", "7", ""], r[2].TrasaMin);
    }

    [Fact]
    public void VyveskovaZastavka_ZustaneJednimRadkem()
    {
        var s = Stop("Vývěska", current: true, ["0", "0", "0"], ["3", "2", "4"]);
        var r = Expand(s);
        Assert.Single(r);
        Assert.Same(s, r[0]);   // aktuální se nemění
    }

    [Fact]
    public void JednoStanoviste_ZustaneJednimRadkem()
    {
        var s = Stop("Běžná", current: false, ["5", "6"], ["2", "2"]);
        var r = Expand(s);
        Assert.Single(r);
        Assert.Same(s, r[0]);
    }

    [Fact]
    public void ProjizdejiciVarianta_ZnackaZustaneNaPrvnimRadku()
    {
        // Varianta na indexu 1 zastávkou jen projíždí („<", bez stanoviště nv=""). Platformy 3, 2.
        var s = Stop("Křižovatka", current: false, ["5", "<", "6"], ["3", "", "2"]);
        var r = Expand(s);

        Assert.Equal(2, r.Count);
        // Řádek stanoviště 3 (první) → čas své varianty (5) + zachovaná značka „<" projíždějící varianty.
        Assert.Equal(["5", "<", ""], r[0].TrasaMin);
        // Řádek stanoviště 2 (další) → jen svůj čas (6); „<" se neopakuje.
        Assert.Equal(["", "", "6"], r[1].TrasaMin);
    }

    [Fact]
    public void Znacky_JenNaPrvnimRadku()
    {
        var s = Stop("Nádraží", current: false, ["5", "6"], ["3", "2"]);
        s.NaZnameni = true;
        s.Atributy = [new AtributData { Znak = "L", Popis = "vlak" }];
        var r = Expand(s);

        Assert.Equal(2, r.Count);
        Assert.True(r[0].NaZnameni);
        Assert.Single(r[0].Atributy);
        Assert.False(r[1].NaZnameni);      // duplikát bez značek (šum + duplicita v legendě)
        Assert.Empty(r[1].Atributy);
    }
}
