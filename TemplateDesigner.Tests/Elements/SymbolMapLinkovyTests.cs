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
    /// ⚠️ Není to pravidlo „víceznakové = zkratka" (to padlo 27. 8. s BEV). <c>WC</c> v překladové
    /// tabulce starého klienta míří na zápornou hodnotu („-15"), tedy na text - stejně jako u nás.
    /// </remarks>
    [Theory]
    [InlineData("WC")]
    public void NeoverenaViceznakovaZnacka_ZustavaText(string znak)
    {
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveLinkovy(znak).Mode);
    }

    /// <summary>
    /// Vybavení vozidla se tiskne piktogramem - loď, wifi i <b>zásuvka</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 31. 8. tu stálo, že tyhle tři ve fontu nejsou. U zásuvky to <b>nebyla pravda</b> -
    /// a překladová tabulka legacy pak potvrdila i kódy: loď 61 → 180, wifi 65 → 186,
    /// zásuvka 66 → 179. Zbytek bloku (káva, sedadlo, obrazovka) legacy k žádné zkratce nepřiřazuje.
    /// </remarks>
    [Theory]
    [InlineData("lod", "´")]
    [InlineData("wifi", "º")]
    [InlineData("zasu", "³")]
    public void VybaveniVozidla_JePiktogram(string znak, string pozice)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, value);
    }

    /// <summary>
    /// Ve <b>vývěsce</b> ty tři zůstávají textem.
    /// </summary>
    /// <remarks>
    /// ⚠️ Není to jen opatrnost: starý vývěskový modul (<c>ALVA.TISKMHD.Main.ConvertSpecialniZnak</c>)
    /// zná jen dny, vozíček a částečnou bezbariérovost - <b>loď, wifi ani zásuvku vůbec nemá</b>.
    /// Přepnout je na piktogram by tedy nebyla parita, ale nová featura nad rámec starého tisku.
    /// </remarks>
    [Theory]
    [InlineData("lod")]
    [InlineData("wifi")]
    [InlineData("zasu")]
    public void VybaveniVozidla_VeVyvesceZustavaText(string znak)
    {
        var (mode, value) = SymbolMap.Resolve(znak);

        Assert.Equal(SymbolRenderMode.Text, mode);
        Assert.Equal(znak, value);
    }

    /// <summary>
    /// Kola a zavazadla mají ve fontu jednoznačný tvar, takže se tisknou piktogramem.
    /// </summary>
    /// <remarks>
    /// Kolo je táž pozice, jakou už používá značka <c>K</c>; kufr je pozice <c>p</c>, kterou má
    /// tenhle soubor doloženou odjakživa (proto tam nesmí „pokračuje"). ⚠️ Původně to bylo odvozené
    /// z významu zkratky; 31. 8. se našla překladová tabulka legacy a <b>obojí potvrdila</b>
    /// (PJI 27 → 76, PCZ 26 → 112).
    /// </remarks>
    [Theory]
    [InlineData("PJI", "L")]   // kola
    [InlineData("PCZ", "p")]   // zavazadla - kufr
    public void VybaveniSJednoznacnymTvarem_JePiktogram(string znak, string pozice)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, value);
    }

    /// <summary>
    /// Zbytek rodiny atributů se tiskne piktogramem - pozice jsou opsané z legacy, ne hádané.
    /// </summary>
    /// <remarks>
    /// Překladová tabulka starého tiskového klienta <c>ALVA.TISKJR.Main.ConvertSpecialniZnak</c>
    /// (id z <c>$$$SALL</c> → kód znaku). Do 31. 8. se myslelo, že v repu není, a tyhle atributy se
    /// proto tiskly zkratkou. V závorce je id a kód znaku.
    /// </remarks>
    [Theory]
    [InlineData("OBC", "M")]   // občerstvení (25 → 77) - příbor
    [InlineData("RBU", "Z")]   // radiobus (29 → 90) - telefon
    [InlineData("SAO", "s")]   // samoobslužný (30 → 115)
    [InlineData("RBP", "ð")]   // jede podmínečně (57 → 240)
    [InlineData("POS", "¨")]   // zásilky (28 → 168)
    public void RodinaAtributu_JePiktogramDleLegacy(string znak, string pozice)
    {
        var (mode, value) = SymbolMap.ResolveLinkovy(znak);

        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, value);
    }

    /// <summary>
    /// IDS a nízkokapacitní vůz zůstávají textem - <b>tak je tiskne i starý modul</b>.
    /// </summary>
    /// <remarks>
    /// V překladové tabulce mají zápornou hodnotu (IDS „-11", NKA „-12"), což u klienta znamená
    /// text, ne glyf. Není to tedy nedodělek po `L11`, ale shoda se starým tiskem - a proto na to
    /// míří vlastní test, aby to někdo „nedodělal" doplněním piktogramu.
    /// </remarks>
    [Theory]
    [InlineData("IDS")]
    [InlineData("NKA")]
    public void IdsANizkokapacitni_ZustavajiTextem(string znak)
    {
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveLinkovy(znak).Mode);
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
