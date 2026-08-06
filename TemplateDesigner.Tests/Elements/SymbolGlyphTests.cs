using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Mapování datového atributu na glyf symbolového JŘ fontu (#13048 bod 1, #13093 bod 2). Pozice glyfů
/// v Timetable fontu se neshodují s písmeny číselníku, takže se bez překladu kreslil cizí piktogram.
///
/// Pozice ověřené 2026-08-03 <b>vytažením obrysů z <c>Fonts/TimeTable2.ttf</c></b> (jména glyfů jsou
/// v fontu obecná, takže se musí nakreslit a prohlédnout - Mapa znaků na to nestačí, z ní se dvakrát
/// přečetlo špatně): `w` 0x77 = železniční vagon · `x` 0x78 = lokomotiva · `z` 0x7A = autobus ·
/// `L` 0x4C = jízdní kolo · `y` 0x79 = restaurace.
///
/// ⚠️ Vybrané pozice platí i ve **slovenském** fontu (`TimeTable2SK.ttf`) - `z` autobus, `x` vlak,
/// `L` kolo jsou v obou. `w` je jediná z nich, kde se fonty rozcházejí (CZ vagon / SK autobus),
/// takže autobus na `w` by tiskl podle jazyka jinou věc.
/// </summary>
public class SymbolGlyphTests
{
    [Fact]
    public void PrestupyMajiGlyfPodleFontu()
    {
        Assert.Equal("z", StopScheduleElementBase.SymbolGlyph("A"));   // autobus (0x7A)
        Assert.Equal("x", StopScheduleElementBase.SymbolGlyph("L"));   // vlak - lokomotiva (0x78)
    }

    /// <summary>Autobus NESMÍ skončit na „w" - tam je železniční vagon. Byla to první verze opravy
    /// (podle ručně opsaného kódu) a u přestupu na autobus se kreslil vagon.</summary>
    [Fact]
    public void AutobusNeniVagon()
    {
        Assert.NotEqual("w", StopScheduleElementBase.SymbolGlyph("A"));
    }

    /// <summary>
    /// Žádná sémantická značka nesmí cílit na „w": je to jediný kód z používaných, který má
    /// v CZ a SK fontu **jiný význam** (CZ železniční vagon × SK autobus, změřeno 4. 8. 2026),
    /// takže by se výsledek rozešel podle jazyka šablony. Zbytek mapy (`z` autobus, `x` vlak,
    /// `L` kolo) sedí v obou fontech - proto `SymbolGlyph` culture parametr nepotřebuje.
    /// </summary>
    [Theory]
    [InlineData("A")]
    [InlineData("L")]
    [InlineData("K")]
    public void ZadnaZnackaNecliNaKodZavislyNaJazyku(string znak)
    {
        Assert.NotEqual("w", StopScheduleElementBase.SymbolGlyph(znak));
    }

    /// <summary>Kola nemají písmeno v číselníku, posílají se naším markerem „K". Musí se přeložit na
    /// „L" (kolo); poslat rovnou „L" nelze, to je vlak a skončilo by na lokomotivě.</summary>
    [Fact]
    public void KoloMaVlastniMarker()
    {
        Assert.Equal("L", StopScheduleElementBase.SymbolGlyph("K"));
        Assert.NotEqual(StopScheduleElementBase.SymbolGlyph("L"), StopScheduleElementBase.SymbolGlyph("K"));
    }

    /// <summary>„Na znamení" se kreslí PROSTÝM TEXTEM, ne piktogramem - na pozici „x" je v symbolovém
    /// fontu lokomotiva, takže piktogramová cesta by u každé zastávky na znamení nakreslila vláček.
    /// Tohle je invariant, na kterém stojí vzhled vývěsky (potvrzeno Jan 2026-08-03).</summary>
    [Fact]
    public void NaZnameniZustavaText()
    {
        Assert.True(StopScheduleElementBase.SymbolJeText("x"));
        Assert.True(StopScheduleElementBase.SymbolJeText("y"));
        Assert.True(StopScheduleElementBase.SymbolJeText("z"));

        // A protože jde textovou cestou, nesmí ho mapování glyfů nikdy potkat ani přepsat.
        Assert.Equal("x", StopScheduleElementBase.SymbolGlyph("x"));
    }

    /// <summary>Piktogramem je jen jednoznaková značka, která nejde textovou cestou - na téhle
    /// podmínce visí, kdy se vůbec smí překládat glyf (víceznakové „Po" ani „na znamení" ne).</summary>
    [Fact]
    public void PiktogramJeJenJednoznakovaNetextovaZnacka()
    {
        Assert.True(StopScheduleElementBase.JePiktogram("A"));
        Assert.True(StopScheduleElementBase.JePiktogram("H"));
        Assert.False(StopScheduleElementBase.JePiktogram("x"));    // na znamení - text
        Assert.False(StopScheduleElementBase.JePiktogram("Po"));   // víceznakový den
        Assert.False(StopScheduleElementBase.JePiktogram(""));
    }

    /// <summary>Mřížka odjezdů musí překlad glyfu dělat **taky** - dlouho ho nedělala, takže kolo
    /// u minuty vyšlo jako glyf sedící na pozici „K" (uživatel to popsal jako „had"), zatímco
    /// v legendě už bylo správně. Značka a její vysvětlivka se nesmí rozejít.</summary>
    [Fact]
    public void MrizkaOdjezduPrekladaGlyfStejneJakoLegenda()
    {
        var kolo = new AnotaceData { Typ = TypAnotace.SpecialniZnak, Znak = "K", Kategorie = NoteCategories.OdjezdKola };
        Assert.Equal("L", DepartureGridElement.GlyfProKresleni(kolo));
        Assert.Equal(StopScheduleElementBase.SymbolGlyph("K"), DepartureGridElement.GlyfProKresleni(kolo));

        // Textová anotace („R" u místenky, ruční komentáře) se nepřekládá - je to písmeno ze zadání.
        var mistenka = new AnotaceData { Typ = TypAnotace.Text, Znak = "R" };
        Assert.Equal("R", DepartureGridElement.GlyfProKresleni(mistenka));

        // „Na znamení" jde textovou cestou, i když je označené jako speciální znak.
        var naZnameni = new AnotaceData { Typ = TypAnotace.SpecialniZnak, Znak = "x" };
        Assert.Equal("x", DepartureGridElement.GlyfProKresleni(naZnameni));
    }

    /// <summary>Překlad se řídí **kategorií z dat**, ne písmenem. Agenda MHD posílá značku jako
    /// <c>$c(kód)</c>, tedy přímo pozici ve fontu - kód 65 dá „A", kód 75 „K". Plošný překlad podle
    /// písmene by zákazníkovi MHD nakreslil autobus nebo kolo místo jeho symbolu.</summary>
    [Fact]
    public void ZnackyMhdSeNeprekladaji()
    {
        // MHD kategorii sémantických značek nepoužívá (prázdná nebo „atribut-zastavky").
        Assert.Equal("A", StopScheduleElementBase.GlyfDleKategorie("A", ""));
        Assert.Equal("K", StopScheduleElementBase.GlyfDleKategorie("K", ""));
        Assert.Equal("A", StopScheduleElementBase.GlyfDleKategorie("A", NoteCategories.AtributZastavky));

        // Sémantické značky AD se přeloží. Kategorie SE BEROU Z KONSTANT - opsané jméno je přesně to,
        // co 4. 8. rozbilo kolo: kategorie se přejmenovala a překlad zůstal na starém jménu, kdežto
        // test si to staré jméno držel sám, takže zůstal zelený nad nefunkční aplikací.
        Assert.Equal("z", StopScheduleElementBase.GlyfDleKategorie("A", NoteCategories.Prestup));
        Assert.Equal("L", StopScheduleElementBase.GlyfDleKategorie("K", NoteCategories.OdjezdKola));
    }

    /// <summary>Kategorie, které nesou **marker** místo pozice ve fontu, musí být v sémantické sadě -
    /// jinak se marker vykreslí surově a vyjede cizí piktogram. Tohle je pojistka proti rozejití
    /// dvou nezávislých míst (co se vypíná v šabloně × co se překládá na glyf).</summary>
    [Fact]
    public void MarkeroveKategorieJsouSemanticke()
    {
        Assert.Contains(NoteCategories.OdjezdKola, NoteCategories.Semanticke);
        Assert.Contains(NoteCategories.Prestup, NoteCategories.Semanticke);

        // A naopak: kategorie MHD značek v sadě být nesmí.
        Assert.DoesNotContain(NoteCategories.AtributZastavky, NoteCategories.Semanticke);
    }

    /// <summary>Znaky, které dnes fungují (bezbariér, kroužkové dny), se nesmí překládat - jejich
    /// glyf na písmenu sedí a překlad by je rozbil.</summary>
    [Fact]
    public void OstatniZnakyZustavajiBezeZmeny()
    {
        Assert.Equal("H", StopScheduleElementBase.SymbolGlyph("H"));   // bezbariér
        Assert.Equal("F", StopScheduleElementBase.SymbolGlyph("F"));   // částečně bezbariérový
        Assert.Equal("1", StopScheduleElementBase.SymbolGlyph("1"));   // kroužkový den
        Assert.Equal("+", StopScheduleElementBase.SymbolGlyph("+"));
        Assert.Equal("R", StopScheduleElementBase.SymbolGlyph("R"));   // místenka - kreslí se textem
    }
}
