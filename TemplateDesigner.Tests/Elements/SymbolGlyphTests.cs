using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Mapování datového atributu na glyf symbolového JŘ fontu (#13048 bod 1). Pozice glyfů v Timetable
/// fontu se neshodují s písmeny číselníku, takže „A" (autobus) a „L" (vlak) se musí přeložit -
/// bez toho se u zastávky kreslil cizí piktogram (trojúhelník a kolo).
/// Naměřeno v Mapě znaků: autobus 0x77 („w"), lokomotiva 0x78 („x"), vagon 0x7A („z").
/// </summary>
public class SymbolGlyphTests
{
    [Fact]
    public void PrestupyMajiGlyfPodleFontu()
    {
        Assert.Equal("w", StopScheduleElementBase.SymbolGlyph("A"));   // autobus
        Assert.Equal("x", StopScheduleElementBase.SymbolGlyph("L"));   // vlak (lokomotiva)
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

    /// <summary>Znaky, které dnes fungují (bezbariér, kroužkové dny), se nesmí překládat - jejich
    /// glyf na písmenu sedí a překlad by je rozbil.</summary>
    [Fact]
    public void OstatniZnakyZustavajiBezeZmeny()
    {
        Assert.Equal("H", StopScheduleElementBase.SymbolGlyph("H"));   // bezbariér
        Assert.Equal("F", StopScheduleElementBase.SymbolGlyph("F"));   // částečně bezbariérový
        Assert.Equal("1", StopScheduleElementBase.SymbolGlyph("1"));   // kroužkový den
        Assert.Equal("+", StopScheduleElementBase.SymbolGlyph("+"));
    }
}
