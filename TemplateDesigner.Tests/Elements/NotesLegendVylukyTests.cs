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
