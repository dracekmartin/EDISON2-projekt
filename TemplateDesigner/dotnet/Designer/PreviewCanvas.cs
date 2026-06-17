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
    private const float PageGapPx     = 16f;
    private const float BaseMmToPx   = 96f / 25.4f;   // px na mm při 96 DPI, zoom 1:1

    private static readonly SKColor ClrBackground  = new(0xFFE8E8E8);
    private static readonly SKColor ClrPage        = SKColors.White;
    private static readonly SKColor ClrPageBorder  = new(0xFFAAAAAA);
    private static readonly SKColor ClrOverflowBdr = new(0xFFD32F2F);   // rámeček přetékající stránky
    private static readonly SKColor ClrSectionSep  = new(0xFFDDDDDD);

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

    /// <summary>Výška všech stránek v px při daném zoomu — pro nastavení výšky SKElement.</summary>
    public float CalculatePageHeightPx(float zoom, DataContext? data = null)
    {
        var layout = _engine.Layout(_state.CurrentTemplate, data);
        int pageCount = Math.Max(1, layout.TotalPages);
        float pageH = _state.CurrentTemplate.PageSize.Height * BaseMmToPx * zoom;
        return PagePaddingPx + pageCount * pageH + (pageCount - 1) * PageGapPx + PagePaddingPx;
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
    public void RenderToPdf(string filePath, DataContext? data) =>
        RenderMultiDataToPdf(filePath, [data!]);

    /// <summary>
    /// Exportuje šablonu s více sadami dat do PDF — každý DataContext = kompletní průchod šablonou.
    /// Pro JŘ: každý DataContext = jedna zastávka/směr → 1+ PDF stránek.
    /// </summary>
    public void RenderMultiDataToPdf(string filePath, IReadOnlyList<DataContext> dataPages)
    {
        const float pointsPerMm = 72f / 25.4f;
        var   ps    = _state.CurrentTemplate.PageSize;
        float pageW = ps.Width  * pointsPerMm;
        float pageH = ps.Height * pointsPerMm;

        using var stream = new FileStream(filePath, FileMode.Create);
        using var doc    = SKDocument.CreatePdf(stream);

        foreach (var data in dataPages)
        {
            var layout = _engine.Layout(_state.CurrentTemplate, data);

            foreach (var page in layout.Pages)
            {
                var canvas = doc.BeginPage(pageW, pageH);

                using (var bg = new SKPaint { Color = SKColors.White })
                    canvas.DrawRect(0, 0, pageW, pageH, bg);

                RenderPage(canvas, ps, pointsPerMm, 0, 0, page);

                doc.EndPage();
            }
        }

        doc.Close();
    }

    /// <summary>
    /// Vyrenderuje stránky šablony do PNG souboru/souborů.
    /// Pokud je víc než 1 stránka, jména souborů budou očíslovaná: "name_001.png", "name_002.png", ...
    /// Vrátí počet vytvořených souborů.
    /// </summary>
    /// <param name="filePath">Cílová cesta. Při více stránkách se před příponu vloží "_NNN".</param>
    /// <param name="dataPages">Datové sady pro renderování.</param>
    /// <param name="dpi">Rozlišení (default 150 = print quality, 96 = screen).</param>
    public int RenderMultiDataToPng(string filePath, IReadOnlyList<DataContext> dataPages, float dpi = 150f)
    {
        float mmToPx = dpi / 25.4f;
        var   ps     = _state.CurrentTemplate.PageSize;
        int   wPx    = (int)MathF.Round(ps.Width  * mmToPx);
        int   hPx    = (int)MathF.Round(ps.Height * mmToPx);

        // Posbírat všechny renderované stránky napříč DataContexty
        var renderedPages = new List<Page>();
        foreach (var data in dataPages)
            renderedPages.AddRange(_engine.Layout(_state.CurrentTemplate, data).Pages);

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
        }

        return renderedPages.Count;
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

        var layout = _engine.Layout(_state.CurrentTemplate, data);

        // Pre-detekce přetečení per stránka (funguje bez renderu) → červený rámeček konkrétní
        // přetékající stránky + hláška ve status baru (přes OverflowChanged).
        var overflowPages = layout.Pages.Select(p => PageOverflows(p, ps)).ToList();
        OverflowChanged?.Invoke(overflowPages.Any(x => x));

        canvas.Clear(ClrBackground);
        if (layout.TotalPages == 0) return;

        float currentPageY = PagePaddingPx;
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

    /// <summary>Renderuje jednu stránku LayoutResult.</summary>
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
                if (el.IsRotated)
                {
                    var (cx, cy) = el.GetCenterPx(ctx);
                    canvas.Save();
                    canvas.RotateDegrees(el.Rotation, cx, cy);
                    el.Render(ctx);
                    canvas.Restore();
                }
                else
                {
                    el.Render(ctx);
                }
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

            // Druhý průchod — rámečky MIMO clip, aby byly vidět i na hranici sekce
            foreach (var el in visibleElements)
                el.RenderBorderPass(ctx);

            // Jemná čára mezi sekcemi (orientační pomůcka)
            if (i < sortedSections.Count - 1)
            {
                using var sep = new SKPaint { Color = ClrSectionSep, StrokeWidth = 0.5f };
                float sepY = sectionTopPx + sectionHeightPx;
                canvas.DrawLine(ox, sepY, ox + contentW, sepY, sep);
            }
        }
    }
}
