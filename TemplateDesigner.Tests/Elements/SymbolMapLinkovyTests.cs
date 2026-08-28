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
    [InlineData("u")]    // pokračuje - v jádru sady se nevyskytuje, tvar neověřený
    public void NeoverenaZnacka_ZustavaTextem(string znak)
    {
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveLinkovy(znak).Mode);
    }

    /// <summary>Návaznosti jsou trojúhelníky - doloženo tiskem staré linky 330511: „vyčká" dutý,
    /// „navazuje" plný. ⚠️ Cíl se bere z tabulky značek linky, ne přes vývěskový překlad - ten by
    /// „vyčká" (pozice <c>A</c>) poslal dál na autobus.</summary>
    [Theory]
    [InlineData("m", "A")]   // vyčká - dutý trojúhelník
    [InlineData("^", "B")]   // navazuje - plný trojúhelník
    public void Navaznost_JeTrojuhelnik(string znak, string pozice)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, value);
    }

    /// <summary>
    /// Bezbariérovost spoje se tiskne <b>vozíčkem</b>, i když ji data posílají víceznakovým kódem.
    /// </summary>
    /// <remarks>
    /// Nález Jan 27. 8.: „ve starém je ta ikonka vozíčku, teď vypíšeme textem BEV". Starý modul má
    /// BEV i CBE v číselníku znaků jako symboly (<c>JRapiLinPrtWin.mac:1580</c>, <c>:1596</c>).
    /// Pozice ověřená vykreslením tabulky glyfů v obou fontech - 72 vozíček, 70 s doprovodem.
    /// </remarks>
    [Theory]
    [InlineData("BEV", "H")]
    [InlineData("CBE", "F")]
    public void Bezbarierovost_JeVozicek(string znak, string pozice)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, value);
    }

    /// <summary>
    /// Víceznakový kód, u kterého pozici ve fontu nikdo neověřil, zůstává textem.
    /// </summary>
    /// <remarks>
    /// ⚠️ Není to pravidlo „víceznakové = zkratka" (to padlo 27. 8. s BEV), ale tolerance: starý
    /// modul má symboly i pro IDS a WC, jen se u nás pozice ve fontu neurčila. Než se určí, je
    /// lepší vypsat zkratku než vytisknout cizí piktogram.
    /// </remarks>
    [Theory]
    [InlineData("IDS")]
    [InlineData("WC")]
    public void NeoverenaViceznakovaZnacka_ZustavaText(string znak)
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
