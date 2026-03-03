using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Designer;

/// <summary>
/// Renderuje šablonu pro live preview — bez designerového chrome (záhlaví sekcí,
/// handles, badgů, mřížky). Elementy s visibilityExpression="false" se nezobrazí.
/// Zoom se počítá automaticky podle šířky dostupného canvasu (fit-to-width).
/// </summary>
public class PreviewCanvas
{
    private const float PagePaddingPx = 20f;
    private const float BaseMmToPx   = 96f / 25.4f;   // px na mm při 96 DPI, zoom 1:1

    private static readonly SKColor ClrBackground = new(0xFFE8E8E8);
    private static readonly SKColor ClrPage       = SKColors.White;
    private static readonly SKColor ClrPageBorder = new(0xFFAAAAAA);
    private static readonly SKColor ClrSectionSep = new(0xFFDDDDDD);

    private readonly DesignerState _state;

    public PreviewCanvas(DesignerState state) => _state = state;

    // ─────────────────────────────────────────────────────────────────────
    // Veřejné pomocné metody (volá MainWindow pro určení velikosti SKElement)

    /// <summary>
    /// Vypočítá zoom tak, aby se šablona vešla do dostupné šířky panelu.
    /// </summary>
    public float ComputeFitZoom(float availableWidth)
    {
        float pageW = _state.CurrentTemplate.PageSize.Width;
        float fitZoom = (availableWidth - 2 * PagePaddingPx) / (pageW * BaseMmToPx);
        return Math.Clamp(fitZoom, 0.2f, 3.0f);
    }

    /// <summary>Výška stránky v px při daném zoomu — pro nastavení výšky SKElement.</summary>
    public float CalculatePageHeightPx(float zoom)
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
    // Renderování

    public void Render(SKCanvas canvas, float canvasWidth, float canvasHeight, DataContext? data)
    {
        canvas.Clear(ClrBackground);

        float zoom   = ComputeFitZoom(canvasWidth);
        float mmToPx = BaseMmToPx * zoom;
        var   ps     = _state.CurrentTemplate.PageSize;
        float pageW  = ps.Width * mmToPx;
        float pageH  = CalculatePageHeightPx(zoom);
        float pageX  = Math.Max(PagePaddingPx, (canvasWidth - pageW) / 2f);
        float pageY  = PagePaddingPx;

        DrawShadow(canvas, pageX, pageY, pageW, pageH);
        DrawPageBackground(canvas, pageX, pageY, pageW, pageH);
        DrawContent(canvas, ps, mmToPx, pageX, pageY, data);
        DrawPageBorder(canvas, pageX, pageY, pageW, pageH);
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

    private static void DrawPageBorder(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var p = new SKPaint { Color = ClrPageBorder, IsStroke = true, StrokeWidth = 1f };
        canvas.DrawRect(new SKRect(x, y, x + w, y + h), p);
    }

    private void DrawContent(SKCanvas canvas, PageSize ps, float mmToPx, float pageX, float pageY, DataContext? data)
    {
        float ox       = pageX + ps.MarginLeft * mmToPx;
        float currentY = pageY + ps.MarginTop  * mmToPx;
        float contentW = ps.PrintableWidth * mmToPx;

        foreach (var section in _state.CurrentTemplate.Sections)
        {
            float sectionH = section.EffectiveDesignerHeight * mmToPx;
            var clipRect   = new SKRect(ox, currentY, ox + contentW, currentY + sectionH);

            canvas.Save();
            canvas.ClipRect(clipRect);

            var ctx = new RenderContext(canvas, mmToPx, data)
            {
                SectionOriginX = ox,
                SectionOriginY = currentY
            };

            // Elementy s podmínkou "false" se v preview nezobrazí
            foreach (var el in section.Elements
                .Where(e => !string.Equals(e.VisibilityExpression, "false", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.ZIndex))
            {
                el.Render(ctx);
            }

            canvas.Restore();
            currentY += sectionH;

            // Jemná čára mezi sekcemi (orientační pomůcka)
            using var sep = new SKPaint { Color = ClrSectionSep, StrokeWidth = 0.5f };
            canvas.DrawLine(ox, currentY, ox + contentW, currentY, sep);
        }
    }
}
