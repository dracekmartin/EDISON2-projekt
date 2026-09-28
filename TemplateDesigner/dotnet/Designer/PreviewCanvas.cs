using System.IO;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Designer;

/// <summary>
/// Renderuje šablonu pro live preview - bez designerového chrome (záhlaví sekcí,
/// handles, badgů, mřížky). Elementy s visibilityExpression="false" se nezobrazí.
/// Zoom se počítá automaticky podle šířky dostupného canvasu (fit-to-width).
///
/// Používá PageLayoutEngine pro rozložení sekcí na stránky.
/// </summary>
public class PreviewCanvas
{
    private const float PagePaddingPx = 20f;
    // Horní odsazení první stránky uvnitř plátna. Nad plátnem je v UI pevný 34px pruh
    // se stránkováním → 34 + 66 = 100 = horní okraj stránky v návrháři (DesignerCanvas
    // PagePaddingPx 40 + TopBufferPx 60). Náhled tak začíná na ose Y zarovnaný s plátnem vlevo.
    private const float PageTopPaddingPx = 66f;
    private const float PageGapPx     = 16f;
    private const float BaseMmToPx   = 96f / 25.4f;   // px na mm při 96 DPI, zoom 1:1

    private static readonly SKColor ClrPage        = SKColors.White;
    private static readonly SKColor ClrPageBorder  = new(0xFFAAAAAA);
    private static readonly SKColor ClrOverflowBdr = new(0xFFD32F2F);   // rámeček přetékající stránky

    /// <summary>
    /// Volá se na konci každého renderu preview s popisem toho, co se nevejde a kde (null = vejde
    /// se všechno). Wire-uje MainWindow na status bar. ⚠️ Posílá se **text, ne příznak** - z „něco
    /// přetéká" uživatel nepoznal, na které stránce ani co ji přetéká.
    /// </summary>
    public Action<IReadOnlyList<Hlaseni>>? OverflowChanged { get; set; }

    /// <summary>Na kolik stran se sází právě zobrazená stránka dat - druhá osa stránkování (`B131`).</summary>
    public Action<int>? PocetStranSazbyChanged { get; set; }

    /// <summary>
    /// Volá se na konci každého renderu preview s popisem buněk, jejichž obsah se nevešel ani po
    /// zmenšení na mez, takže se ořízl (null = všechno se vešlo). Wire-uje MainWindow na status bar.
    /// </summary>
    public Action<IReadOnlyList<Hlaseni>>? OriznuteBunkyChanged { get; set; }

    /// <summary>
    /// Volá se při každém renderu preview s popisem tabulek spojů, kde je sloupec užší, než se
    /// obsah vejde (null = v pořádku). Wire-uje MainWindow na týž odznak status baru.
    /// </summary>
    public Action<IReadOnlyList<Hlaseni>>? UzkeSloupceChanged { get; set; }

    /// <summary>Co sazba sama upravila nebo nesplnila - zmenšení, Počet stran, nečitelné podmínky
    /// (`B178`). Volá se při každém renderu náhledu.</summary>
    public Action<IReadOnlyList<Hlaseni>>? SazbaChanged { get; set; }

    /// <summary>Sběrač oříznutých buněk právě probíhajícího renderu; mimo render null.</summary>
    private ClippedCells? _oriznuteBunky;

    private readonly DesignerState _state;
    private readonly PageLayoutEngine _engine;
    private readonly IElementHeightCalculator _heightCalc;

    public PreviewCanvas(DesignerState state)
    {
        _state = state;
        _heightCalc = new SkiaHeightCalculator();
        _engine = new PageLayoutEngine(_heightCalc);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Veřejné pomocné metody (volá MainWindow pro určení velikosti SKElement)

    /// <summary>
    /// Vypočítá zoom tak, aby se šablona vešla do dostupné šířky panelu.
    /// </summary>
    public float ComputeFitZoom(float availableWidth)
    {
        float pageW = _state.CurrentTemplate.PageSize.Width;
        float fitZoom = (availableWidth - 2 * PagePaddingPx) / (pageW * BaseMmToPx);
        return Math.Clamp(fitZoom, 0.2f, 10.0f);
    }

    /// <summary>Šířka SKElementu v px pro daný zoom - zahrnuje PagePadding na obou stranách.</summary>
    public float ComputePageWidthPx(float zoom)
    {
        float pageW = _state.CurrentTemplate.PageSize.Width;
        return 2 * PagePaddingPx + pageW * BaseMmToPx * zoom;
    }

    /// <summary>Spustí layout engine a zároveň synchronizuje ambient výchozí styl (font) na aktuální
    /// šablonu. Tím všechny font-resolve cesty (preview render, PDF/PNG export, měření přetečení/výšky)
    /// používají správný font i bez předchozího paintu - font se mění přes EDInputForm reflexí, takže se
    /// na něj nedá spolehnout přes RebuildSections/Render. Jediný chokepoint místo per-metoda syncu.</summary>
    private LayoutResult Layout(DataContext? data) => Layout(data, _state.CurrentTemplate);

    private LayoutResult Layout(DataContext? data, Template template)
    {
        DocumentDefaults.Style = template.DefaultTextStyle;
        return _engine.Layout(template, data);
    }

    /// <summary>Výška všech stránek v px při daném zoomu - pro nastavení výšky SKElement.</summary>
    public float CalculatePageHeightPx(float zoom, DataContext? data = null)
    {
        var layout = Layout(data);
        int pageCount = Math.Max(1, ZobrazeneStranky(layout).Count);
        float pageH = _state.CurrentTemplate.PageSize.Height * BaseMmToPx * zoom;
        return PageTopPaddingPx + pageCount * pageH + (pageCount - 1) * PageGapPx + PagePaddingPx;
    }

    /// <summary>Výška stránek - zpětná kompatibilita (bez dat = jedna stránka).</summary>
    public float CalculatePageHeightPxLegacy(float zoom)
    {
        float mmToPx = BaseMmToPx * zoom;
        var   ps     = _state.CurrentTemplate.PageSize;
        float px     = ps.MarginTop * mmToPx;
        foreach (var s in _state.CurrentTemplate.Sections)
            px += s.EffectiveDesignerHeight * mmToPx;
        px += ps.MarginBottom * mmToPx;
        return Math.Max(ps.Height * mmToPx, px);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PDF export

    /// <summary>
    /// Exportuje šablonu s dodanými daty do PDF souboru.
    /// Multi-page: každá stránka LayoutResult = jedna stránka PDF.
    /// </summary>
    public void RenderToPdf(string filePath, DataContext? data, SheetLayoutOptions? sheetLayout = null,
        float meritko = 1f) =>
        RenderMultiDataToPdf(filePath, [data!], sheetLayout: sheetLayout, meritko: meritko);

    /// <summary>
    /// Exportuje šablonu s více sadami dat do PDF - každý DataContext = kompletní průchod šablonou.
    /// Pro JŘ: každý DataContext = jedna zastávka/směr → 1+ PDF stránek.
    /// </summary>
    /// <param name="template">Šablona k renderování - snapshot pro export na pozadí
    /// (uživatel může mezitím editovat živou šablonu). Null = aktuální šablona ze State.</param>
    /// <param name="progress">Per datová stránka: (hotovo, celkem).</param>
    /// <param name="sheetLayout">Imposice: skládání logických stránek na fyzické listy (N-up).
    /// Null = 1 stránka = 1 PDF stránka (dnešní chování).</param>
    /// <param name="meritko">Zvětšení hotové sazby při tisku (1 = skutečná velikost). Násobí se
    /// jen převod milimetrů na body, takže se nepřepočítává layout - na stránku jde tentýž počet
    /// řádků a mění se jen rozměr na papíře (`B132`).</param>
    public void RenderMultiDataToPdf(string filePath, IReadOnlyList<DataContext> dataPages,
        Template? template = null, IProgress<(int done, int total)>? progress = null,
        SheetLayoutOptions? sheetLayout = null, float meritko = 1f)
    {
        var tpl = template ?? _state.CurrentTemplate;
        float pointsPerMm = 72f / 25.4f * meritko;
        var   ps    = tpl.PageSize;
        float pageW = ps.Width  * pointsPerMm;
        float pageH = ps.Height * pointsPerMm;

        using var stream = new FileStream(filePath, FileMode.Create);
        using var doc    = SKDocument.CreatePdf(stream);

        if (sheetLayout is not null)
        {
            RenderSheetsToPdf(doc, dataPages, tpl, sheetLayout, pointsPerMm, progress);
            doc.Close();
            return;
        }

        for (int i = 0; i < dataPages.Count; i++)
        {
            var layout = Layout(dataPages[i], tpl);

            foreach (var page in layout.Pages)
            {
                var canvas = doc.BeginPage(pageW, pageH);

                using (var bg = new SKPaint { Color = SKColors.White })
                    canvas.DrawRect(0, 0, pageW, pageH, bg);

                RenderPage(canvas, ps, pointsPerMm, 0, 0, page, naRastr: false);

                doc.EndPage();
            }

            progress?.Report((i + 1, dataPages.Count));
        }

        doc.Close();
    }

    /// <summary>
    /// Imposiční větev PDF exportu: nalayoutuje všechny logické stránky, složí je přes
    /// <see cref="SheetComposer"/> na listy a každý list vyrenderuje jako jednu PDF stránku
    /// (stránky 1:1 s offsetem, blok centrovaný). Progress se hlásí per datová sada ve fázi
    /// layoutu (render listů je pak už rychlý).
    /// </summary>
    private void RenderSheetsToPdf(SKDocument doc, IReadOnlyList<DataContext> dataPages, Template tpl,
        SheetLayoutOptions o, float pointsPerMm, IProgress<(int done, int total)>? progress)
    {
        var ps = tpl.PageSize;
        // Offsety na listu jsou v milimetrech LISTU (nezvětšují se), obsah stránky ano - proto
        // se ořezové značky a pozice kreslí základním převodem a stránka měřítkem.
        float bodyNaMmListu = pointsPerMm / (ps.Width > 0.01f ? o.PageWidth / ps.Width : 1f);
        var doklady = new List<IReadOnlyList<Page>>();
        for (int i = 0; i < dataPages.Count; i++)
        {
            doklady.Add(Layout(dataPages[i], tpl).Pages);
            progress?.Report((i + 1, dataPages.Count));
        }
        // Nakreslené uspořádání (`B186`) skládá podle stránek šablony, dřívější režimy za sebou.
        var listy = o.Plan is not null
            ? SheetComposer.ComposeDle([.. doklady.Select(d => (IReadOnlyList<(Page, int)>)
                  [.. d.Zip(DesignerPages.StrankySablony(tpl, d), (page, stranka) => (page, stranka))])], o)
            : SheetComposer.Compose(SheetComposer.StrankyDokladu(doklady, o), o);

        foreach (var sheet in listy)
        {
            var canvas = doc.BeginPage(o.SheetWidth * bodyNaMmListu, o.SheetHeight * bodyNaMmListu);

            using (var bg = new SKPaint { Color = SKColors.White })
                canvas.DrawRect(0, 0, o.SheetWidth * bodyNaMmListu, o.SheetHeight * bodyNaMmListu, bg);

            foreach (var placed in sheet.Pages)
                RenderPage(canvas, ps, pointsPerMm,
                    placed.OffsetX * bodyNaMmListu, placed.OffsetY * bodyNaMmListu, placed.Page,
                    naRastr: false);

            if (o.DrawCutMarks)
                DrawCutMarks(canvas, o, bodyNaMmListu);

            doc.EndPage();
        }
    }

    /// <summary>Tenké šedé ořezové čáry na všech hranicích stránek bloku (leží přesně na řezu).</summary>
    private static void DrawCutMarks(SKCanvas canvas, SheetLayoutOptions o, float pointsPerMm)
    {
        using var p = new SKPaint
        {
            Color = new SKColor(0x99, 0x99, 0x99),
            IsStroke = true,
            StrokeWidth = 0.15f * pointsPerMm,
            IsAntialias = false
        };
        float top = o.BlockOriginY * pointsPerMm, bottom = (o.BlockOriginY + o.Rows * o.PageHeight) * pointsPerMm;
        float left = o.BlockOriginX * pointsPerMm, right = (o.BlockOriginX + o.Cols * o.PageWidth) * pointsPerMm;

        foreach (var x in o.CutLinesX)
            canvas.DrawLine(x * pointsPerMm, top, x * pointsPerMm, bottom, p);
        foreach (var y in o.CutLinesY)
            canvas.DrawLine(left, y * pointsPerMm, right, y * pointsPerMm, p);
    }

    /// <summary>
    /// Vyrenderuje stránky šablony do PNG souboru/souborů.
    /// Pokud je víc než 1 stránka, jména souborů budou očíslovaná: "name_001.png", "name_002.png", ...
    /// Vrátí počet vytvořených souborů.
    /// </summary>
    /// <param name="filePath">Cílová cesta. Při více stránkách se před příponu vloží "_NNN".</param>
    /// <param name="dataPages">Datové sady pro renderování.</param>
    /// <param name="dpi">Rozlišení (default 150 = print quality, 96 = screen).</param>
    /// <param name="template">Šablona k renderování - snapshot pro export na pozadí. Null = aktuální ze State.</param>
    /// <param name="progress">Per výstupní stránka: (hotovo, celkem).</param>
    public int RenderMultiDataToPng(string filePath, IReadOnlyList<DataContext> dataPages, float dpi = 150f,
        Template? template = null, IProgress<(int done, int total)>? progress = null)
    {
        var tpl = template ?? _state.CurrentTemplate;
        float mmToPx = dpi / 25.4f;
        var   ps     = tpl.PageSize;
        int   wPx    = (int)MathF.Round(ps.Width  * mmToPx);
        int   hPx    = (int)MathF.Round(ps.Height * mmToPx);

        // Posbírat všechny renderované stránky napříč DataContexty
        var renderedPages = new List<Page>();
        foreach (var data in dataPages)
            renderedPages.AddRange(Layout(data, tpl).Pages);

        if (renderedPages.Count == 0) return 0;

        bool multiPage = renderedPages.Count > 1;
        string dir     = Path.GetDirectoryName(filePath) ?? "";
        string baseName = Path.GetFileNameWithoutExtension(filePath);
        string ext     = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";

        for (int i = 0; i < renderedPages.Count; i++)
        {
            using var bitmap = new SKBitmap(wPx, hPx);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            RenderPage(canvas, ps, mmToPx, 0, 0, renderedPages[i]);

            string outPath = multiPage
                ? Path.Combine(dir, $"{baseName}_{i + 1:D3}{ext}")
                : filePath;

            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = new FileStream(outPath, FileMode.Create);
            encoded.SaveTo(fs);

            progress?.Report((i + 1, renderedPages.Count));
        }

        return renderedPages.Count;
    }

    /// <summary>
    /// Spočítá přetékající stránky napříč všemi dodanými datovými sadami - **layout-only, bez renderu**.
    /// Použito v tisku pro agregovanou kontrolu přes všechny instance: data už jsou fetchnutá z Caché,
    /// takže projetí layoutem + <see cref="NajdiPreteceni"/> je levné. Vrací nálezy (co a kde se nevejde)
    /// a celkový počet stránek.
    /// </summary>
    /// <remarks>
    /// Čísla stránek jsou průběžná přes všechny instance, ne od jedničky za každou - v tisku jde
    /// o jeden výstup a uživatel v něm hledá pořadovou stránku.
    /// </remarks>
    /// <summary>
    /// Hlášky ze sazby pro stránky dat jedné linky - přetečení, Počet stran, zmenšení tabulky,
    /// nečitelné podmínky (`B178`). Bez kreslení, jen layout.
    /// </summary>
    public List<Hlaseni> HlaseniSazby(IReadOnlyList<DataContext> dataPages)
    {
        var (nalezy, total) = CountOverflowingPages(dataPages);
        var hlaseni = OverflowWarnings.Seznam(nalezy, total);
        foreach (var data in dataPages)
            hlaseni.AddRange(SazbaHlaseni.Seznam(_state.CurrentTemplate, Layout(data)));
        return [.. hlaseni.Distinct()];
    }

    public (IReadOnlyList<PreteceniNalez> Nalezy, int Total) CountOverflowingPages(IReadOnlyList<DataContext> dataPages)
    {
        var ps = _state.CurrentTemplate.PageSize;
        var nalezy = new List<PreteceniNalez>();
        int total = 0;
        foreach (var data in dataPages)
        {
            foreach (var page in Layout(data).Pages)
            {
                total++;
                if (NajdiPreteceni(page, ps) is { } nalez)
                    nalezy.Add(nalez with { CisloStranky = total });
            }
        }
        return (nalezy, total);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Renderování

    public void Render(SKCanvas canvas, float canvasWidth, float canvasHeight, DataContext? data)
    {
        float zoom   = ComputeFitZoom(canvasWidth);
        float mmToPx = BaseMmToPx * zoom;
        var   ps     = _state.CurrentTemplate.PageSize;
        float pageW  = ps.Width * mmToPx;
        float pageH  = ps.Height * mmToPx;
        float pageX  = Math.Max(PagePaddingPx, (canvasWidth - pageW) / 2f);

        var layout = Layout(data);

        // Pre-detekce přetečení per stránka (funguje bez renderu) → červený rámeček konkrétní
        // přetékající stránky + hláška ve status baru (přes OverflowChanged).
        var nalezy = layout.Pages.Select(p => NajdiPreteceni(p, ps)).ToList();
        OverflowChanged?.Invoke(OverflowWarnings.Seznam(
            [.. nalezy.Where(n => n is not null).Select(n => n!)], layout.Pages.Count));

        // Kolik stran leží pod sebou ve scrollu. Nabídka nad náhledem přepíná stránky DAT, tohle
        // je ta druhá osa - bez ní si uživatel obojí plete (`B131`).
        PocetStranSazbyChanged?.Invoke(layout.Pages.Count);
        SazbaChanged?.Invoke(SazbaHlaseni.Seznam(_state.CurrentTemplate, layout));

        // Úzký sloupec spoje se hlásí odsud, ne z načtení dat: uživatel mění šířku v panelu
        // a hláška musí jít s ním, ne čekat na příští stažení jízdního řádku.
        UzkeSloupceChanged?.Invoke(data is null
            ? []
            : TripColumnWidthWarnings.Seznam(_state.CurrentTemplate, [data]));

        // Jen obrazovkový náhled - export si plochu čistí bílou (viz RenderMultiDataToPng/Pdf).
        canvas.Clear(CanvasTheme.Background);
        if (layout.TotalPages == 0) return;

        // Oříznuté buňky se poznají až při kreslení (šířka sloupce vzniká v rozvržení prvku),
        // takže se sbírají průběžně a hlásí se, teprve až jsou nakreslené všechny strany sazby.
        _oriznuteBunky = new ClippedCells();
        float currentPageY = PageTopPaddingPx;
        var stranky = new List<(float Top, int Predloha)>();
        var predlohy = PredlohyStranek(layout);
        var zobrazene = ZobrazeneStranky(layout).ToHashSet();
        for (int i = 0; i < layout.Pages.Count; i++)
        {
            if (!zobrazene.Contains(i))
            {
                // Skrytá stránka (jiná předloha) se vykreslí naprázdno, jen aby hláška
                // o oříznutých buňkách platila za celý doklad, ne jen za otevřenou stránku.
                using var zaznam = new SKPictureRecorder();
                RenderPage(zaznam.BeginRecording(new SKRect(0, 0, pageW, pageH)), ps, mmToPx, 0, 0,
                    layout.Pages[i], _oriznuteBunky);
                zaznam.EndRecording().Dispose();
                continue;
            }
            stranky.Add((currentPageY, predlohy[i]));
            DrawShadow(canvas, pageX, currentPageY, pageW, pageH);
            DrawPageBackground(canvas, pageX, currentPageY, pageW, pageH);
            RenderPage(canvas, ps, mmToPx, pageX, currentPageY, layout.Pages[i], _oriznuteBunky);
            DrawPageBorder(canvas, pageX, currentPageY, pageW, pageH, nalezy[i] is not null);

            currentPageY += pageH + PageGapPx;
        }
        OriznuteBunkyChanged?.Invoke(_oriznuteBunky.Seznam());
        _oriznuteBunky = null;
        _strankyNahledu = stranky;
    }

    /// <summary>
    /// Ze které předlohy (stránky šablony) vznikla každá stránka sazby - podle první obsahové sekce
    /// na ní; stránka jen s hlavičkou a patičkou patří k předloze předchozí stránky.
    /// </summary>
    private List<int> PredlohyStranek(LayoutResult layout) =>
        DesignerPages.StrankySablony(_state.CurrentTemplate, layout.Pages);

    /// <summary>
    /// Které stránky sazby náhled ukáže: u šablony o víc stránkách bez „Náhled všech stránek" jen ty,
    /// které vznikly z otevřené předlohy (u pevné jedna, u „podle dat" všechny její); jinak všechny.
    /// </summary>
    private List<int> ZobrazeneStranky(LayoutResult layout)
    {
        var vse = Enumerable.Range(0, layout.Pages.Count).ToList();
        int pocetPredloh = DesignerPages.ObsahStranek(_state.CurrentTemplate).Count;
        if (pocetPredloh < 2 || _state.NahledVsechStranek) return vse;
        int otevrena = Math.Clamp(_state.Predloha, 0, pocetPredloh - 1);
        var predlohy = PredlohyStranek(layout);
        return [.. vse.Where(i => predlohy[i] == otevrena)];
    }

    // Kde leží stránky posledního vykreslení a ze které předlohy vznikly - pro synchronizaci
    // přepínače předloh v návrhu s posunem náhledu (`B184`).
    private List<(float Top, int Predloha)> _strankyNahledu = [];

    /// <summary>Horní hrana první stránky náhledu, která vznikla z předlohy; null = žádná.</summary>
    public float? TopPredlohy(int predloha) =>
        _strankyNahledu.FirstOrDefault(s => s.Predloha == predloha) is { Top: > 0 } s ? s.Top : null;

    /// <summary>Předloha stránky náhledu na svislé pozici <paramref name="y"/>; null = nic tam není.</summary>
    public int? PredlohaNa(float y)
    {
        for (int i = _strankyNahledu.Count - 1; i >= 0; i--)
            if (y >= _strankyNahledu[i].Top) return _strankyNahledu[i].Predloha;
        return _strankyNahledu.Count > 0 ? _strankyNahledu[0].Predloha : null;
    }

    /// <summary>
    /// Co se na stránce nevejde, nebo null když se vejde všechno. Hlásí **jen reálné geometrické
    /// přetečení** - element přesahuje spodní hranu své sekce (dynamický element, který narostl, nebo
    /// pevný box větší než sekce). NEhlásí situaci, kdy JŘ element s pevnou výškou musel auto-scalem
    /// zmenšit písmo: ten se do svého boxu vejde, takže stránka nepřetéká (viz
    /// <see cref="ScheduleElementBase.ContentOverflows"/> = „box je menší než obsah", což není přetečení
    /// stránky). Funguje bez renderu (volá se v pre-detekci).
    /// </summary>
    /// <remarks>
    /// Vrací **největší** přesah na stránce: hláška má jmenovat to, co uživatel musí zmenšit nejvíc.
    /// ⚠️ Hledá se jen přesah sekce za stránku, ne prvku za sekci - engine sekci nafoukne na obsah
    /// (<c>max(nastavená výška, obsah)</c>), takže prvek ji nepřesáhne nikdy. Do 3. 9. tady byla
    /// i podmínka „prvek přesahuje sekci", která z tohoto důvodu nemohla nastat.
    /// </remarks>
    private PreteceniNalez? NajdiPreteceni(Page page, PageSize ps)
    {
        const float tolMm = 0.5f;   // tolerance proti IEEE-754 false-positive na přesné hranici
        PreteceniNalez? nejhorsi = null;

        foreach (var placed in page.Sections)
        {
            // Jen KeepTogether sekce mohou obsah reálně oříznout. SplitAcrossPages i AllowSplit
            // engine chunkuje přes stránky (ClipTop/ClipBottom) → obsah se nikdy neztratí, nehlásit.
            if (placed.Source.OverflowBehavior != SectionOverflowBehavior.KeepTogether) continue;

            // KeepTogether nelze rozdělit: pokud sekce přesáhne spodní hranu tiskové plochy stránky,
            // obsah pod hranou se ořízne → reálné přetečení. (placed.Y je relativní k MarginTop.)
            float presah = placed.Y + placed.Height - ps.PrintableHeight;
            if (presah <= tolMm) continue;
            if (nejhorsi is not null && presah <= nejhorsi.PresahMm) continue;

            nejhorsi = NejvetsiPrvek(placed, ps) is { } vinnik
                ? new PreteceniNalez(page.PageNumber, DruhPreteceni.ObsahPrvku,
                                     placed.Source.Name, ElementNames.SPopisem(vinnik), presah, vinnik.Id)
                : new PreteceniNalez(page.PageNumber, DruhPreteceni.NastavenaVyskaSekce,
                                     placed.Source.Name, null, presah);
        }
        return nejhorsi;
    }

    /// <summary>
    /// Prvek, který sekci nafoukl - sahá nejdál dolů a zároveň za její nastavenou výšku. Null =
    /// sekci nafoukla nastavená výška, prvky se do ní vejdou (pak nemá smysl jmenovat prvek, ten
    /// za to nemůže).
    /// </summary>
    private TemplateElement? NejvetsiPrvek(PlacedSection placed, PageSize ps)
    {
        float mez = placed.Source.DesignerHeight ?? 0f;
        TemplateElement? vinnik = null;
        float nejdal = mez;

        foreach (var el in placed.Source.Elements)
        {
            if (!ExpressionEvaluator.IsVisible(el.VisibilityExpression, placed.Data)) continue;
            float dno = el.Y + (el.Height ?? _heightCalc.CalculateHeight(el, ps.PrintableWidth, placed.Data));
            if (dno <= nejdal) continue;
            nejdal = dno;
            vinnik = el;
        }
        return vinnik;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Interní renderovací metody

    private static void DrawShadow(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var p = new SKPaint
        {
            Color      = new SKColor(0x35000000),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
        };
        canvas.DrawRect(new SKRect(x + 4, y + 4, x + w + 4, y + h + 4), p);
    }

    private static void DrawPageBackground(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var p = new SKPaint { Color = ClrPage };
        canvas.DrawRect(new SKRect(x, y, x + w, y + h), p);
    }

    private static void DrawPageBorder(SKCanvas canvas, float x, float y, float w, float h, bool overflow = false)
    {
        using var p = overflow
            ? new SKPaint { Color = ClrOverflowBdr, IsStroke = true, StrokeWidth = 2.5f, IsAntialias = true }
            : new SKPaint { Color = ClrPageBorder, IsStroke = true, StrokeWidth = 1f };
        canvas.DrawRect(new SKRect(x, y, x + w, y + h), p);
    }

    /// <summary>Renderuje jednu stránku LayoutResult. Preview zrcadlí výstup (PDF/PNG/tisk) 1:1 -
    /// žádné orientační pomůcky navíc; hranice sekcí ukazuje jen editační plátno (DesignerCanvas).</summary>
    /// <param name="naRastr">Kreslí se na obrazovku nebo do PNG - čáry se pak srovnají na pixel.
    /// Pro PDF <c>false</c>: tam je jednotkou bod a srovnání by čáry posunulo.</param>
    private static void RenderPage(SKCanvas canvas, PageSize ps, float mmToPx,
        float pageX, float pageY, Page page, ClippedCells? oriznuteBunky = null,
        bool naRastr = true)
    {
        float ox = pageX + ps.MarginLeft * mmToPx;
        float contentW = ps.PrintableWidth * mmToPx;
        float marginTopPx = ps.MarginTop * mmToPx;

        // Seřadit placed sections podle Y
        var sortedSections = page.Sections.OrderBy(s => s.Y).ToList();

        for (int i = 0; i < sortedSections.Count; i++)
        {
            var placed = sortedSections[i];
            float sectionTopPx = pageY + marginTopPx + placed.Y * mmToPx;
            float sectionHeightPx = placed.Height * mmToPx;

            // Clip oblast sekce
            var clipRect = new SKRect(ox, sectionTopPx, ox + contentW, sectionTopPx + sectionHeightPx);

            canvas.Save();
            canvas.ClipRect(clipRect);

            // Pro split sekce: posunout origin nahoru o ClipTop. Když stránka opakuje hlavičku
            // rozříznutého dílu, posunout obsah ještě o ni dolů - místo pro ni vyhradil engine.
            float originYOffset = placed.ClipTop.HasValue ? -placed.ClipTop.Value * mmToPx : 0f;
            originYOffset += placed.OpakovanaHlavickaMm * mmToPx;

            var ctx = new RenderContext(canvas, mmToPx, placed.Data)
            {
                SectionOriginX = ox,
                SectionOriginY = sectionTopPx + originYOffset,
                ClipTopMm = placed.ClipTop ?? 0f,
                ViditelnyPasMm = placed.Height,
                OpakovanaHlavickaMm = placed.OpakovanaHlavickaMm,
                ClippedCells = oriznuteBunky,
                SirkaSekceMm = ps.PrintableWidth,
                SrovnatCaryNaPixel = naRastr,
            };

            // Barvy z dat („pozadí = barva linky") se čtou z property elementu, kam se DataContext
            // nedostane - kreslení ho proto na tuto sekci zveřejní přes ColorHex.
            using var colorScope = ColorHex.DataScope(placed.Data);
            // Formulář vlastností se plní mimo kreslení, takže si data uložíme i mimo scope -
            // jinak by u barvy z dat neměl z čeho vzít náhledovou barvu do čtverečku.
            if (placed.Data is not null) ColorHex.UiData = placed.Data;

            // Renderovat viditelné elementy seřazené podle ZIndex
            var visibleElements = placed.Source.Elements
                .Where(e => ExpressionEvaluator.IsVisible(e.VisibilityExpression, placed.Data))
                .OrderBy(e => e.ZIndex)
                .ToList();

            foreach (var el in visibleElements)
            {
                el.RenderBackgroundPass(ctx);   // pozadí (respektuje rohy) hned před obsahem téhož elementu
                canvas.Save();
                if (el.IsRotated)
                {
                    var (cx, cy) = el.GetCenterPx(ctx);
                    canvas.RotateDegrees(el.Rotation, cx, cy);
                }
                el.ApplyCornerClip(ctx);   // ořez obsahu na tvar rohů (no-op bez rohů) - jedno místo pro všechny
                // Hláška o oříznuté buňce má jmenovat prvek; kreslení buňky o prvku neví, tak mu
                // jméno podrží kreslicí smyčka.
                if (oriznuteBunky is not null)
                {
                    oriznuteBunky.CurrentElement = ElementNames.SPopisem(el);
                    oriznuteBunky.CurrentElementId = el.Id;
                }
                el.Render(ctx);
                canvas.Restore();
            }

            // Rámeček sekce (volitelný - ShowBorder property)
            // Tloušťka 0 = bez rámečku; Skia by čáru s nulovou tloušťkou nakreslila vlasovou.
            if (placed.Source.ShowBorder && placed.Source.BorderWidth > 0f)
            {
                using var borderPaint = new SKPaint
                {
                    Color = ColorHex.ParseOrBlack(placed.Source.BorderColor),
                    IsStroke = true,
                    StrokeWidth = placed.Source.BorderWidth * mmToPx,
                    IsAntialias = true
                };
                canvas.DrawRect(clipRect, borderPaint);
            }

            canvas.Restore();

            // Druhý průchod - rámečky mimo TĚSNÝ section-clip (ať jsou vidět i na hranici sekce),
            // ale stále v rámci STRÁNKY. Bez toho by se u rozdělené (split) sekce rámečky elementů
            // z jiného chunku vykreslily i sem: posun originu o ClipTop je odsune mimo stránku, ale
            // bez clipu by přesto prosákly (viz „dvojité vykreslení" u sekce vyšší než stránka).
            canvas.Save();
            canvas.ClipRect(new SKRect(pageX, pageY, pageX + ps.Width * mmToPx, pageY + ps.Height * mmToPx));
            foreach (var el in visibleElements)
                el.RenderBorderPass(ctx);
            canvas.Restore();
        }
    }
}
