using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Značky <b>linkového</b> JŘ: která se kreslí piktogramem ze symbolového fontu a která textem.
///
/// Testuje se to zvlášť od vývěsek, protože linkový JŘ má <b>jiný slovník značek</b> a platí pro něj
/// opačné výchozí pravidlo: piktogram jen z ověřeného seznamu, zbytek textem. Bez toho by první
/// napojení fontu vytisklo cizí obrázky - pozice glyfů se s písmeny číselníku neshodují.
/// </summary>
public class SymbolMapLinkovyTests
{
    /// <summary>
    /// Past, kvůli které tenhle seznam vůbec vznikl: „pokračuje" má v číselníku značku <c>p</c>,
    /// jenže na pozici <c>p</c> je v Timetable fontu <b>kufr</b> (zavazadla). Naivní „jednoznakové
    /// = piktogram" by cestujícímu do jízdního řádu nakreslilo zavazadlo.
    /// </summary>
    [Fact]
    public void Pokracuje_SeKresliTextem_NeKufrem()
    {
        var (mode, value) = SymbolMap.ResolveLinkovy("p");

        Assert.Equal(SymbolRenderMode.Text, mode);
        Assert.Equal("p", value);
    }

    /// <summary>Denní značky piktogramy jsou - a v tištěných jízdních řádech se tak i tisknou:
    /// pracovní dny zkřížená kladiva, soboty a neděle kroužkové číslice, svátky kříž.</summary>
    [Theory]
    [InlineData("X")]   // pracovní dny - zkřížená kladiva (0x58)
    [InlineData("+")]   // svátky - kříž (0x2B)
    [InlineData("6")]   // sobota - kroužková číslice (0x36)
    [InlineData("7")]   // neděle - kroužková číslice (0x37)
    public void DenniZnacky_JsouPiktogramy(string znak)
    {
        Assert.Equal(SymbolRenderMode.FontGlyf, SymbolMap.ResolveLinkovy(znak).Mode);
    }

    /// <summary>Značky, jejichž pozice ve fontu <b>měřená není</b>, musí zůstat textem. Jakmile je
    /// někdo ověří v obou fontech, přidá je do seznamu a tenhle test upraví - do té doby je hádání
    /// zakázané, protože se pozná až v tisku u zákazníka.</summary>
    [Theory]
    [InlineData("|")]    // projíždí - typografická konvence, ne piktogram
    [InlineData("<")]    // jede jinudy - dtto
    [InlineData("m")]    // vyčká - pozice neměřená
    [InlineData("u")]    // pokračuje jako - pozice neměřená
    [InlineData("^")]    // navazuje - pozice neměřená
    public void NeoverenaZnacka_ZustavaTextem(string znak)
    {
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveLinkovy(znak).Mode);
    }

    /// <summary>Víceznakové značky jsou zkratky (BEV, IDS, WC…), ne piktogramy.</summary>
    [Theory]
    [InlineData("BEV")]
    [InlineData("IDS")]
    [InlineData("WC")]
    public void ViceznakovaZnacka_JeText(string znak)
    {
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveLinkovy(znak).Mode);
    }

    /// <summary>Tři značky v tiskovém fontu vůbec nejsou. Provizorně se vypisují textem
    /// (rozhodnutí Jan 2026-08-24), ať se informace aspoň neztratí.</summary>
    [Theory]
    [InlineData("lod")]
    [InlineData("wifi")]
    [InlineData("zasu")]
    public void ZnackaMimoFont_SeVypisujeTextem(string znak)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.Text, mode);
        Assert.Equal(znak, value);
    }

    /// <summary>Piktogram se překládá na <b>pozici</b>, ne na písmeno - jinak by autobus vyšel
    /// jako železniční vagon.</summary>
    [Fact]
    public void Autobus_SePrekladaNaPozici() => Assert.Equal("z", SymbolMap.ResolveLinkovy("A").Value);

    /// <summary>Vývěskové pravidlo zůstává, jaké bylo: jednoznaková značka je piktogram
    /// (kromě „na znamení"). Sdílená mapa nesměla nic rozhodit - podmínka zadání.</summary>
    [Fact]
    public void VyveskovePravidlo_ZustavaBezeZmeny()
    {
        Assert.Equal(SymbolRenderMode.FontGlyf, SymbolMap.Resolve("A").Mode);
        Assert.Equal("z", SymbolMap.Resolve("A").Value);
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.Resolve("x").Mode);
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.Resolve("BEV").Mode);
    }
}
