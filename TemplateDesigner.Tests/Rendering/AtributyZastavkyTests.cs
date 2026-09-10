using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Značky trasy zastávky - vlastní mapa, oddělená od atributů spoje (`B123`).
/// </summary>
/// <remarks>
/// Číselník 16 („atributy trasy", `JRapiCis.mac:1283`) a číselník značek spoje sdílejí písmena,
/// ale znamenají jiné věci. Legacy je odděluje dvěma makry (`JRapiLinPrtWin.mac:17-18`):
/// značky `NVxyzALefgOCGabcDM` se kreslí, řídicí `PTpoS&gt;&lt;JE` mění vzhled řádku a samy se
/// netisknou.
/// </remarks>
public class AtributyZastavkyTests
{
    // ── co se kreslí a co ne ─────────────────────────────────────────────────

    [Theory]
    [InlineData("N")] [InlineData("V")] [InlineData("x")] [InlineData("y")] [InlineData("z")]
    [InlineData("A")] [InlineData("L")] [InlineData("O")] [InlineData("D")] [InlineData("M")]
    [InlineData("G")] [InlineData("C")]
    public void Znacka_SeKresli(string znak) => Assert.True(SymbolMap.JeZnackaZastavky(znak));

    /// <summary>
    /// Řídicí atributy se netisknou - mění vzhled řádku, ale samy nejsou značky.
    /// </summary>
    /// <remarks>
    /// `T` = sázej tučně, `S` = vypiš stanoviště k názvu, `P`/`J` = rozděl řádek na příjezd
    /// a odjezd, `E` = nácestná zastávka (563× v datech, nejčastější ze všech - a nekreslí se).
    /// </remarks>
    [Theory]
    [InlineData("P")] [InlineData("T")] [InlineData("p")] [InlineData("o")]
    [InlineData("S")] [InlineData(">")] [InlineData("<")] [InlineData("J")] [InlineData("E")]
    public void RidiciAtribut_SeNekresli(string znak) => Assert.False(SymbolMap.JeZnackaZastavky(znak));

    /// <summary>
    /// ⚠️ `H` u zastávky není bezbariérové vozidlo.
    /// </summary>
    /// <remarks>
    /// V číselníku 16 je to „cílová zastávka pro tablo", kdežto u atributů spoje je `H` vozíček.
    /// Se společnou mapou se zastávka kreslila vozíčkem; v datech se `H` nevyskytuje (census
    /// 10. 9.), takže to nikdo neviděl, ale past to byla.
    /// </remarks>
    [Fact]
    public void H_UZastavkyNeniVozicek()
    {
        Assert.False(SymbolMap.JeZnackaZastavky("H"));
        Assert.Equal(SymbolRenderMode.FontGlyf, SymbolMap.ResolveLinkovy("H").Mode);
    }

    // ── pozice ve fontu ──────────────────────────────────────────────────────

    /// <summary>
    /// Piktogramy míří na pozice z překladové tabulky legacy.
    /// </summary>
    /// <remarks>
    /// `ALVA.TISKJR.Main.ConvertSpecialniZnak`; ověřeno obrysem glyfu v obou fontech 10. 9.
    /// </remarks>
    [Theory]
    [InlineData("N", "m")]   // jen nástup   (31 → 109)
    [InlineData("V", "l")]   // jen výstup   (32 → 108)
    [InlineData("O", "ö")]   // letadlo      (53 → 246)
    [InlineData("D", "´")]   // loď          (61 → 180)
    [InlineData("M", "î")]   // metro        (60 → 238)
    [InlineData("G", "P")]   // paragraf     (37 → 80)
    public void Piktogram_MiriNaPoziciZTabulky(string znak, string pozice)
    {
        var (mode, hodnota) = SymbolMap.ResolveZastavka(znak);
        Assert.Equal(SymbolRenderMode.FontGlyf, mode);
        Assert.Equal(pozice, hodnota);
    }

    /// <summary>
    /// ⚠️ Autobus míří na naši pozici, ne na tu z legacy.
    /// </summary>
    /// <remarks>
    /// Legacy má 119, my 122 - není to odchylka, ale jiný font: starý klient sází „Timetable",
    /// my TimeTable2, a právě na 119 se sady rozcházejí (v TimeTable2 CZ je tam vagon). Táž
    /// úvaha jako u <c>PiktogramyLinky</c>.
    /// </remarks>
    [Fact]
    public void Autobus_MiriNaNasiPozici()
    {
        Assert.Equal("z", SymbolMap.ResolveZastavka("A").Value);
        Assert.Equal("x", SymbolMap.ResolveZastavka("L").Value);
    }

    /// <summary>
    /// „Projíždí" a „jede jinudy" jsou piktogramy, ne znaky.
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 10. 9. se tiskly doslova jako `|` a `&lt;`, protože se pozice hledala u těch znaků
    /// samotných (60 a 124, kde je jen osmiúhelník a čtverec) místo v překladové tabulce, kde jsou
    /// pod id 1 a 2. Doslovné znaky používá jen HTML varianta starého tisku, ne papírová.
    /// Ověřeno obrysem glyfu 10. 9.: 74 je 3,9 × 52,1 (svislice), 75 je 15,7 × 51,8 (vlnovka).
    /// </remarks>
    [Theory]
    [InlineData("|", "J")]   // projíždí    (id 1 → 74)
    [InlineData("<", "K")]   // jede jinudy (id 2 → 75)
    public void PruchodZastavkou_JePiktogram(string hodnota, string pozice) =>
        Assert.Equal(pozice, SymbolMap.PruchodZastavkou(hodnota));

    [Theory]
    [InlineData("7:15")] [InlineData("")] [InlineData("x")] [InlineData(null)]
    public void CoNeniPruchod_ZustavaBezeZmeny(string? hodnota) =>
        Assert.Null(SymbolMap.PruchodZastavkou(hodnota));

    /// <summary>
    /// Textem zůstává, co si starý klient kreslí vlastní větví nebo píše slovem.
    /// </summary>
    /// <remarks>
    /// „Na znamení" a jeho varianty jsou konvence JŘ; metro s indexem a paragrafy s indexem mají
    /// v překladové tabulce **zápornou** hodnotu, což u starého klienta znamená složenou značku.
    /// Vypsat je textem je poctivější než trefit glyf naslepo.
    /// </remarks>
    [Theory]
    [InlineData("x")] [InlineData("y")] [InlineData("z")]
    [InlineData("e")] [InlineData("f")] [InlineData("g")]
    [InlineData("a")] [InlineData("b")] [InlineData("c")]
    [InlineData("C")]
    public void SlozeneZnacky_ZustavajiTextem(string znak) =>
        Assert.Equal(SymbolRenderMode.Text, SymbolMap.ResolveZastavka(znak).Mode);
}
