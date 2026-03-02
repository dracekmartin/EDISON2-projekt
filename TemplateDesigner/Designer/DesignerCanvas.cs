using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Designer;

public enum ResizeHandle
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft,         MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

/// <summary>
/// Renderuje šablonu na SKCanvas a poskytuje hit testing pro interakci myší.
/// </summary>
public class DesignerCanvas
{
    public const float SectionHeaderPx = 20f;
    private const float PagePaddingPx  = 40f;
    private const float HandleSizePx   = 7f;

    // Barvy designeru
    private static readonly SKColor ClrBackground         = new(0xFFE4E4E4);
    private static readonly SKColor ClrPage               = SKColors.White;
    private static readonly SKColor ClrPageBorder         = new(0xFFAAAAAA);
    private static readonly SKColor ClrMarginGuide        = new(0x35FF6600);
    private static readonly SKColor ClrGrid               = new(0x22000080);
    private static readonly SKColor ClrSectionHeader      = new(0xFF4A90D9);
    private static readonly SKColor ClrSectionHeaderDyn   = new(0xFF3A7AC9);
    private static readonly SKColor ClrSectionHeaderText  = SKColors.White;
    private static readonly SKColor ClrSectionContent     = SKColors.White;
    private static readonly SKColor ClrSectionContentDyn  = new(0xFFF0F6FF);
    private static readonly SKColor ClrSectionBorder      = new(0xFFCCCCCC);
    private static readonly SKColor ClrSectionActive      = new(0xFF2196F3);
    private static readonly SKColor ClrSelection          = new(0xFF2196F3);
    private static readonly SKColor ClrLocked             = new(0xFFFF8C00); // jantarová pro zamčené elementy

    private readonly DesignerState _state;

    public DesignerCanvas(DesignerState state) => _state = state;

    public float MmToPx => (96f / 25.4f) * _state.Zoom;

    // ─────────────────────────────────────────────────────────────────────
    // Render

    public void Render(SKCanvas canvas, float canvasWidth, float canvasHeight)
    {
        canvas.Clear(ClrBackground);

        var template = _state.CurrentTemplate;
        float pageW  = template.PageSize.Width * MmToPx;
        float pageH  = CalculatePageHeightPx(template);
        float pageX  = Math.Max(PagePaddingPx, (canvasWidth - pageW) / 2f);
        float pageY  = PagePaddingPx;

        DrawPageShadow(canvas, pageX, pageY, pageW, pageH);
        DrawPageBackground(canvas, pageX, pageY, pageW, pageH);
        if (_state.ShowGrid) DrawGrid(canvas, pageX, pageY, pageW, template.PageSize);
        DrawMarginGuides(canvas, pageX, pageY, template.PageSize);
        DrawSections(canvas, template, pageX, pageY, canvasWidth);
        DrawPageBorder(canvas, pageX, pageY, pageW, pageH);

        // Selection handles kreslíme naposled — vždy nad elementy
        DrawSelectionOverlay(canvas, canvasWidth);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Veřejné pomocné metody pro MainWindow (hit testing, layout)

    /// <summary>Vrátí sekci pod px pozicí na canvasu, nebo null.</summary>
    public Section? HitTestSection(float canvasX, float canvasY, float canvasWidth)
    {
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            float sectionH = section.EffectiveDesignerHeight * MmToPx;
            float sectionW = _state.CurrentTemplate.PageSize.PrintableWidth * MmToPx;
            float headerY  = oy - SectionHeaderPx;

            if (canvasX >= ox && canvasX <= ox + sectionW &&
                canvasY >= headerY && canvasY <= oy + sectionH)
                return section;
        }
        return null;
    }

    /// <summary>Vrátí element a jeho sekci pod px pozicí na canvasu, nebo null.</summary>
    public (Section Section, TemplateElement Element)? HitTestElement(float canvasX, float canvasY, float canvasWidth)
    {
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            float sectionW = _state.CurrentTemplate.PageSize.PrintableWidth * MmToPx;
            float sectionH = section.EffectiveDesignerHeight * MmToPx;

            if (canvasX < ox || canvasX > ox + sectionW) continue;
            if (canvasY < oy || canvasY > oy + sectionH) continue;

            // Procházíme od nejvyššího ZIndex (klikne se na element "nahoře")
            foreach (var el in section.Elements.OrderByDescending(e => e.ZIndex))
            {
                float ex = ox + el.X * MmToPx;
                float ey = oy + el.Y * MmToPx;
                float ew = el.Width * MmToPx;
                float eh = (el.Height ?? el.EffectiveDesignerHeight) * MmToPx;

                if (canvasX >= ex && canvasX <= ex + ew &&
                    canvasY >= ey && canvasY <= ey + eh)
                    return (section, el);
            }
        }
        return null;
    }

    /// <summary>
    /// Vrátí resize handle a element pod px pozicí na canvasu, nebo null.
    /// Testuje pouze vybrané elementy (handles jsou viditelné jen pro ně).
    /// Zamčené elementy resize handles nemají.
    /// </summary>
    public (TemplateElement Element, ResizeHandle Handle)? HitTestHandle(float canvasX, float canvasY, float canvasWidth)
    {
        // Handles jsou viditelné jen při výběru jednoho odemčeného elementu
        if (_state.SelectedElements.Count != 1) return null;
        if (_state.SelectedElements[0].IsLocked) return null;

        float hs = HandleSizePx / 2f;

        foreach (var el in _state.SelectedElements)
        {
            var rect = GetElementCanvasRect(el, canvasWidth);
            if (rect is null) continue;
            var r = rect.Value;
            float mx = r.MidX, my = r.MidY;

            (float hx, float hy, ResizeHandle handle)[] handles =
            [
                (r.Left,  r.Top,    ResizeHandle.TopLeft),
                (mx,      r.Top,    ResizeHandle.TopCenter),
                (r.Right, r.Top,    ResizeHandle.TopRight),
                (r.Left,  my,       ResizeHandle.MiddleLeft),
                (r.Right, my,       ResizeHandle.MiddleRight),
                (r.Left,  r.Bottom, ResizeHandle.BottomLeft),
                (mx,      r.Bottom, ResizeHandle.BottomCenter),
                (r.Right, r.Bottom, ResizeHandle.BottomRight),
            ];

            foreach (var (hx, hy, handle) in handles)
            {
                if (canvasX >= hx - hs && canvasX <= hx + hs &&
                    canvasY >= hy - hs && canvasY <= hy + hs)
                    return (el, handle);
            }
        }
        return null;
    }

    /// <summary>Vrátí absolutní canvas rect elementu v px, nebo null pokud element není na canvasu.</summary>
    public SKRect? GetElementCanvasRect(TemplateElement element, float canvasWidth)
    {
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            if (!section.Elements.Contains(element)) continue;
            return new SKRect(
                ox + element.X * MmToPx,
                oy + element.Y * MmToPx,
                ox + (element.X + element.Width) * MmToPx,
                oy + (element.Y + (element.Height ?? element.EffectiveDesignerHeight)) * MmToPx);
        }
        return null;
    }

    /// <summary>
    /// Vrátí všechny elementy jejichž obdélník se překrývá s daným canvas rect.
    /// Používá se pro rubber band selection.
    /// </summary>
    public List<TemplateElement> HitTestRect(SKRect canvasRect, float canvasWidth)
    {
        var result = new List<TemplateElement>();
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            foreach (var el in section.Elements)
            {
                float ex = ox + el.X * MmToPx;
                float ey = oy + el.Y * MmToPx;
                float ew = el.Width * MmToPx;
                float eh = (el.Height ?? el.EffectiveDesignerHeight) * MmToPx;
                var elRect = new SKRect(ex, ey, ex + ew, ey + eh);

                if (canvasRect.Left < elRect.Right  && canvasRect.Right  > elRect.Left &&
                    canvasRect.Top  < elRect.Bottom && canvasRect.Bottom > elRect.Top)
                    result.Add(el);
            }
        }
        return result;
    }

    /// <summary>Vrátí origin (levý horní roh obsahu) každé sekce na canvasu.</summary>
    public IEnumerable<(Section Section, float OriginX, float OriginY)> GetSectionOrigins(float canvasWidth)
    {
        var ps      = _state.CurrentTemplate.PageSize;
        float pageW = ps.Width * MmToPx;
        float pageX = Math.Max(PagePaddingPx, (canvasWidth - pageW) / 2f);
        float pageY = PagePaddingPx;

        float ox       = pageX + ps.MarginLeft * MmToPx;
        float currentY = pageY + ps.MarginTop  * MmToPx;

        foreach (var section in _state.CurrentTemplate.Sections)
        {
            float contentY = currentY + SectionHeaderPx;
            yield return (section, ox, contentY);
            currentY = contentY + section.EffectiveDesignerHeight * MmToPx;
        }
    }

    public float CalculatePageHeightPx(Template template)
    {
        float px = template.PageSize.MarginTop * MmToPx;
        foreach (var s in template.Sections)
            px += SectionHeaderPx + s.EffectiveDesignerHeight * MmToPx;
        px += template.PageSize.MarginBottom * MmToPx;
        return Math.Max(template.PageSize.Height * MmToPx, px);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Interní renderovací metody

    private static void DrawPageShadow(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var p = new SKPaint { Color = new SKColor(0x35000000), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f) };
        canvas.DrawRect(new SKRect(x + 5, y + 5, x + w + 5, y + h + 5), p);
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

    private void DrawGrid(SKCanvas canvas, float pageX, float pageY, float pageW, PageSize ps)
    {
        float step  = _state.GridSize * MmToPx;
        float endX  = pageX + pageW;
        float endY  = pageY + ps.Height * MmToPx;
        using var p = new SKPaint { Color = ClrGrid, StrokeWidth = 0.5f };
        for (float gx = pageX + ps.MarginLeft * MmToPx; gx < endX; gx += step)
            canvas.DrawLine(gx, pageY, gx, endY, p);
        for (float gy = pageY + ps.MarginTop  * MmToPx; gy < endY; gy += step)
            canvas.DrawLine(pageX, gy, endX, gy, p);
    }

    private void DrawMarginGuides(SKCanvas canvas, float pageX, float pageY, PageSize ps)
    {
        float ml = ps.MarginLeft * MmToPx, mr = ps.MarginRight  * MmToPx;
        float mt = ps.MarginTop  * MmToPx, mb = ps.MarginBottom * MmToPx;
        float pw = ps.Width * MmToPx, ph = ps.Height * MmToPx;
        using var p = new SKPaint { Color = ClrMarginGuide, StrokeWidth = 0.5f, PathEffect = SKPathEffect.CreateDash([4f, 3f], 0) };
        canvas.DrawLine(pageX + ml,      pageY + mt,      pageX + ml,      pageY + ph - mb, p);
        canvas.DrawLine(pageX + pw - mr, pageY + mt,      pageX + pw - mr, pageY + ph - mb, p);
        canvas.DrawLine(pageX + ml,      pageY + mt,      pageX + pw - mr, pageY + mt,      p);
        canvas.DrawLine(pageX + ml,      pageY + ph - mb, pageX + pw - mr, pageY + ph - mb, p);
    }

    private void DrawSections(SKCanvas canvas, Template template, float pageX, float pageY, float canvasWidth)
    {
        float contentW = template.PageSize.PrintableWidth * MmToPx;

        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            float sectionH = section.EffectiveDesignerHeight * MmToPx;
            bool  dynamic  = section.DesignerHeight is null;
            bool  isActive = section == _state.ActiveSection;
            float headerY  = oy - SectionHeaderPx;

            // Hlavička
            var hdrRect = new SKRect(ox, headerY, ox + contentW, oy);
            using (var bg = new SKPaint { Color = dynamic ? ClrSectionHeaderDyn : ClrSectionHeader })
                canvas.DrawRect(hdrRect, bg);
            using var hdrFont  = new SKFont(SKTypeface.Default, 11f);
            using var hdrText  = new SKPaint { Color = ClrSectionHeaderText, IsAntialias = true };
            canvas.DrawText(section.Name, ox + 8f, headerY + SectionHeaderPx - 5f, hdrFont, hdrText);
            if (dynamic)
                canvas.DrawText("∞", ox + contentW - 18f, headerY + SectionHeaderPx - 5f, hdrFont, hdrText);

            // Obsah sekce
            var contentRect = new SKRect(ox, oy, ox + contentW, oy + sectionH);
            using (var bg = new SKPaint { Color = dynamic ? ClrSectionContentDyn : ClrSectionContent })
                canvas.DrawRect(contentRect, bg);

            canvas.Save();
            canvas.ClipRect(contentRect);
            var ctx = new RenderContext(canvas, MmToPx) { SectionOriginX = ox, SectionOriginY = oy };
            foreach (var el in section.Elements.OrderBy(e => e.ZIndex))
                el.Render(ctx);
            canvas.Restore();

            // Rámeček sekce
            using var border = new SKPaint
            {
                Color       = isActive ? ClrSectionActive : ClrSectionBorder,
                IsStroke    = true,
                StrokeWidth = isActive ? 2f : 1f
            };
            canvas.DrawRect(contentRect, border);

            if (isActive)
            {
                using var accent = new SKPaint { Color = ClrSectionActive, StrokeWidth = 3f };
                canvas.DrawLine(ox, oy, ox, oy + sectionH, accent);
            }
        }
    }

    private void DrawSelectionOverlay(SKCanvas canvas, float canvasWidth)
    {
        if (_state.SelectedElements.Count == 0) return;

        // Ohraničení každého vybraného elementu — jantarová pro zamčené, modrá pro odemčené
        foreach (var el in _state.SelectedElements)
        {
            var rect = GetElementCanvasRect(el, canvasWidth);
            if (rect is null) continue;

            using var selBorder = new SKPaint
            {
                Color       = el.IsLocked ? ClrLocked : ClrSelection,
                IsStroke    = true,
                StrokeWidth = 1.5f,
                PathEffect  = SKPathEffect.CreateDash([5f, 3f], 0)
            };
            canvas.DrawRect(rect.Value, selBorder);
        }

        // Resize handles nebo lock badge — pouze při výběru jednoho elementu
        if (_state.SelectedElements.Count != 1) return;

        var singleEl   = _state.SelectedElements[0];
        var singleRect = GetElementCanvasRect(singleEl, canvasWidth);
        if (singleRect is null) return;
        var r = singleRect.Value;

        if (singleEl.IsLocked)
        {
            // Zamčený element: zobraz lock badge místo resize handles
            DrawLockBadge(canvas, r);
        }
        else
        {
            // Odemčený element: klasické resize handles
            using var handleFill   = new SKPaint { Color = SKColors.White };
            using var handleBorder = new SKPaint { Color = ClrSelection, IsStroke = true, StrokeWidth = 1.5f };

            float hs = HandleSizePx / 2f;
            float mx = r.MidX, my = r.MidY;
            foreach (var (hx, hy) in new[]
            {
                (r.Left, r.Top),    (mx, r.Top),    (r.Right, r.Top),
                (r.Left, my),                       (r.Right, my),
                (r.Left, r.Bottom), (mx, r.Bottom), (r.Right, r.Bottom)
            })
            {
                var hr = new SKRect(hx - hs, hy - hs, hx + hs, hy + hs);
                canvas.DrawRect(hr, handleFill);
                canvas.DrawRect(hr, handleBorder);
            }
        }
    }

    /// <summary>
    /// Kreslí malý jantarový badge se symbolem zámku v pravém horním rohu elementu.
    /// </summary>
    private static void DrawLockBadge(SKCanvas canvas, SKRect elementRect)
    {
        const float sz = 14f;
        float bx = elementRect.Right - sz - 2f;
        float by = elementRect.Top + 2f;

        // Příliš malý element — badge by překryl obsah
        if (elementRect.Width < sz + 6 || elementRect.Height < sz + 6) return;

        using var bg = new SKPaint { Color = ClrLocked, IsAntialias = true };
        canvas.DrawRoundRect(bx, by, sz, sz, 2f, 2f, bg);

        using var font = new SKFont(SKTypeface.Default, 9f);
        using var tp   = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText("L", bx + 4f, by + sz - 3.5f, font, tp);
    }
}
