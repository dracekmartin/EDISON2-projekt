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
