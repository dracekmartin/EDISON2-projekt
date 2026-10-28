using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Formát času spoje: hodina větším písmem a minuta menším, oddělené mezerou bez dvojtečky -
/// tvar starého postskriptového tisku (`JRapiLinPrtPS7.mac:178` posílá čas jako `hh,mm`).
/// </summary>
public class FormatCasuSpojeTests
{
    private const float MmToPx = 10f;

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 60, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        Grid = new GridStyle { ShowColumnLines = false, ShowRowLines = false },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = 30, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = 12,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    // ── Rozpad času na hodinu a minutu ──────────────────────────────────────

    [Fact]
    public void HodinaVetsi_RozdeliCasNaTriUseky()
    {
        var runs = Prvek().CasRuns("16:15");

        Assert.NotNull(runs);
        Assert.Equal(3, runs!.Count);
        Assert.Equal("16", runs[0].Text);
        Assert.Equal(" ",  runs[1].Text);    // mezera místo dvojtečky, jako ve starém tisku
        Assert.Equal("15", runs[2].Text);
        Assert.Equal(1f,    runs[0].Zmenseni);
        Assert.Equal(0.75f, runs[2].Zmenseni);
    }

    /// <summary>
    /// ⚠️ Hodina se tiskne <b>bez vedoucí nuly</b>. Legacy ji utne číselným přetypováním
    /// (<c>+$p(cas,":",1)</c>, <c>JRapiFce.mac:138</c>), takže má „7 10", ne „07 10". Není to
    /// kosmetika: sloupec spoje je o číslici užší a vejde se jich do řádku víc.
    /// </summary>
    [Theory]
    [InlineData("07:10", "7", "10")]
    [InlineData("05:08", "5", "08")]
    [InlineData("16:15", "16", "15")]
    [InlineData("00:05", "0", "05")]
    public void HodinaJeBezVedouciNuly(string cas, string hodina, string minuta)
    {
        var runs = Prvek().CasRuns(cas);

        Assert.Equal(hodina, runs![0].Text);
        Assert.Equal(minuta, runs[2].Text);
    }

    /// <summary>
    /// Menší minuty a hodina bez nuly znamenají užší sloupec, takže se jich do bloku vejde víc než
    /// při jednotné velikosti s dvojtečkou. Dokud se dolní práh sloupce měřil vždycky jako „00:00",
    /// byl zbytečně velký a formát se do rozvržení nepropsal (nález Jan 7. 9.).
    /// </summary>
    [Fact]
    public void UzsiFormatPustiDoBlokuVicSpoju()
    {
        // Měří se na prvku tak úzkém, že o sloupci rozhoduje jen obsah - kapacita bloku je
        // hrubší měřítko a rozdíl mezi formáty se v ní nemusí projevit celým sloupcem.
        var uzky = PrvekSAutoSloupcem(FormatCasuSpoje.HodinaVetsi);
        uzky.Width = 21f;
        var siroky = PrvekSAutoSloupcem(FormatCasuSpoje.SDvojteckou);
        siroky.Width = 21f;

        float sUzkym = uzky.Rozvrzeni(SirokaData(4))[0].SirkySloupcu[0];
        float sSirokym = siroky.Rozvrzeni(SirokaData(4))[0].SirkySloupcu[0];

        Assert.True(sUzkym < sSirokym,
            $"užší formát dal sloupec {sUzkym:0.00} mm, širší {sSirokym:0.00} mm - práh se nezměnil");
    }

    /// <summary>Kolik spojů se do prvku vejde na jeden blok - největší počet, který se ještě
    /// nerozdělí.</summary>
    /// <remarks>⚠️ Nejde to odečíst z prvního bloku rozdělené linky: od 14. 9. se spoje dělí mezi
    /// části rovným dílem, takže obě velikosti písma mohou dát stejně velkou první část, přestože
    /// se do bloku vejde jiný počet spojů.</remarks>
    private static int KapacitaBloku(LineTableElement prvek)
    {
        int n = 1;
        while (n < 200 && prvek.Rozvrzeni(SirokaData(n + 1)).Count == 1) n++;
        return n;
    }

    /// <summary>
    /// Prvek s <b>automatickou</b> šířkou sloupce spoje - jen tam se dolní práh vůbec uplatní.
    /// S pevnou šířkou (jako má sdílená fixtura) by test neměřil nic.
    /// </summary>
    private static LineTableElement PrvekSAutoSloupcem(FormatCasuSpoje format) => new()
    {
        X = 0, Y = 0, Width = 60, Height = null,
        FormatCasu = format,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        Grid = new GridStyle { ShowColumnLines = false, ShowRowLines = false },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = 20, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Visible = true,
                Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>Linka s tolika spoji, že se do šířky nevejdou a rozvržení musí řezat.</summary>
    private static DataContext SirokaData(int pocetSpoju) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"),   ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            .. Enumerable.Range(1, pocetSpoju).Select(i => RenderSnapshot.Row(
                ("CISLO_SPOJE", i.ToString()), ("SKUPINA", "1"),
                ("CASY", new JsonArray("16:15", "16:40")))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    [Fact]
    public void PomerMinutySePropisujeDoUseku()
    {
        var prvek = Prvek();
        prvek.PomerMinuty = 0.5f;

        var runs = prvek.CasRuns("08:05");

        Assert.Equal(0.5f, runs![2].Zmenseni);
    }

    /// <summary>Podíl mimo rozsah nesmí vyrobit nečitelné nebo obrácené písmo.</summary>
    [Theory]
    [InlineData(0f,   0.3f)]
    [InlineData(-1f,  0.3f)]
    [InlineData(5f,   1f)]
    public void PomerMinutySeOreze(float zadano, float ocekavano)
    {
        var prvek = Prvek();
        prvek.PomerMinuty = zadano;

        Assert.Equal(ocekavano, prvek.CasRuns("08:05")![2].Zmenseni);
    }

    /// <summary>
    /// ⚠️ Buňka nemusí nést čas - rozdělit ji nesmí nic, vznikl by nesmysl a půlka by se navíc
    /// vysázela menším písmem.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("16:")]
    [InlineData(":15")]
    [InlineData("od:do")]
    public void CoNeniCas_SeNedeli(string obsah)
    {
        Assert.Null(Prvek().CasRuns(obsah));
    }

    /// <summary>
    /// Průjezd zastávkou jde jako **jeden** piktogram, ne jako rozdělený čas.
    /// </summary>
    /// <remarks>
    /// „&lt;" znamená jinou trasu, „|" projetí bez zastavení. Od 10. 9. se kreslí glyfem
    /// (svislice a vlnovka, `B126`), takže `CasRuns` u nich vrací run - ale jednodílný a bez
    /// zmenšeného písma, což je přesně to, co tenhle test hlídal.
    /// </remarks>
    [Theory]
    [InlineData("<")]
    [InlineData("|")]
    public void Prujezd_JdeJakoJedenPiktogram(string obsah)
    {
        var runs = Prvek().CasRuns(obsah);

        Assert.NotNull(runs);
        var run = Assert.Single(runs!);
        Assert.True(run.Symbol);
    }

    [Fact]
    public void SDvojteckou_SeNedeliVubec()
    {
        var prvek = Prvek();
        prvek.FormatCasu = FormatCasuSpoje.SDvojteckou;

        Assert.Null(prvek.CasRuns("16:15"));
    }

    // ── Formát s tečkou (#13610, KOVED) ─────────────────────────────────────

    /// <summary>Zákazník píše časy „9.05" a „14.05": tečka místo dvojtečky a hodina bez nuly.</summary>
    [Theory]
    [InlineData("09:05", "9.05")]
    [InlineData("14:05", "14.05")]
    [InlineData("00:20", "0.20")]
    [InlineData("24:10", "24.10")]
    public void STeckou_PrepiseCas(string cas, string ocekavano)
    {
        var prvek = Prvek();
        prvek.FormatCasu = FormatCasuSpoje.STeckou;

        Assert.Equal(ocekavano, prvek.TextCasu(cas));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<")]
    [InlineData("|")]
    [InlineData("16:")]
    [InlineData("od:do")]
    public void STeckou_CoNeniCas_NechaBezeZmeny(string obsah)
    {
        var prvek = Prvek();
        prvek.FormatCasu = FormatCasuSpoje.STeckou;

        Assert.Equal(obsah, prvek.TextCasu(obsah));
    }

    /// <summary>Ostatní formáty čas nepřepisují - výstup existujících šablon se nesmí změnit.</summary>
    [Theory]
    [InlineData(FormatCasuSpoje.HodinaVetsi)]
    [InlineData(FormatCasuSpoje.SDvojteckou)]
    public void OstatniFormaty_CasNeprepisuji(FormatCasuSpoje format)
    {
        var prvek = Prvek();
        prvek.FormatCasu = format;

        Assert.Equal("09:05", prvek.TextCasu("09:05"));
    }

    [Fact]
    public void STeckou_SeNedeliNaUseky()
    {
        var prvek = Prvek();
        prvek.FormatCasu = FormatCasuSpoje.STeckou;

        Assert.Null(prvek.CasRuns("16:15"));
    }

    /// <summary>
    /// Přepis se musí dostat až do kreslení: „7.10" má o nulu a o jednu tečku dvojtečky méně
    /// inkoustu než „07:10". Převod jen v pomocné metodě by test nechal zelený, tenhle ne.
    /// </summary>
    [Fact]
    public void STeckou_SeNakresliBezNulyATeckou()
    {
        var data = DataSCasy("07:10", "07:35");
        var dvojtecka = Prvek();
        dvojtecka.FormatCasu = FormatCasuSpoje.SDvojteckou;
        var tecka = Prvek();
        tecka.FormatCasu = FormatCasuSpoje.STeckou;

        using var bmpDvojtecka = Vykresli(dvojtecka, data);
        using var bmpTecka = Vykresli(tecka, data);

        Assert.True(Inkoust(bmpTecka) < Inkoust(bmpDvojtecka),
            "čas s tečkou nezabral míň inkoustu - formát se nepropsal do buňky spoje");
    }

    /// <summary>
    /// Čas přípoje se sází stejným tvarem jako čas spoje (nález Jan 8. 9.), takže formát s tečkou
    /// musí platit i v přípojném řádku. Spoje tu časy nemají - inkoust ve sloupcích spojů je jen přípoj.
    /// </summary>
    [Fact]
    public void STeckou_PlatiIVPripojnemRadku()
    {
        var data = RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
            ]),
            ("SPOJE",
            [
                RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                    ("PRIPOJE", RenderSnapshot.Nested(
                        [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
                          ("CAS", "08:25"), ("LINKA", "330520"), ("TEXT", "Hulín")]]))),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));
        LineTableElement SPripoji(FormatCasuSpoje format)
        {
            var prvek = Prvek();
            prvek.ShowPripojeRows = true;
            prvek.FormatCasu = format;
            return prvek;
        }

        using var bmpDvojtecka = Vykresli(SPripoji(FormatCasuSpoje.SDvojteckou), data);
        using var bmpTecka = Vykresli(SPripoji(FormatCasuSpoje.STeckou), data);

        Assert.True(Inkoust(bmpTecka) < Inkoust(bmpDvojtecka),
            "čas přípoje s tečkou nezabral míň inkoustu - formát se nepropsal do přípojného řádku");
    }

    // ── Že se to opravdu nakreslí ───────────────────────────────────────────

    private static DataContext Data() => DataSCasy("16:15", "16:40");

    private static DataContext DataSCasy(string prvni, string druhy) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"),   ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray(prvni, druhy))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    /// <summary>
    /// Kouřová zkouška: ve všech formátech je ve sloupci spoje vidět čas.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Nechytne vypadlou ruční cestu.** Čas se při tomhle formátu vyjímá z buňky a kreslí
    /// se ručně; kdyby ruční cesta vypadla, buňka si text nechá a vysází se starým způsobem -
    /// obsah tedy nezmizí, jen se změní tvar. Že se zmenšení opravdu propíše do kreslení, hlídá
    /// <see cref="HodinaVetsi_ZabereMeneInkoustuNezPlnaVelikost"/> (ověřeno mutací).
    /// </remarks>
    [Theory]
    [InlineData(FormatCasuSpoje.HodinaVetsi)]
    [InlineData(FormatCasuSpoje.SDvojteckou)]
    [InlineData(FormatCasuSpoje.STeckou)]
    public void CasSeNakresliVeVsechFormatech(FormatCasuSpoje format)
    {
        var prvek = Prvek();
        prvek.FormatCasu = format;
        var data = Data();

        using var bmp = Vykresli(prvek, data);

        // Sloupec spoje leží za sloupcem zastávek; hledá se v něm jakýkoli tmavý pixel.
        int inkoust = 0;
        for (int x = (int)(31 * MmToPx); x < (int)(42 * MmToPx); x++)
            inkoust += InkoustVeSloupci(bmp, x);

        Assert.True(inkoust > 0, $"ve sloupci spoje není při formátu {format} žádný čas");
    }

    /// <summary>Menší minuty musí zabrat míň místa než celý čas jednou velikostí - jinak se
    /// zmenšení nikam nepropsalo a formát je jen jiný oddělovač.</summary>
    [Fact]
    public void HodinaVetsi_ZabereMeneInkoustuNezPlnaVelikost()
    {
        var data = Data();
        var velky = Prvek();
        velky.FormatCasu = FormatCasuSpoje.HodinaVetsi;
        velky.PomerMinuty = 1f;
        var maly = Prvek();
        maly.FormatCasu = FormatCasuSpoje.HodinaVetsi;
        maly.PomerMinuty = 0.4f;

        using var bmpVelky = Vykresli(velky, data);
        using var bmpMaly = Vykresli(maly, data);

        Assert.True(Inkoust(bmpMaly) < Inkoust(bmpVelky),
            "menší minuty nezabraly míň inkoustu - poměr se nepropsal do kreslení");
    }

    // ── Pomocné ─────────────────────────────────────────────────────────────

    private static int Inkoust(SKBitmap bmp)
    {
        int n = 0;
        for (int x = 0; x < bmp.Width; x++) n += InkoustVeSloupci(bmp, x);
        return n;
    }

    private static int InkoustVeSloupci(SKBitmap bmp, int x)
    {
        if (x < 0 || x >= bmp.Width) return 0;
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red < 128 && c.Green < 128 && c.Blue < 128) n++;
        }
        return n;
    }

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 10f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 10f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
