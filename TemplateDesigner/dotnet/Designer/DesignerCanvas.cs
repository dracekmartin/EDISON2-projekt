using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

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
    /// <summary>Šířka levého štítku sekce v px (vlevo od stránky). Úzký pruh —
    /// název sekce je otočený o 90° (svisle), takže šetří horizontální místo.</summary>
    public const float SectionTabWidthPx = 26f;
    /// <summary>Minimální výška štítku aby se vešlo textové označení.</summary>
    private const float SectionTabMinLabelPx = 28f;
    private const float PagePaddingPx  = 40f;
    /// <summary>
    /// Extra prostor nad stránkou v designeru — kompenzuje vyšší chrome live preview pane
    /// (header bar + parametry sestavy + page navigation). Drží Y-zarovnání stránky mezi
    /// designerem a preview, aby vizuálně nebyly posunuté na ose Y.
    /// </summary>
    private const float TopBufferPx    = 60f;
    private const float HandleSizePx   = 7f;
    private const float RulerThicknessPx = 20f;
    private const float RulerOffsetPx    = 3f;

    /// <summary>Extra levý prostor, který potřebuje canvas navíc kvůli štítkům sekcí.</summary>
    public float ExtraLeftPx => _state.ShowSectionHeaders ? SectionTabWidthPx : 0f;

    // Barvy designeru
    private static readonly SKColor ClrBackground         = new(0xFFE4E4E4);
    private static readonly SKColor ClrPage               = SKColors.White;
    private static readonly SKColor ClrPageBorder         = new(0xFFAAAAAA);
    private static readonly SKColor ClrMarginGuide        = new(0x35FF6600);
    private static readonly SKColor ClrGrid               = new(0x40000080);
    private static readonly SKColor ClrSectionHeader      = new(0xFF4A90D9);
    private static readonly SKColor ClrSectionHeaderDyn   = new(0xFF3A7AC9);
    private static readonly SKColor ClrSectionHeaderText  = SKColors.White;
    private static readonly SKColor ClrSectionContent     = SKColors.White;
    private static readonly SKColor ClrSectionContentDyn  = new(0xFFF0F6FF);
    private static readonly SKColor ClrSectionBorder      = new(0xFFCCCCCC);
    private static readonly SKColor ClrSectionActive      = new(0xFF2563EB);
    private static readonly SKColor ClrSelection          = new(0xFF2563EB);
    private static readonly SKColor ClrLocked             = new(0xFFFF8C00); // jantarová pro zamčené elementy
    private static readonly SKColor ClrCondition         = new(0xFF7B1FA2); // fialová pro podmíněné elementy
    private static readonly SKColor ClrConditionInvalid  = new(0xFFD32F2F); // červená pro nevalidní výraz
    private static readonly SKColor ClrRulerBg           = new(0xFFF2F2F2);
    private static readonly SKColor ClrRulerBorder       = new(0xFF999999);
    private static readonly SKColor ClrRulerTick         = new(0xFF555555);
    private static readonly SKColor ClrRulerText         = new(0xFF333333);
    private static readonly SKColor ClrRulerHighlight    = new(0x602563EB); // poloprůhledná modrá
    private static readonly SKColor ClrPageBreak         = new(0xFF2D7DD2); // modrá dělící čára stránek (návrhář)


    private readonly DesignerState _state;

    public DesignerCanvas(DesignerState state) => _state = state;

    public float MmToPx => (96f / 25.4f) * _state.Zoom;

    /// <summary>
    /// Viditelná oblast canvas v lokálních souřadnicích (set z MainWindow podle ScrollVieweru).
    /// Default = celý canvas (zero rectangle vypne culling). Používá DrawGrid pro viewport culling
    /// — bez něj při vysokém zoomu kreslíme tisíce linek mimo viditelnou oblast.
    /// </summary>
    public SKRect VisibleViewport { get; set; }

    // ─────────────────────────────────────────────────────────────────────
    // Render

    public void Render(SKCanvas canvas, float canvasWidth, float canvasHeight)
    {
        // Ambient výchozí styl šablony (font) — kořen kaskády; sync na každý render (viz PreviewCanvas).
        DocumentDefaults.Style = _state.CurrentTemplate.DefaultTextStyle;

        // Viewport clip — při vyšším zoomu je SKElement velký (5000+ px), ale viditelná je
        // jen malá část. ClipRect umožní Skia přeskočit draw operace mimo viewport
        // (canvas.Clear, page shadow s blur, sekce atd.). Nepomáhá s alokací a uploadem
        // buffer surface — to je dáno velikostí SKElementu (viz roadmap Virtualized canvas).
        if (VisibleViewport.Width > 0 && VisibleViewport.Height > 0)
            canvas.ClipRect(VisibleViewport);

        canvas.Clear(ClrBackground);

        var template = _state.CurrentTemplate;
        float pageW  = template.PageSize.Width * MmToPx;
        // „Papír" roste s obsahem (součet výšek sekcí) — návrhář už není omezen na jednu A4.
        // Zlomy stránek se vyznačí overlayem (Slice 2); tady jen souvislý papír na plnou výšku.
        float paperH = TotalPaperHeightPx();
        float pageX  = CalculatePageX(canvasWidth);
        float pageY  = PagePaddingPx + TopBufferPx;

        DrawPageShadow(canvas, pageX, pageY, pageW, paperH);
        DrawPageBackground(canvas, pageX, pageY, pageW, paperH);
        DrawMarginGuides(canvas, pageX, pageY, template.PageSize, paperH);
        DrawSections(canvas, template, pageX, pageY, canvasWidth);
        // Grid — kreslen po sekcích, aby byl vidět přes jejich pozadí (v printable area, kde se pozicují elementy).
        if (_state.ShowGrid) DrawGrid(canvas, pageX, pageY, pageW, paperH, template.PageSize);
        DrawPageBorder(canvas, pageX, pageY, pageW, paperH);
        DrawPageBreakLines(canvas, pageX, pageY, pageW, paperH, template.PageSize);

        // Selection handles kreslíme naposled — vždy nad elementy
        DrawSnapTargetHighlights(canvas, canvasWidth);
        DrawSelectionOverlay(canvas, canvasWidth);
        DrawSnapLines(canvas, canvasWidth);
        DrawSectionCenterGuide(canvas, canvasWidth);

        // Pravítka — nad stránkou a vpravo, v existujícím PagePaddingPx prostoru (svislé přes celý papír)
        if (_state.ShowRuler) DrawRulers(canvas, pageX, pageY, pageW, paperH, canvasWidth);

        // Měřící badge během dragu/resize — zcela nahoře
        DrawDragBadge(canvas);
    }

    /// <summary>Floating badge s pozicí/rozměrem během dragu nebo resize.</summary>
    private void DrawDragBadge(SKCanvas canvas)
    {
        var text = _state.DragBadgeText;
        if (string.IsNullOrEmpty(text)) return;

        using var font = new SKFont(SKTypeface.Default, 12f);
        float textW = font.MeasureText(text);
        var metrics = font.Metrics;
        float textH = -metrics.Ascent + metrics.Descent;

        const float padX = 6f, padY = 4f, offsetX = 14f, offsetY = 14f;
        float bx = _state.DragBadgeX + offsetX;
        float by = _state.DragBadgeY + offsetY;
        var rect = new SKRect(bx, by, bx + textW + 2 * padX, by + textH + 2 * padY);

        using var bg = new SKPaint { Color = new SKColor(0xE0333333), IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawRoundRect(rect, 3f, 3f, bg);
        canvas.DrawText(text, bx + padX, by + padY - metrics.Ascent, font, textPaint);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Veřejné pomocné metody pro MainWindow (hit testing, layout)

    /// <summary>Vrátí sekci pokud klik dopadl na levý štítek sekce, nebo null.</summary>
    public Section? HitTestSectionHeader(float canvasX, float canvasY, float canvasWidth)
    {
        if (!_state.ShowSectionHeaders) return null;
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            float sectionH = SectionHeightPx(section);
            float tabLeft  = ox - _state.CurrentTemplate.PageSize.MarginLeft * MmToPx - SectionTabWidthPx;
            float tabRight = ox - _state.CurrentTemplate.PageSize.MarginLeft * MmToPx;

            if (canvasX >= tabLeft && canvasX < tabRight &&
                canvasY >= oy && canvasY <= oy + sectionH)
                return section;
        }
        return null;
    }

    /// <summary>Vrátí sekci pod px pozicí na canvasu, nebo null.</summary>
    public Section? HitTestSection(float canvasX, float canvasY, float canvasWidth)
    {
        foreach (var (section, ox, oy) in GetSectionOrigins(canvasWidth))
        {
            float sectionH = SectionHeightPx(section);
            float sectionW = _state.CurrentTemplate.PageSize.PrintableWidth * MmToPx;

            if (canvasX >= ox && canvasX <= ox + sectionW &&
                canvasY >= oy && canvasY <= oy + sectionH)
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
            float sectionH = SectionHeightPx(section);

            if (canvasX < ox || canvasX > ox + sectionW) continue;
            if (canvasY < oy || canvasY > oy + sectionH) continue;

            // Procházíme od nejvyššího ZIndex (klikne se na element "nahoře")
            foreach (var el in _state.GetElementsByZIndexDesc(section))
            {
                float ex = ox + el.X * MmToPx;
                float ey = oy + el.Y * MmToPx;
                float ew = el.Width * MmToPx;
                float eh = (el.Height ?? el.EffectiveDesignerHeight) * MmToPx;

                // Pro rotované elementy transformovat testovací bod inverzní rotací
                float testX = canvasX, testY = canvasY;
                if (el.IsRotated)
                {
                    float ecx = ex + ew / 2f, ecy = ey + eh / 2f;
                    float rad = -el.Rotation * MathF.PI / 180f;
                    float cos = MathF.Cos(rad), sin = MathF.Sin(rad);
                    float dx = canvasX - ecx, dy = canvasY - ecy;
                    testX = ecx + dx * cos - dy * sin;
                    testY = ecy + dx * sin + dy * cos;
                }

                if (testX >= ex && testX <= ex + ew &&
                    testY >= ey && testY <= ey + eh)
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

            foreach (var (hx, hy, handle) in GetHandlesForElement(el, r))
            {
                if (canvasX >= hx - hs && canvasX <= hx + hs &&
                    canvasY >= hy - hs && canvasY <= hy + hs)
                    return (el, handle);
            }
        }
        return null;
    }

    /// <summary>
    /// Vrátí resize handles pro element. Dynamické elementy (Height==null) nemají
    /// spodní ani horní vertikální handles — výšku počítá engine z dat.
    /// Zůstávají jen MiddleLeft a MiddleRight pro změnu šířky.
    /// </summary>
    private static IEnumerable<(float hx, float hy, ResizeHandle handle)> GetHandlesForElement(
        TemplateElement el, SKRect r)
    {
        if (el.IsRotated) yield break;

        float mx = r.MidX, my = r.MidY;

        if (el.IsDynamic)
        {
            // Dynamická výška — jen horizontální handles pro změnu šířky
            yield return (r.Left,  my, ResizeHandle.MiddleLeft);
            yield return (r.Right, my, ResizeHandle.MiddleRight);
        }
        else
        {
            // Fixní výška — všech 8 handles
            yield return (r.Left,  r.Top,    ResizeHandle.TopLeft);
            yield return (mx,      r.Top,    ResizeHandle.TopCenter);
            yield return (r.Right, r.Top,    ResizeHandle.TopRight);
            yield return (r.Left,  my,       ResizeHandle.MiddleLeft);
            yield return (r.Right, my,       ResizeHandle.MiddleRight);
            yield return (r.Left,  r.Bottom, ResizeHandle.BottomLeft);
            yield return (mx,      r.Bottom, ResizeHandle.BottomCenter);
            yield return (r.Right, r.Bottom, ResizeHandle.BottomRight);
        }
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

    /// <summary>Spočítá X souřadnici levého okraje stránky na canvasu.</summary>
    private float CalculatePageX(float canvasWidth)
    {
        float pageW = _state.CurrentTemplate.PageSize.Width * MmToPx;
        float minLeft = PagePaddingPx + ExtraLeftPx;
        return Math.Max(minLeft, (canvasWidth - pageW) / 2f);
    }

    /// <summary>Vrátí origin (levý horní roh obsahu) každé sekce na canvasu.</summary>
    public IEnumerable<(Section Section, float OriginX, float OriginY)> GetSectionOrigins(float canvasWidth)
    {
        var ps      = _state.CurrentTemplate.PageSize;
        float pageX = CalculatePageX(canvasWidth);
        float pageY = PagePaddingPx + TopBufferPx;

        float ox       = pageX + ps.MarginLeft * MmToPx;
        float currentY = pageY + ps.MarginTop  * MmToPx;

        var sections = _state.CurrentTemplate.Sections;

        // Souvislé plátno: hlavička+tělo se skládají shora dolů ve své PRAVÉ (neclampnuté) výšce a
        // plátno poroste (viz TotalPaperHeightPx). Žádné zahazování za přehybem — zlomy vyznačí overlay.
        foreach (var section in sections)
        {
            if (section.Role == SectionRole.Footer) continue;
            yield return (section, ox, currentY);
            currentY += SectionHeightPx(section);
        }

        // Patičky kotvené ke SPODKU rostoucího papíru — u jednostránkové šablony tak sedí s preview/tiskem
        // (WYSIWYG), u vysokého obsahu vyjdou hned za tělem. Per-stránkové opakování řeší preview/engine.
        float totalFooterH = 0f;
        foreach (var s in sections)
            if (s.Role == SectionRole.Footer) totalFooterH += SectionHeightPx(s);
        float footerY = pageY + TotalPaperHeightPx() - ps.MarginBottom * MmToPx - totalFooterH;
        foreach (var section in sections)
        {
            if (section.Role != SectionRole.Footer) continue;
            yield return (section, ox, footerY);
            footerY += SectionHeightPx(section);
        }
    }

    /// <summary>Výška sekce v px = její pravá (neclampnutá) designer-výška.</summary>
    public float SectionHeightPx(Section section) => section.EffectiveDesignerHeight * MmToPx;

    /// <summary>
    /// Celková výška „papíru" v px — roste s obsahem: horní okraj + součet výšek všech sekcí +
    /// spodní okraj. Minimum = jedna stránka (malá šablona ukáže plnou A4). Návrhář tím přestává
    /// být omezen na jednu stránku; plátno se přizpůsobí (viz UpdateCanvasSize).
    /// </summary>
    public float TotalPaperHeightPx()
    {
        var ps = _state.CurrentTemplate.PageSize;
        float sectionsMm = 0f;
        foreach (var s in _state.CurrentTemplate.Sections)
            sectionsMm += s.EffectiveDesignerHeight;
        float paperMm = ps.MarginTop + sectionsMm + ps.MarginBottom;
        return Math.Max(ps.Height, paperMm) * MmToPx;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Interní renderovací metody

    // Cache paint + maskfilter — `CreateBlur` je drahý, vytvářet jednou stačí.
    private static readonly SKPaint _shadowPaint = new()
    {
        Color = new SKColor(0x35000000),
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f)
    };

    /// <summary>
    /// Přerušované dělící čáry stránek přes souvislé plátno — orientační vodítko „kudy půjde zlom"
    /// (varianta 1: geometrické, po výšce tisknutelné oblasti). NENÍ pixel-přesné vůči preview
    /// (nezná opakování hlaviček/patiček ani KeepTogether) — přesný stránkovaný výsledek je v preview.
    /// Kreslí se jen když obsah přesahuje jednu stránku.
    /// </summary>
    private void DrawPageBreakLines(SKCanvas canvas, float pageX, float pageY, float pageW, float paperH, PageSize ps)
    {
        float printableHpx = (ps.Height - ps.MarginTop - ps.MarginBottom) * MmToPx;
        if (printableHpx <= 0.5f) return;

        float contentTop    = pageY + ps.MarginTop * MmToPx;
        float contentBottom = pageY + paperH - ps.MarginBottom * MmToPx;

        using var linePaint = new SKPaint
        {
            Color = ClrPageBreak, StrokeWidth = 1f, IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([8f, 4f], 0)
        };
        using var labelFont  = new SKFont(SKTypeface.Default, 11f);
        using var labelPaint = new SKPaint { Color = ClrPageBreak, IsAntialias = true };

        int page = 2;
        for (float y = contentTop + printableHpx; y < contentBottom - 0.5f; y += printableHpx, page++)
        {
            canvas.DrawLine(pageX, y, pageX + pageW, y, linePaint);
            string label = $"Strana {page}";
            float tw = labelFont.MeasureText(label);
            canvas.DrawText(label, pageX + pageW - tw - 4f, y - 3f, labelFont, labelPaint);
        }
    }

    private static void DrawPageShadow(SKCanvas canvas, float x, float y, float w, float h)
    {
        canvas.DrawRect(new SKRect(x + 5, y + 5, x + w + 5, y + h + 5), _shadowPaint);
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

    private void DrawGrid(SKCanvas canvas, float pageX, float pageY, float pageW, float paperH, PageSize ps)
    {
        // Grid jen v printable area (kde se pozicují elementy), ne v margins. Slabě modrá, aby nerušila obsah.
        float step   = _state.GridSize * MmToPx;
        if (step <= 0f) return;

        float startX = pageX + ps.MarginLeft  * MmToPx;
        float startY = pageY + ps.MarginTop   * MmToPx;
        float endX   = pageX + (ps.Width  - ps.MarginRight)  * MmToPx;
        float endY   = pageY + paperH - ps.MarginBottom * MmToPx;   // grid přes celý (rostoucí) papír

        // Viewport culling — vykresli jen linky uvnitř viditelné oblasti.
        // Při vysokém zoomu jsou tisíce linek mimo viewport (uživatel je nevidí),
        // jejich vynechání výrazně zrychlí drag bez vizuální změny.
        SKRect vp = VisibleViewport;
        bool cull = vp.Width > 0 && vp.Height > 0;
        float clipLeft   = cull ? Math.Max(startX, vp.Left)   : startX;
        float clipRight  = cull ? Math.Min(endX,   vp.Right)  : endX;
        float clipTop    = cull ? Math.Max(startY, vp.Top)    : startY;
        float clipBottom = cull ? Math.Min(endY,   vp.Bottom) : endY;

        // První viditelná linka (zaokrouhleno na grid krok pro správné zarovnání)
        float firstX = startX + MathF.Floor((clipLeft - startX) / step) * step;
        float firstY = startY + MathF.Floor((clipTop  - startY) / step) * step;
        if (firstX < startX) firstX = startX;
        if (firstY < startY) firstY = startY;

        using var p = new SKPaint
        {
            Color = ClrGrid,
            StrokeWidth = 0.5f,
            IsAntialias = true,    // AA potřebné pro pravidelný vzor — bez AA se 0.5px linky
                                   // zaokrouhlují na pixely a při non-integer kroku vznikají
                                   // nepravidelnosti. Při viewport culling je linek málo (~100),
                                   // takže AA má zanedbatelný cost.
        };
        for (float gx = firstX; gx <= clipRight + 0.1f; gx += step)
            canvas.DrawLine(gx, startY, gx, endY, p);
        for (float gy = firstY; gy <= clipBottom + 0.1f; gy += step)
            canvas.DrawLine(startX, gy, endX, gy, p);
    }

    private void DrawMarginGuides(SKCanvas canvas, float pageX, float pageY, PageSize ps, float paperH)
    {
        float ml = ps.MarginLeft * MmToPx, mr = ps.MarginRight  * MmToPx;
        float mt = ps.MarginTop  * MmToPx, mb = ps.MarginBottom * MmToPx;
        float pw = ps.Width * MmToPx, ph = paperH;   // spodní okraj sleduje rostoucí papír
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
            float sectionH = SectionHeightPx(section);
            bool  dynamic  = section.DesignerHeight is null;
            bool  isActive = section == _state.ActiveSection;

            // Obsah sekce
            var contentRect = new SKRect(ox, oy, ox + contentW, oy + sectionH);
            using (var bg = new SKPaint { Color = dynamic ? ClrSectionContentDyn : ClrSectionContent })
                canvas.DrawRect(contentRect, bg);

            canvas.Save();
            canvas.ClipRect(contentRect);
            var ctx = new RenderContext(canvas, MmToPx) { SectionOriginX = ox, SectionOriginY = oy };
            foreach (var el in _state.GetElementsByZIndexAsc(section))
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

            // Indikátory podmíněné viditelnosti a skupin — jeden průchod
            foreach (var el in section.Elements)
            {
                bool hasCondition = !string.IsNullOrWhiteSpace(el.VisibilityExpression);
                bool hasGroup = el.GroupId is not null;
                if (!hasCondition && !hasGroup) continue;

                float ex = ox + el.X * MmToPx;
                float ey = oy + el.Y * MmToPx;
                float ew = el.Width * MmToPx;
                float eh = (el.Height ?? el.EffectiveDesignerHeight) * MmToPx;
                var rect = new SKRect(ex, ey, ex + ew, ey + eh);

                if (hasCondition)
                {
                    bool invalid = Models.Expressions.ExpressionEvaluator.Validate(el.VisibilityExpression) is not null;
                    DrawConditionBadge(canvas, rect, invalid);
                }
                if (hasGroup) DrawGroupBadge(canvas, rect, el.GroupId!);
            }
            canvas.Restore();

            // Druhý průchod — rámečky elementů MIMO clip sekce, aby byly vidět i na hranici sekce.
            // Na sdílené hraně dvou elementů se strokes překryjí na stejném pixelu → jeden rámeček.
            foreach (var el in section.Elements.OrderBy(e => e.ZIndex))
                el.RenderBorderPass(ctx);

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

            // Levý štítek sekce
            if (_state.ShowSectionHeaders)
                DrawSectionTab(canvas, section, ox, oy, sectionH, dynamic, isActive, template.PageSize);
        }
    }

    private void DrawSectionTab(SKCanvas canvas, Section section, float contentOx, float contentOy,
        float sectionH, bool dynamic, bool isActive, PageSize ps)
    {
        // Tab je přilepený k levé hraně stránky (ne k levé hraně sekce — ta je posunutá o MarginLeft).
        float pageLeft = contentOx - ps.MarginLeft * MmToPx;
        float tabRight = pageLeft;
        float tabLeft  = tabRight - SectionTabWidthPx;

        var tabRect = new SKRect(tabLeft, contentOy, tabRight, contentOy + sectionH);
        using (var bg = new SKPaint { Color = isActive ? ClrSectionActive : (dynamic ? ClrSectionHeaderDyn : ClrSectionHeader) })
            canvas.DrawRect(tabRect, bg);

        // Tenký rámeček štítku — vizuální oddělení
        using (var tabBorder = new SKPaint { Color = ClrSectionBorder, IsStroke = true, StrokeWidth = 1f })
            canvas.DrawRect(tabRect, tabBorder);

        // Text jen pokud je dost místa vertikálně
        if (sectionH >= SectionTabMinLabelPx)
        {
            using var font    = new SKFont(SKTypeface.Default, 11f);
            using var txtPaint = new SKPaint { Color = ClrSectionHeaderText, IsAntialias = true };

            // Název sekce svisle — otočený o 90° po směru hod. ručiček. Délka textu jede
            // podél výšky sekce (ne šířky úzkého pruhu), takže se vejde i delší název.
            string name = section.Name ?? "";
            float maxTextH = sectionH - 16f;
            string drawn = TruncateToWidth(name, font, maxTextH);

            float cx     = (tabLeft + tabRight) / 2f;
            float startY = contentOy + 8f;
            canvas.Save();
            canvas.RotateDegrees(90f, cx, startY);                       // pivot u horního okraje pruhu
            canvas.DrawText(drawn, cx, startY + font.Size * 0.34f, font, txtPaint);  // baseline ~ vystředěná na šířku pruhu
            canvas.Restore();

            // Indikátor Role / dynamické výšky — dole, vodorovně vystředěný (1–2 znaky se vejdou)
            string badge = "";
            if (section.Role == SectionRole.Header) badge = "H";
            else if (section.Role == SectionRole.Footer) badge = "F";
            if (dynamic) badge = badge.Length > 0 ? badge + "∞" : "∞";

            if (badge.Length > 0)
            {
                using var badgeFont  = new SKFont(SKTypeface.Default, 9f);
                using var badgePaint = new SKPaint { Color = ClrSectionHeaderText.WithAlpha(200), IsAntialias = true };
                float badgeW = badgeFont.MeasureText(badge);
                canvas.DrawText(badge, cx - badgeW / 2f, contentOy + sectionH - 5f, badgeFont, badgePaint);
            }
        }
    }

    private static string TruncateToWidth(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth) return text;
        const string ellipsis = "…";
        for (int len = text.Length - 1; len > 0; len--)
        {
            string candidate = text[..len] + ellipsis;
            if (font.MeasureText(candidate) <= maxWidth) return candidate;
        }
        return ellipsis;
    }

    private void DrawSelectionOverlay(SKCanvas canvas, float canvasWidth)
    {
        if (_state.SelectedElements.Count == 0) return;

        // Bounding box skupiny — tečkovaný barevný rámeček kolem celé skupiny
        var groupIds = _state.SelectedElements
            .Where(e => e.GroupId is not null)
            .Select(e => e.GroupId!)
            .Distinct();
        foreach (var gid in groupIds)
        {
            var members = _state.SelectedElements.Where(e => e.GroupId == gid).ToList();
            if (members.Count < 2) continue;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var m in members)
            {
                var mr = GetElementCanvasRect(m, canvasWidth);
                if (mr is null) continue;
                minX = Math.Min(minX, mr.Value.Left);
                minY = Math.Min(minY, mr.Value.Top);
                maxX = Math.Max(maxX, mr.Value.Right);
                maxY = Math.Max(maxY, mr.Value.Bottom);
            }
            if (minX < maxX)
            {
                var groupColor = ElementGroupHelper.GetGroupColor(gid);
                using var gBorder = new SKPaint
                {
                    Color = groupColor.WithAlpha(120),
                    IsStroke = true,
                    StrokeWidth = 1.5f,
                    PathEffect = SKPathEffect.CreateDash([4f, 4f], 0)
                };
                canvas.DrawRect(new SKRect(minX - 2, minY - 2, maxX + 2, maxY + 2), gBorder);
            }
        }

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

            if (el.IsRotated)
            {
                canvas.Save();
                canvas.RotateDegrees(el.Rotation, rect.Value.MidX, rect.Value.MidY);
                canvas.DrawRect(rect.Value, selBorder);
                canvas.Restore();
            }
            else
            {
                canvas.DrawRect(rect.Value, selBorder);
            }
        }

        // Resize handles nebo lock badge — pouze při výběru jednoho elementu
        if (_state.SelectedElements.Count != 1) return;

        var singleEl   = _state.SelectedElements[0];
        var singleRect = GetElementCanvasRect(singleEl, canvasWidth);
        if (singleRect is null) return;
        var r = singleRect.Value;

        if (singleEl.IsLocked)
        {
            DrawLockBadge(canvas, r);
        }
        else if (singleEl.IsRotated)
        {
            DrawRotationBadge(canvas, r, singleEl.Rotation);
        }
        else
        {
            using var handleFill   = new SKPaint { Color = SKColors.White };
            using var handleBorder = new SKPaint { Color = ClrSelection, IsStroke = true, StrokeWidth = 1.5f };

            float hs = HandleSizePx / 2f;
            foreach (var (hx, hy, _) in GetHandlesForElement(singleEl, r))
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

    private static void DrawRotationBadge(SKCanvas canvas, SKRect elementRect, float rotation)
    {
        string label = $"{rotation:0.#}°";
        const float pad = 3f, h = 14f;
        using var font = new SKFont(SKTypeface.Default, 9f);
        float w = MathF.Max(28f, font.MeasureText(label) + pad * 2);
        float bx = elementRect.MidX - w / 2f;
        float by = elementRect.Top - h - 3f;

        using var bg = new SKPaint { Color = ClrSelection, IsAntialias = true };
        canvas.DrawRoundRect(bx, by, w, h, 2f, 2f, bg);

        using var tp = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(label, bx + pad, by + h - 3.5f, font, tp);
    }

    private void DrawSnapTargetHighlights(SKCanvas canvas, float canvasWidth)
    {
        if (_state.SnapTargetElements.Count == 0) return;

        using var paint = new SKPaint
        {
            Color = new SKColor(0x30000000), // jemně šedý poloprůhledný
            IsStroke = true,
            StrokeWidth = 1f,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([3f, 2f], 0)
        };

        foreach (var el in _state.SnapTargetElements)
        {
            var rect = GetElementCanvasRect(el, canvasWidth);
            if (rect is null) continue;
            canvas.DrawRect(rect.Value, paint);
        }
    }

    private void DrawSnapLines(SKCanvas canvas, float canvasWidth)
    {
        if (_state.ActiveGapIndicators.Count == 0) return;
        var section = _state.ActiveSection;
        if (section is null) return;

        var origin = GetSectionOrigins(canvasWidth)
            .FirstOrDefault(s => s.Section == section);
        if (origin.Section is null) return;

        float ox = origin.OriginX, oy = origin.OriginY;

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 1f,
            IsStroke = true,
            IsAntialias = true
        };

        foreach (var gap in _state.ActiveGapIndicators)
        {
            if (gap.IsHorizontalGap)
            {
                float x1 = ox + gap.GapStartMm * MmToPx;
                float x2 = ox + gap.GapEndMm * MmToPx;
                float cy = oy + gap.CrossMidMm * MmToPx;
                canvas.DrawLine(x1, cy, x2, cy, paint);
            }
            else
            {
                float y1 = oy + gap.GapStartMm * MmToPx;
                float y2 = oy + gap.GapEndMm * MmToPx;
                float cx = ox + gap.CrossMidMm * MmToPx;
                canvas.DrawLine(cx, y1, cx, y2, paint);
            }
        }
    }

    /// <summary>
    /// Dashed vertical guide line přes celou výšku aktivní sekce, když element snapem
    /// právě sedí na horizontálním středu sekce. Standardní design tool UX pattern.
    /// </summary>
    private void DrawSectionCenterGuide(SKCanvas canvas, float canvasWidth)
    {
        if (_state.ActiveSectionCenterX is not { } centerXMm) return;
        var section = _state.ActiveSection;
        if (section is null) return;

        var origin = GetSectionOrigins(canvasWidth)
            .FirstOrDefault(s => s.Section == section);
        if (origin.Section is null) return;

        float ox = origin.OriginX, oy = origin.OriginY;
        float sectionHpx = section.EffectiveDesignerHeight * MmToPx;

        using var paint = new SKPaint
        {
            Color = new SKColor(0xC000B0FF),   // cyan, polo-transparentní
            StrokeWidth = 1f,
            IsStroke = true,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([4f, 3f], 0)
        };

        float x = ox + centerXMm * MmToPx;
        canvas.DrawLine(x, oy, x, oy + sectionHpx, paint);
    }

    /// <summary>
    /// Kreslí malý barevný čtvereček v pravém dolním rohu elementu.
    /// Barva je deterministická z GroupId — stejná skupina = stejná barva.
    /// </summary>
    private static void DrawGroupBadge(SKCanvas canvas, SKRect elementRect, string groupId)
    {
        const float sz = 8f;
        float bx = elementRect.Right - sz - 2f;
        float by = elementRect.Bottom - sz - 2f;

        if (elementRect.Width < sz + 6 || elementRect.Height < sz + 6) return;

        var color = ElementGroupHelper.GetGroupColor(groupId);
        using var bg = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRoundRect(bx, by, sz, sz, 2f, 2f, bg);
    }

    /// <summary>
    /// Pravítka v mm — horizontální nad stránkou, vertikální vpravo od stránky.
    /// 0mm = okraj stránky. Vejdou se do existujícího PagePaddingPx prostoru, žádná layout změna.
    /// Zvýrazňuje rozsah vybraného elementu (single) nebo bounding box (multi).
    /// </summary>
    private void DrawRulers(SKCanvas canvas, float pageX, float pageY, float pageW, float pageH, float canvasWidth)
    {
        float topRulerY   = Math.Max(2f, pageY - RulerOffsetPx - RulerThicknessPx);
        float rightRulerX = pageX + pageW + RulerOffsetPx;

        // Pořadí: strip (background) → highlight → ticky/text.
        // Highlight musí být nad pruhem (jinak ho strip přepíše) a pod tickami.
        DrawRulerStrip(canvas, pageX,       topRulerY, pageW,            RulerThicknessPx);
        DrawRulerStrip(canvas, rightRulerX, pageY,     RulerThicknessPx, pageH);
        DrawRulerHighlights(canvas, pageX, pageY, pageW, pageH, topRulerY, rightRulerX, canvasWidth);
        DrawRulerTicks(canvas, horizontal: true,  pageX,       topRulerY, pageW,            RulerThicknessPx);
        DrawRulerTicks(canvas, horizontal: false, rightRulerX, pageY,     RulerThicknessPx, pageH);
    }

    private static void DrawRulerStrip(SKCanvas canvas, float x, float y, float w, float h)
    {
        var rect = new SKRect(x, y, x + w, y + h);
        using var bg = new SKPaint { Color = ClrRulerBg };
        canvas.DrawRect(rect, bg);
        using var border = new SKPaint { Color = ClrRulerBorder, IsStroke = true, StrokeWidth = 1f };
        canvas.DrawRect(rect, border);
    }

    private void DrawRulerTicks(SKCanvas canvas, bool horizontal, float x, float y, float w, float h)
    {
        float mmToPx = MmToPx;
        // Volba kroku číselných popisků — řídí čitelnost při různém zoomu
        int numberStep = mmToPx >= 2f ? 10 : (mmToPx >= 1f ? 20 : 50);
        bool show5mm = mmToPx >= 1.5f;
        bool show1mm = mmToPx >= 4f;

        float lengthMm = (horizontal ? w : h) / mmToPx;

        // Layout pravítka: ticky/text dělí lateral dimension (tloušťku), ne délku
        float lateral  = horizontal ? h : w;
        float tickZone = lateral * 0.45f;
        float textZone = lateral - tickZone;

        using var tickPaint = new SKPaint { Color = ClrRulerTick, StrokeWidth = 1f, IsAntialias = false };
        using var font      = new SKFont(SKTypeface.Default, 9f);
        using var textPaint = new SKPaint { Color = ClrRulerText, IsAntialias = true };
        var metrics = font.Metrics;
        float textHeight = -metrics.Ascent + metrics.Descent;

        for (int mm = 0; mm <= (int)lengthMm + 1; mm++)
        {
            bool isMajor = mm % 10 == 0;
            bool isMid   = mm % 5 == 0;
            if (!isMajor && !isMid && !show1mm) continue;
            if (!isMajor && isMid && !show5mm) continue;

            float tickLen = isMajor ? tickZone
                          : isMid   ? tickZone * 0.65f
                                    : tickZone * 0.40f;

            if (horizontal)
            {
                float px = x + mm * mmToPx;
                if (px > x + w + 0.5f) break;
                // Tick kreslíme od spodní (vnitřní, k stránce přilehlé) hrany směrem nahoru
                canvas.DrawLine(px, y + h - tickLen, px, y + h, tickPaint);

                if (isMajor && mm % numberStep == 0 && mm > 0)
                {
                    string label = mm.ToString();
                    float tw = font.MeasureText(label);
                    // Text v horní polovině pruhu, vertikálně vystředěný v textZone
                    float textY = y + (textZone - textHeight) / 2f - metrics.Ascent;
                    canvas.DrawText(label, px - tw / 2f, textY, font, textPaint);
                }
            }
            else
            {
                float py = y + mm * mmToPx;
                if (py > y + h + 0.5f) break;
                // Tick kreslíme od levé (vnitřní, k stránce přilehlé) hrany směrem doprava
                canvas.DrawLine(x, py, x + tickLen, py, tickPaint);

                if (isMajor && mm % numberStep == 0 && mm > 0)
                {
                    string label = mm.ToString();
                    float tw = font.MeasureText(label);
                    // Text rotovaný -90° — čte se zdola nahoru, šetří horizontální místo
                    float textCenterX = x + tickZone + textZone / 2f;
                    canvas.Save();
                    canvas.Translate(textCenterX, py);
                    canvas.RotateDegrees(-90f);
                    // dx = -tw/2 centruje text vertikálně kolem py (po rotaci local +X = screen -Y)
                    // dy posunuje baseline horizontálně tak, aby bbox textu byl centrován na textCenterX
                    canvas.DrawText(label, -tw / 2f, -(metrics.Ascent + metrics.Descent) / 2f, font, textPaint);
                    canvas.Restore();
                }
            }
        }
    }

    /// <summary>
    /// Modré zvýraznění úseků pravítek pro rozsah vybraných elementů.
    /// Single = rozsah jednoho elementu, multi = bounding box přes všechny vybrané.
    /// </summary>
    private void DrawRulerHighlights(SKCanvas canvas, float pageX, float pageY, float pageW, float pageH,
        float topRulerY, float rightRulerX, float canvasWidth)
    {
        if (_state.SelectedElements.Count == 0) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var el in _state.SelectedElements)
        {
            var r = GetElementCanvasRect(el, canvasWidth);
            if (r is null) continue;
            minX = Math.Min(minX, r.Value.Left);
            maxX = Math.Max(maxX, r.Value.Right);
            minY = Math.Min(minY, r.Value.Top);
            maxY = Math.Max(maxY, r.Value.Bottom);
        }
        if (minX >= maxX) return;

        using var hl = new SKPaint { Color = ClrRulerHighlight };

        // Horizontální pravítko — průmět X rozsahu
        float hx1 = Math.Max(pageX, minX);
        float hx2 = Math.Min(pageX + pageW, maxX);
        if (hx2 > hx1)
            canvas.DrawRect(new SKRect(hx1, topRulerY, hx2, topRulerY + RulerThicknessPx), hl);

        // Vertikální pravítko — průmět Y rozsahu
        float hy1 = Math.Max(pageY, minY);
        float hy2 = Math.Min(pageY + pageH, maxY);
        if (hy2 > hy1)
            canvas.DrawRect(new SKRect(rightRulerX, hy1, rightRulerX + RulerThicknessPx, hy2), hl);
    }

    /// <summary>
    /// Kreslí malý badge v levém horním rohu elementu s nastavenou podmínkou tisku.
    /// Fialový "?" = validní podmínka; červený "!" = nevalidní výraz (upozornění uživateli).
    /// V designeru se podmínka nevyhodnocuje — element je vždy viditelný.
    /// </summary>
    private static void DrawConditionBadge(SKCanvas canvas, SKRect elementRect, bool invalid)
    {
        const float sz = 14f;
        float bx = elementRect.Left + 2f;
        float by = elementRect.Top + 2f;

        if (elementRect.Width < sz + 6 || elementRect.Height < sz + 6) return;

        using var bg = new SKPaint { Color = invalid ? ClrConditionInvalid : ClrCondition, IsAntialias = true };
        canvas.DrawRoundRect(bx, by, sz, sz, 2f, 2f, bg);

        using var font = new SKFont(SKTypeface.Default, 9f);
        using var tp   = new SKPaint { Color = SKColors.White, IsAntialias = true };
        string glyph = invalid ? "!" : "?";
        float textW = font.MeasureText(glyph, tp);
        canvas.DrawText(glyph, bx + (sz - textW) / 2f, by + sz - 3.5f, font, tp);
    }
}
