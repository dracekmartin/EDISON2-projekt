using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Filtr výlukových poznámek na prvku legendy (#13093 bod 1). Klient chce výluky na vývěsce zvlášť
/// od ostatních poznámek linky, takže si položí dva bloky: jeden „Bez výluk", druhý „Jen výluky".
/// Kategorii dává Caché (<c>vyluka</c>), prvek podle ní jen filtruje.
/// </summary>
public class NotesLegendVylukyTests
{
    private static List<PoznamkaData> Vzorek() =>
    [
        new() { Typ = TypPoznamky.Text, Text = "Na lince platí Tarif IDZK", Kategorie = "poznamka-linky-tarifni" },
        new() { Typ = TypPoznamky.Text, Text = "R - k jízdence lze zakoupit místenku", Kategorie = "poznamka-linky-vseobecne" },
        new() { Typ = TypPoznamky.Text, Text = "Výluka: Staré Město, hřbitov zrušena", Kategorie = "vyluka" },
        new() { Typ = TypPoznamky.Text, Text = "Výluka: objízdná trasa I/50", Kategorie = "vyluka" },
        new() { Typ = TypPoznamky.SpecialniZnak, Znak = "x", Text = "na znamení", Kategorie = "atribut-zastavky" },
    ];

    private static List<string> Texty(VylukyFiltr filtr) =>
        [.. new NotesLegendElement { Vyluky = filtr }.FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text)];

    /// <summary>Výchozí stav = dosavadní chování, jeden blok se vším.</summary>
    [Fact]
    public void Vcetne_PropustiVse()
    {
        Assert.Equal(5, Texty(VylukyFiltr.Vcetne).Count);
    }

    [Fact]
    public void BezVyluk_VynechaJenVylukoveKategorie()
    {
        var texty = Texty(VylukyFiltr.Bez);

        Assert.Equal(3, texty.Count);
        Assert.DoesNotContain(texty, t => t.StartsWith("Výluka"));
        Assert.Contains("na znamení", texty);   // ostatní kategorie filtr nesmí sebrat
    }

    [Fact]
    public void JenVyluky_NechaJenJe()
    {
        var texty = Texty(VylukyFiltr.Jen);

        Assert.Equal(2, texty.Count);
        Assert.All(texty, t => Assert.StartsWith("Výluka", t));
    }

    // ── Výběr kategorií poznámek linky (#13093 bod 1: „příp. filtrovat Poznámky 1, Poznámky 2?") ──

    /// <summary>Klient chce jednotlivé kategorie poznámek linky rozložit do víc bloků (mají jiné
    /// rozložení sloupců než seznam značek). Vypnutá kategorie zmizí, ostatní zůstanou.</summary>
    [Fact]
    public void VypnutaKategorieLinky_ZmiziJenOna()
    {
        var texty = new NotesLegendElement { ShowPoznamkyLinkyTarifni = false }
            .FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text).ToList();

        Assert.DoesNotContain(texty, t => t.StartsWith("Na lince platí"));
        Assert.Contains("R - k jízdence lze zakoupit místenku", texty);   // jiná kategorie linky zůstává
        Assert.Contains("na znamení", texty);
        Assert.Equal(4, texty.Count);
    }

    /// <summary>Dvojice bloků: v jednom jen tarifní, ve druhém jen všeobecné. To je ten scénář,
    /// kvůli kterému o filtr žádal.</summary>
    [Fact]
    public void LzeNechatJenJednuKategoriiLinky()
    {
        var jenTarifni = new NotesLegendElement
        {
            ShowPoznamkyLinkyVseobecne = false,
            ShowPoznamkyLinkyRadiobus = false,
            Vyluky = VylukyFiltr.Bez,
            ShowSpecialniZnaky = false,
        }.FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text).ToList();

        Assert.Equal(["Na lince platí Tarif IDZK"], jenTarifni);
    }

    /// <summary>Filtr kategorií se **nesmí** dotknout poznámek, které z linky nejsou (značky, omezení,
    /// ruční komentáře) - jinak by vypnutá kategorie sebrala jako vedlejší efekt i je.</summary>
    [Fact]
    public void KategorieLinky_NesahajiNaOstatniPoznamky()
    {
        var vse = new NotesLegendElement
        {
            ShowPoznamkyLinkyTarifni = false,
            ShowPoznamkyLinkyVseobecne = false,
            ShowPoznamkyLinkyRadiobus = false,
        }.FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text).ToList();

        Assert.Contains("na znamení", vse);
        Assert.Equal(3, vse.Count);   // zůstaly obě výluky + značka, zmizely jen dvě poznámky linky
    }

    /// <summary>Výchozí stav nesmí nic schovat - jinak by se existujícím šablonám změnil vzhled.</summary>
    [Fact]
    public void VychoziStav_NechavaVsechnyKategorieLinky()
    {
        var e = new NotesLegendElement();

        Assert.True(e.ShowPoznamkyLinkyTarifni);
        Assert.True(e.ShowPoznamkyLinkyVseobecne);
        Assert.True(e.ShowPoznamkyLinkyRadiobus);
        Assert.Equal(5, e.FiltrujPoznamkyProTest(Vzorek()).Count);
    }

    /// <summary>MHD kategorie neposílá - prázdná kategorie nesmí spadnout do výluk, jinak by se
    /// při volbě „Bez výluk" vytratily poznámky agend, které o kategoriích nevědí.</summary>
    [Fact]
    public void PrazdnaKategorie_SeChovaJakoNevylukova()
    {
        var bezKategorie = new List<PoznamkaData>
        {
            new() { Typ = TypPoznamky.Text, Text = "MHD poznámka", Kategorie = "" },
        };

        Assert.Single(new NotesLegendElement { Vyluky = VylukyFiltr.Bez }.FiltrujPoznamkyProTest(bezKategorie));
        Assert.Empty(new NotesLegendElement { Vyluky = VylukyFiltr.Jen }.FiltrujPoznamkyProTest(bezKategorie));
    }
}
