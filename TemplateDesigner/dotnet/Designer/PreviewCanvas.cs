using System.IO;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Designer;

/// <summary>
/// Renderuje šablonu pro live preview — bez designerového chrome (záhlaví sekcí,
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

    private static readonly SKColor ClrBackground  = new(0xFFE4E4E4);   // shodné s DesignerCanvas
    private static readonly SKColor ClrPage        = SKColors.White;
    private static readonly SKColor ClrPageBorder  = new(0xFFAAAAAA);
    private static readonly SKColor ClrOverflowBdr = new(0xFFD32F2F);   // rámeček přetékající stránky

    /// <summary>Volá se na konci každého renderu preview: true = obsah některé stránky přetéká. Wire-uje MainWindow na status bar.</summary>
    public Action<bool>? OverflowChanged { get; set; }

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

    /// <summary>Šířka SKElementu v px pro daný zoom — zahrnuje PagePadding na obou stranách.</summary>
    public float ComputePageWidthPx(float zoom)
    {
        float pageW = _state.CurrentTemplate.PageSize.Width;
        return 2 * PagePaddingPx + pageW * BaseMmToPx * zoom;
    }

    /// <summary>Spustí layout engine a zároveň synchronizuje ambient výchozí styl (font) na aktuální
    /// šablonu. Tím všechny font-resolve cesty (preview render, PDF/PNG export, měření přetečení/výšky)
    /// používají správný font i bez předchozího paintu — font se mění přes EDInputForm reflexí, takže se
    /// na něj nedá spolehnout přes RebuildSections/Render. Jediný chokepoint místo per-metoda syncu.</summary>
    private LayoutResult Layout(DataContext? data) => Layout(data, _state.CurrentTemplate);

    private LayoutResult Layout(DataContext? data, Template template)
    {
        DocumentDefaults.Style = template.DefaultTextStyle;
        return _engine.Layout(template, data);
    }

    /// <summary>Výška všech stránek v px při daném zoomu — pro nastavení výšky SKElement.</summary>
    public float CalculatePageHeightPx(float zoom, DataContext? data = null)
    {
        var layout = Layout(data);
        int pageCount = Math.Max(1, layout.TotalPages);
        float pageH = _state.CurrentTemplate.PageSize.Height * BaseMmToPx * zoom;
        return PageTopPaddingPx + pageCount * pageH + (pageCount - 1) * PageGapPx + PagePaddingPx;
    }

    /// <summary>Výška stránek — zpětná kompatibilita (bez dat = jedna stránka).</summary>
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
    public void RenderToPdf(string filePath, DataContext? data, SheetLayoutOptions? sheetLayout = null) =>
        RenderMultiDataToPdf(filePath, [data!], sheetLayout: sheetLayout);

    /// <summary>
    /// Exportuje šablonu s více sadami dat do PDF — každý DataContext = kompletní průchod šablonou.
    /// Pro JŘ: každý DataContext = jedna zastávka/směr → 1+ PDF stránek.
    /// </summary>
    /// <param name="template">Šablona k renderování — snapshot pro export na pozadí
    /// (uživatel může mezitím editovat živou šablonu). Null = aktuální šablona ze State.</param>
    /// <param name="progress">Per datová stránka: (hotovo, celkem).</param>
    /// <param name="sheetLayout">Imposice: skládání logických stránek na fyzické listy (N-up).
    /// Null = 1 stránka = 1 PDF stránka (dnešní chování).</param>
    public void RenderMultiDataToPdf(string filePath, IReadOnlyList<DataContext> dataPages,
        Template? template = null, IProgress<(int done, int total)>? progress = null,
        SheetLayoutOptions? sheetLayout = null)
    {
        var tpl = template ?? _state.CurrentTemplate;
        const float pointsPerMm = 72f / 25.4f;
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

                RenderPage(canvas, ps, pointsPerMm, 0, 0, page);

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
        var logicalPages = new List<Page>();
        for (int i = 0; i < dataPages.Count; i++)
        {
            logicalPages.AddRange(Layout(dataPages[i], tpl).Pages);
            progress?.Report((i + 1, dataPages.Count));
        }

        foreach (var sheet in SheetComposer.Compose(logicalPages, o))
        {
            var canvas = doc.BeginPage(o.SheetWidth * pointsPerMm, o.SheetHeight * pointsPerMm);

            using (var bg = new SKPaint { Color = SKColors.White })
                canvas.DrawRect(0, 0, o.SheetWidth * pointsPerMm, o.SheetHeight * pointsPerMm, bg);

            foreach (var placed in sheet.Pages)
                RenderPage(canvas, ps, pointsPerMm, placed.OffsetX * pointsPerMm, placed.OffsetY * pointsPerMm, placed.Page);

            if (o.DrawCutMarks)
                DrawCutMarks(canvas, o, pointsPerMm);

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
    /// <param name="template">Šablona k renderování — snapshot pro export na pozadí. Null = aktuální ze State.</param>
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
    /// Spočítá přetékající stránky napříč všemi dodanými datovými sadami — **layout-only, bez renderu**.
    /// Použito v tisku pro agregovanou kontrolu přes všechny instance: data už jsou fetchnutá z Caché,
    /// takže projetí layoutem + <see cref="PageOverflows"/> je levné. Vrací počet přetékajících a celkový
    /// počet stránek.
    /// </summary>
    public (int Overflowing, int Total) CountOverflowingPages(IReadOnlyList<DataContext> dataPages)
    {
        var ps = _state.CurrentTemplate.PageSize;
        int overflowing = 0, total = 0;
        foreach (var data in dataPages)
        {
            foreach (var page in Layout(data).Pages)
            {
                total++;
                if (PageOverflows(page, ps)) overflowing++;
            }
        }
        return (overflowing, total);
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
        var overflowPages = layout.Pages.Select(p => PageOverflows(p, ps)).ToList();
        OverflowChanged?.Invoke(overflowPages.Any(x => x));

        canvas.Clear(ClrBackground);
        if (layout.TotalPages == 0) return;

        float currentPageY = PageTopPaddingPx;
        for (int i = 0; i < layout.Pages.Count; i++)
        {
            DrawShadow(canvas, pageX, currentPageY, pageW, pageH);
            DrawPageBackground(canvas, pageX, currentPageY, pageW, pageH);
            RenderPage(canvas, ps, mmToPx, pageX, currentPageY, layout.Pages[i]);
            DrawPageBorder(canvas, pageX, currentPageY, pageW, pageH, overflowPages[i]);

            currentPageY += pageH + PageGapPx;
        }
    }

    /// <summary>
    /// Vejde se obsah stránky? Hlásí **jen reálné geometrické přetečení** — element přesahuje spodní hranu
    /// své sekce (dynamický element, který narostl, nebo pevný box větší než sekce). NEhlásí situaci, kdy
    /// JŘ element s pevnou výškou musel auto-scalem zmenšit písmo: ten se do svého boxu vejde, takže stránka
    /// nepřetéká (viz <see cref="ScheduleElementBase.ContentOverflows"/> = „box je menší než obsah", což není
    /// přetečení stránky). Funguje bez renderu (volá se v pre-detekci).
    /// </summary>
    private bool PageOverflows(Page page, PageSize ps)
    {
        const float tolMm = 0.5f;   // tolerance proti IEEE-754 false-positive na přesné hranici
        foreach (var placed in page.Sections)
        {
            // Jen KeepTogether sekce mohou obsah reálně oříznout. SplitAcrossPages i AllowSplit
            // engine chunkuje přes stránky (ClipTop/ClipBottom) → obsah se nikdy neztratí, nehlásit.
            if (placed.Source.OverflowBehavior != SectionOverflowBehavior.KeepTogether) continue;

            // KeepTogether nelze rozdělit: pokud sekce přesáhne spodní hranu tiskové plochy stránky,
            // obsah pod hranou se ořízne → reálné přetečení. (placed.Y je relativní k MarginTop.)
            if (placed.Y + placed.Height > ps.PrintableHeight + tolMm) return true;

            foreach (var el in placed.Source.Elements)
            {
                if (!ExpressionEvaluator.IsVisible(el.VisibilityExpression, placed.Data)) continue;
                float h = el.Height ?? _heightCalc.CalculateHeight(el, ps.PrintableWidth, placed.Data);
                if (el.Y + h > placed.Height + tolMm) return true;
            }
        }
        return false;
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

    /// <summary>Renderuje jednu stránku LayoutResult. Preview zrcadlí výstup (PDF/PNG/tisk) 1:1 —
    /// žádné orientační pomůcky navíc; hranice sekcí ukazuje jen editační plátno (DesignerCanvas).</summary>
    private static void RenderPage(SKCanvas canvas, PageSize ps, float mmToPx,
        float pageX, float pageY, Page page)
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

            // Pro split sekce: posunout origin nahoru o ClipTop
            float originYOffset = placed.ClipTop.HasValue ? -placed.ClipTop.Value * mmToPx : 0f;

            var ctx = new RenderContext(canvas, mmToPx, placed.Data)
            {
                SectionOriginX = ox,
                SectionOriginY = sectionTopPx + originYOffset
            };

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
                el.ApplyCornerClip(ctx);   // ořez obsahu na tvar rohů (no-op bez rohů) — jedno místo pro všechny
                el.Render(ctx);
                canvas.Restore();
            }

            // Rámeček sekce (volitelný — ShowBorder property)
            if (placed.Source.ShowBorder)
            {
                using var borderPaint = new SKPaint
                {
                    Color = SKColor.Parse(placed.Source.BorderColor),
                    IsStroke = true,
                    StrokeWidth = placed.Source.BorderWidth * mmToPx,
                    IsAntialias = true
                };
                canvas.DrawRect(clipRect, borderPaint);
            }

            canvas.Restore();

            // Druhý průchod — rámečky mimo TĚSNÝ section-clip (ať jsou vidět i na hranici sekce),
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
