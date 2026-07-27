using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// KOVED #13048 bod 4 — režim „Duplikace řádku per stanoviště" (<see cref="StopListElement.ExpandPlatformRows"/>).
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

    [Fact]
    public void JinyRezim_NechaSeznamBezeZmeny()
    {
        var stops = new List<ZastavkaData> { Stop("A", false, ["1"], ["2"]) };
        var vysledek = new StopListElement { MultiPlatformMode = MultiPlatformDisplay.All }
            .ExpandPlatformRows(stops);
        Assert.Same(stops, vysledek);
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
