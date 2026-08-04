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

    /// <summary>Výluky jsou všeobecné poznámky linky, takže se rozdělení mezi bloky testuje s nimi
    /// zapnutými - vypnutá kategorie by je schovala celé.</summary>
    private static List<string> Texty(VylukyFiltr filtr) =>
        [.. new NotesLegendElement { Vyluky = filtr, ShowPoznamkyLinkyVseobecne = true }
            .FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text)];

    /// <summary>Se zapnutými všeobecnými projdou jak R, tak obě výluky (+ značka).</summary>
    [Fact]
    public void Vcetne_PropustiVse()
    {
        Assert.Equal(4, Texty(VylukyFiltr.Vcetne).Count);
    }

    [Fact]
    public void BezVyluk_VynechaJenVylukoveKategorie()
    {
        var texty = Texty(VylukyFiltr.Bez);

        Assert.Equal(2, texty.Count);
        Assert.DoesNotContain(texty, t => t.StartsWith("Výluka"));
        Assert.Contains("na znamení", texty);   // ostatní kategorie filtr nesmí sebrat
    }

    [Fact]
    public void JenVyluky_NechaJenJe()
    {
        var texty = Texty(VylukyFiltr.Jen);

        Assert.Equal(2, texty.Count);
        Assert.DoesNotContain("na znamení", texty);
        Assert.All(texty, t => Assert.StartsWith("Výluka", t));
    }

    // ── Výběr kategorií poznámek linky (#13093 bod 1: „příp. filtrovat Poznámky 1, Poznámky 2?") ──

    /// <summary>Klient chce jednotlivé kategorie poznámek linky rozložit do víc bloků (mají jiné
    /// rozložení sloupců než seznam značek). Vypnutá kategorie zmizí, ostatní zůstanou.</summary>
    [Fact]
    public void VypnutaKategorieLinky_ZmiziJenOna()
    {
        var texty = new NotesLegendElement { ShowPoznamkyLinkyTarifni = false, ShowPoznamkyLinkyVseobecne = true }
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
            ShowPoznamkyLinkyTarifni = true,
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

        Assert.Equal(["na znamení"], vse);   // značka zůstala, všechny poznámky linky odešly (i výluky)
    }

    /// <summary>Výchozí stav **netiskne** žádnou poznámku linky - server je teď posílá vždy (dřív je
    /// gatoval parametr sestavy), takže bez tohohle by texty naskočily všem existujícím šablonám.
    /// Výluky jsou na všeobecných, takže odejdou s nimi.</summary>
    [Fact]
    public void VychoziStav_NetiskneZadnouPoznamkuLinky()
    {
        var e = new NotesLegendElement();

        Assert.False(e.ShowPoznamkyLinkyTarifni);
        Assert.False(e.ShowPoznamkyLinkyVseobecne);
        Assert.False(e.ShowPoznamkyLinkyRadiobus);

        var texty = e.FiltrujPoznamkyProTest(Vzorek()).Select(p => p.Text).ToList();
        Assert.Equal(["na znamení"], texty);
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

    // ── Prázdný blok při reálných datech (šablona „Jen výluky" na lince bez výluk) ──

    /// <summary>Blok, jehož filtr při tisku nic nepropustí, se složí na 0 mm - nesmí si držet
    /// 25mm placeholder, který by odtlačil elementy pod ním. Placeholder patří jen designeru.</summary>
    [Fact]
    public void PrazdnyFiltrSRealnymiDaty_SloziBlokNaNulu()
    {
        var blok = new NotesLegendElement { Width = 80, Vyluky = VylukyFiltr.Jen };

        Assert.Equal(0f, blok.CalculateHeightFromData(Rendering.RenderSnapshot.Data(("POZNAMKY", []))));
        Assert.Equal(25f, new NotesLegendElement { Width = 80 }.CalculateHeightFromData(null));
    }

    /// <summary>Prázdný blok při tisku nekreslí vůbec nic - ani nadpis. Šablona se tiskne pro
    /// všechny linky a většina výluku nemá; osiřelý nadpis nad prázdnem by byl na většině vývěsek.</summary>
    [Fact]
    public void PrazdnyFiltrSRealnymiDaty_NekresliAniNadpis()
    {
        var data = Rendering.RenderSnapshot.Data(("POZNAMKY", []));
        var blok = new NotesLegendElement { X = 10, Y = 10, Width = 80, Vyluky = VylukyFiltr.Jen };
        // Referenční prázdný canvas: element s nulovou šířkou nekreslí nic už od první verze.
        var prazdnyCanvas = Rendering.RenderSnapshot.Capture(new NotesLegendElement { Width = 0 }, data);

        Assert.Equal(prazdnyCanvas, Rendering.RenderSnapshot.Capture(blok, data));
    }
}
