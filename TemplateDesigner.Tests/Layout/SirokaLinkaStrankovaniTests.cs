using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;


namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Doklad k rozhodnutí, že **vodorovné opakování sekce není potřeba** (plánovaný `L5-3`, zrušen
/// 25. 8.): bloky se sázejí pod sebe, takže široká linka vyjede na víc stránek **stávajícím
/// svislým** stránkováním.
/// </summary>
/// <remarks>
/// Bez tohohle testu by to bylo jen tvrzení v dokumentaci. Zrušený krok plánu se neobhajuje
/// úvahou - buď to ta cesta unese, nebo ho bylo potřeba udělat.
/// </remarks>
public class SirokaLinkaStrankovaniTests
{
    private static Template Sablona(LineTableElement prvek) => new()
    {
        PageSize = PageSize.A4Portrait,
        Sections =
        [
            new Section
            {
                Name = "Jízdní řád",
                PrintBehavior = SectionPrintBehavior.Once,
                OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                DesignerHeight = 50,
                Elements = [prvek]
            }
        ]
    };

    private static LineTableElement Prvek()
    {
        var p = ElementRenderFixtures.LineTable();
        p.X = 0; p.Y = 0; p.Height = null;   // dynamická výška - roste s počtem bloků
        return p;
    }

    /// <summary>Linka, která se do šířky nevejde, vyjede na víc stránek - a to bez jediné změny
    /// v enginu, jen tím, že si prvek řekne o větší výšku.</summary>
    [Fact]
    public void SirokaLinka_VyjedeNaViceStranek()
    {
        var prvek = Prvek();
        var data = LineTableBlokyTests.SirokaLinkaData(200, pocetZastavek: 25);
        int bloku = prvek.Rozvrzeni(data).Count;

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(Sablona(prvek), data);

        Assert.True(bloku > 1, "200 spojů se do jedné šířky vejít nemá");
        Assert.True(vysledek.TotalPages > 1,
            $"{bloku} bloků pod sebou se má rozpadnout na víc stránek, vyšla jedna");
    }

    /// <summary>Úzká linka zůstane na jedné stránce - stránkování se nesmí plést do běžného případu.</summary>
    [Fact]
    public void UzkaLinka_ZustaneNaJedneStrance()
    {
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(Prvek()), LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 25));

        Assert.Equal(1, vysledek.TotalPages);
    }

    /// <summary>Rozřezaná sekce navazuje bez díry i bez překryvu - jinak by na přechodu stránek
    /// zmizel nebo se zopakoval pruh tabulky.</summary>
    [Fact]
    public void ChunkyNaSebeNavazuji()
    {
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(Sablona(Prvek()), LineTableBlokyTests.SirokaLinkaData(200, pocetZastavek: 25));

        float? konecPredchoziho = null;
        foreach (var stranka in vysledek.Pages)
        {
            var sekce = Assert.Single(stranka.Sections);
            if (konecPredchoziho is float k) Assert.Equal(k, sekce.ClipTop!.Value, 2);
            konecPredchoziho = sekce.ClipBottom;
        }
    }

    /// <summary>
    /// ⚠️ Řez stránky musí padnout <b>na hranici bloku</b>, ne kam zrovna dojde stránka.
    /// </summary>
    /// <remarks>
    /// Změřeno 25. 8. na A4 na šířku: dva bloky po 105,7 mm na ploše 210 mm daly první stránku
    /// s blokem a půl a druhou s <b>1,4 mm proužkem</b> - tabulka useknutá pár řádků před koncem.
    /// Geometrický řez o blocích nevěděl; prvek je teď nabízí přes <c>INedelitelneDily</c>.
    /// </remarks>
    [Fact]
    public void RezPadneNaHraniciBloku()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var data = LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28);
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;

        var hranice = ((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.NotEmpty(hranice);
        Assert.True(vysledek.TotalPages > 1, "linka se má rozpadnout na víc stránek");
        foreach (var stranka in vysledek.Pages.Skip(1))
        {
            float rez = stranka.Sections[0].ClipTop!.Value;
            Assert.True(hranice.Any(z => Math.Abs(z - rez) < 0.05f),
                $"řez na {rez:0.0} mm nepadl na hranici bloku ({string.Join(", ", hranice.Select(z => $"{z:0.0}"))})");
        }
    }

    /// <summary>
    /// Díl vyšší než celá stránka se rozřízne i tak - jinak by se nevešel nikdy a stránkování by se
    /// zacyklilo. Řez ale musí padnout <b>mezi řádky zastávek</b>, ne doprostřed textu: proto prvek
    /// nabízí vedle hranic bloků i slabé zlomy na hranicích řádků.
    /// </summary>
    [Fact]
    public void BlokVyssiNezStranka_SeRozrizneMeziRadky()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        // jeden blok (málo spojů) a tolik zastávek, že sám přeroste stránku
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 200);

        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(data);
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.True(vysledek.TotalPages > 1);
        Assert.Single(((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).DefaultIfEmpty(),
            default(Zlom));   // jediný blok → žádná silná hranice
        foreach (var stranka in vysledek.Pages.Skip(1))
        {
            float rez = stranka.Sections[0].ClipTop!.Value;
            Assert.True(zlomy.Any(z => Math.Abs(z.Mm - rez) < 0.05f),
                $"řez na {rez:0.0} mm nepadl mezi řádky - tabulka je useknutá uprostřed textu");
        }
    }

    /// <summary>Hranice bloku má přednost před hranicí řádku, i když je výš. Kdyby se bralo prostě
    /// „poslední, co se vejde", řez by padl doprostřed bloku pokaždé, když v něm zbyl nějaký řádek.</summary>
    [Fact]
    public void SilnyZlomMaPrednostPredSlabym()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        var data = LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28);

        var silne = ((INedelitelneDily)prvek).ZlomyMm(data).Where(z => z.Silny).Select(z => z.Mm).ToList();
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.NotEmpty(silne);
        foreach (var stranka in vysledek.Pages.Skip(1))
            Assert.Contains(silne, z => Math.Abs(z - stranka.Sections[0].ClipTop!.Value) < 0.05f);
    }

    /// <summary>
    /// Když řez padne <b>doprostřed</b> bloku, pokračovací stránka musí zopakovat jeho hlavu -
    /// jinak jsou tam řádky zastávek bez čísel spojů a nejde poznat, který sloupec je který spoj.
    /// Místo na ni vyhradí stránkování, aby ji prvek nepřetiskl přes obsah.
    /// </summary>
    [Fact]
    public void RezUprostredBloku_ZopakujeHlavicku()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;
        var data = LineTableBlokyTests.SirokaLinkaData(4, pocetZastavek: 200);   // jeden vysoký blok

        float hlavicka = ((INedelitelneDily)prvek).OpakovanaHlavickaMm(data);
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.True(hlavicka > 0.01f, "prvek má hlavu, kterou lze zopakovat");
        Assert.True(vysledek.TotalPages > 1);
        Assert.Equal(0f, vysledek.Pages[0].Sections[0].OpakovanaHlavickaMm);   // první stránka ji má vlastní
        foreach (var stranka in vysledek.Pages.Skip(1))
            Assert.Equal(hlavicka, stranka.Sections[0].OpakovanaHlavickaMm, 2);
    }

    /// <summary>Stránka, která začíná novým blokem, si hlavičku přinese sama - opakovat se nesmí,
    /// jinak by tam byla dvakrát a ukrojila by místo, které nikdo nepotřebuje.</summary>
    [Fact]
    public void RezNaHraniciBloku_HlavickuNeopakuje()
    {
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        var sablona = Sablona(prvek);
        sablona.PageSize = PageSize.A4Landscape;

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(sablona, LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 28));

        Assert.True(vysledek.TotalPages > 1);
        Assert.All(vysledek.Pages, s => Assert.Equal(0f, s.Sections[0].OpakovanaHlavickaMm));
    }

    /// <summary>
    /// ⚠️ Zopakovaná hlavička nesmí mít pod sebou <b>konec předchozí stránky</b>.
    /// </summary>
    /// <remarks>
    /// Posunutí obsahu dolů o výšku hlavičky okno jen přesunulo - do viditelného pásu se tím
    /// dostaly poslední řádky předchozí stránky a prosvítaly zpod hlavičky (nahlásil Jan 25. 8.,
    /// potvrzeno vykreslením: přes hlavičku ležely tři řádky zastávek). Obsah se proto ořezává
    /// až od řezu a hlavička se kreslí zvlášť nad ním.
    /// <para>Testuje se porovnáním pruhu s hlavičkou proti témuž pruhu na první stránce, kde ji
    /// blok nese sám: musí vyjít <b>pixel po pixelu stejně</b>. Cokoli prosáklého se liší.</para>
    /// </remarks>
    [Fact]
    public void ZopakovanaHlavickaNemaPodSebouKonecPredchoziStranky()
    {
        const float MmToPx = 4f;
        var prvek = ElementRenderFixtures.LineTable();
        prvek.X = 0; prvek.Y = 0; prvek.Height = null; prvek.Width = 277;
        // ⚠️ Průhledné pozadí záhlaví je tu **schválně**: s neprůhledným by hlavička prosáklý
        // obsah sama překryla a test by chybu neviděl (ověřeno mutací - s plným pozadím projde).
        prvek.HeaderStyle.BackgroundColor = "#00FFFFFF";
        var ps = PageSize.A4Landscape;
        var sablona = new Template
        {
            PageSize = ps,
            Sections = [ new Section {
                Name = "Obsah",
                PrintBehavior = SectionPrintBehavior.Once,
                OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                DesignerHeight = ps.PrintableHeight,
                Elements = [prvek] } ]
        };
        // tolik zastávek, že jeden blok přeroste stránku → řez uvnitř bloku
        var data = LineTableBlokyTests.SirokaLinkaData(41, pocetZastavek: 90);
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        var pokracovani = vysledek.Pages.Select(x => x.Sections[0])
                                        .First(x => x.OpakovanaHlavickaMm > 0.01f);
        int pruhPx = (int)(pokracovani.OpakovanaHlavickaMm * MmToPx);
        Assert.True(pruhPx > 2, "hlavička musí mít měřitelnou výšku");

        var prvni = Vykresli(prvek, vysledek.Pages[0].Sections[0], ps, MmToPx);
        var druha = Vykresli(prvek, pokracovani, ps, MmToPx);

        int sirka = (int)(ps.PrintableWidth * MmToPx);
        int x0 = (int)(ps.MarginLeft * MmToPx), y0 = (int)(ps.MarginTop * MmToPx);
        for (int y = y0; y < y0 + pruhPx; y++)
            for (int x = x0; x < x0 + sirka; x++)
                if (prvni.GetPixel(x, y) != druha.GetPixel(x, y))
                    Assert.Fail($"pruh s hlavičkou se liší na {x},{y} - zpod hlavičky prosvítá obsah");

        prvni.Dispose();
        druha.Dispose();
    }

    /// <summary>Vykreslí jednu stránku stejnou cestou jako náhled (posun originu, ořez sekce).</summary>
    private static SKBitmap Vykresli(LineTableElement prvek, PlacedSection placed, PageSize ps, float mmToPx)
    {
        var bmp = new SKBitmap((int)(ps.Width * mmToPx), (int)(ps.Height * mmToPx),
                               SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        float topPx = (ps.MarginTop + placed.Y) * mmToPx;
        canvas.Save();
        canvas.ClipRect(new SKRect(ps.MarginLeft * mmToPx, topPx,
            (ps.MarginLeft + ps.PrintableWidth) * mmToPx, topPx + placed.Height * mmToPx));
        float posun = (placed.ClipTop.HasValue ? -placed.ClipTop.Value * mmToPx : 0f)
                      + placed.OpakovanaHlavickaMm * mmToPx;
        prvek.Render(new RenderContext(canvas, mmToPx, placed.Data)
        {
            SectionOriginX = ps.MarginLeft * mmToPx,
            SectionOriginY = topPx + posun,
            ClipTopMm = placed.ClipTop ?? 0f,
            OpakovanaHlavickaMm = placed.OpakovanaHlavickaMm
        });
        canvas.Restore();
        return bmp;
    }

    /// <summary>
    /// ⚠️ <b>Prvky uvnitř sekce se navzájem neposouvají</b> - rostoucí prvek ten pod sebou přetiskne.
    /// </summary>
    /// <remarks>
    /// Není to nedodělek, ale důsledek absolutního pozicování (zásada projektu). Test to drží
    /// zapsané, protože na tom stojí rozhodnutí z 25. 8., proč má nová šablona <b>dvě</b> výchozí
    /// sekce: záhlaví, které roste s daty, nesmí sdílet sekci s tabulkou. Analýza do té doby
    /// tvrdila opak („textový prvek naroste a tabulku odsune"), což měření vyvrátilo.
    /// </remarks>
    [Fact]
    public void UvnitrSekceSePrvkyNeposouvaji()
    {
        var rostouci = Text(y: 0, vyska: null, DlouhyText());
        var pod = Text(y: 12, vyska: 5, "TABULKA");
        var sekce = Sekce("Obsah", 100, rostouci, pod);

        float vyskaTextu = new SkiaHeightCalculator().CalculateHeight(rostouci, 100, null);

        Assert.True(vyskaTextu > pod.Y, "text má narůst přes pozici prvku pod ním, jinak test nic neměří");
        Assert.Equal(12f, pod.Y);   // prvek zůstává, kam ho autor položil
        Assert.Contains(pod, sekce.Elements);
    }

    /// <summary>
    /// Mezi sekcemi tok naopak <b>funguje</b>: sekce pod rostoucí se odsune přesně o její výšku.
    /// Tohle je ta cesta, kterou se překryv řeší - proto má nová šablona záhlaví ve vlastní sekci.
    /// </summary>
    [Fact]
    public void SekcePodRostouciSeOdsune()
    {
        var prvni = Sekce("Záhlaví", null, Text(y: 0, vyska: null, DlouhyText()));
        var druha = Sekce("Obsah", 20, Text(y: 0, vyska: 5, "NÁSLEDUJÍCÍ SEKCE"));

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator())
            .Layout(new Template { PageSize = PageSize.A4Portrait, Sections = [prvni, druha] }, null);

        var s1 = vysledek.Pages[0].Sections[0];
        var s2 = vysledek.Pages[0].Sections[1];
        Assert.True(s1.Height > 30f, "záhlaví má narůst, jinak test nic neměří");
        Assert.Equal(s1.Y + s1.Height, s2.Y, 2);
    }

    /// <summary>
    /// Nová šablona má <b>záhlaví a obsah jako dvě sekce</b>. Kdyby se to sloučilo zpátky do jedné,
    /// vrátí se překryv rostoucího záhlaví přes tabulku - proto to hlídá test, ne jen komentář.
    /// </summary>
    [Fact]
    public void NovaSablonaMaZahlaviVeVlastniSekci()
    {
        var sablona = TemplateDesignerViewModel.CreateSampleTemplate();

        Assert.True(sablona.Sections.Count >= 2,
            "nová šablona má mít záhlaví a obsah zvlášť, ne všechno v jedné sekci");
        var zahlavi = sablona.Sections[0];
        Assert.True(zahlavi.DesignerHeight < sablona.PageSize.PrintableHeight,
            "sekce záhlaví nesmí zabírat celou stránku, jinak se pod ni obsah nevejde");
        Assert.All(sablona.Sections,
            sek => Assert.Equal(SectionOverflowBehavior.SplitAcrossPages, sek.OverflowBehavior));
    }

    private static string DlouhyText() =>
        string.Join(" ", Enumerable.Repeat("Dopravce s dlouhým názvem a adresou", 40));

    private static TextElement Text(float y, float? vyska, string text) => new()
    {
        X = 0, Y = y, Width = 100, Height = vyska, WordWrap = vyska is null, Text = text,
        Style = new TextStyle { FontFamily = "Arial", FontSize = 8 }
    };

    private static Section Sekce(string nazev, float? vyska, params TemplateElement[] prvky) => new()
    {
        Name = nazev, PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
        DesignerHeight = vyska, Elements = [.. prvky]
    };
}
