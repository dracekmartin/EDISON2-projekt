using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Mřížka minut odjezdů — jeden nebo více typů provozu vedle sebe.
/// SkupinaDnuId = "" → zobrazí všechny skupiny dnů vedle sebe se sdíleným sloupcem hodin.
/// SkupinaDnuId = "SMO1" → zobrazí pouze jednu skupinu.
/// SkupinaDnuId = "SMO1,SMO2" → zobrazí vybrané skupiny.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// </summary>
public class DepartureGridElement : ScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Filtr skupin dnů
    // ═══════════════════════════════════════════

    /// <summary>
    /// ID skupin dnů k zobrazení. Prázdný = všechny, čárkou oddělené = výběr.
    /// </summary>
    public string SkupinaDnuId { get; set; } = "";

    // ═══════════════════════════════════════════
    // Display mód
    // ═══════════════════════════════════════════
    public ScheduleDisplayMode DisplayMode { get; set; } = ScheduleDisplayMode.MinutesOnly;

    // ═══════════════════════════════════════════
    // Záhlaví skupin dnů
    // ═══════════════════════════════════════════
    public bool ShowSkupinaDnuHeader { get; set; } = true;
    public float SkupinaDnuHeaderLineHeight { get; set; } = 4f;

    /// <summary>Zarovnání textu záhlaví skupin: Top, Center, Bottom.</summary>
    public VerticalTextAlignment HeaderVerticalAlignment { get; set; } = VerticalTextAlignment.Bottom;

    // ═══════════════════════════════════════════
    // Mřížka minut (MinutesOnly mód)
    // ═══════════════════════════════════════════
    public float HourColumnWidth { get; set; } = 8f;
    public float MinuteWidth { get; set; } = 5f;
    public float MinuteFontSize { get; set; } = 7f;

    /// <summary>Šířka oddělovače mezi skupinami dnů (mm).</summary>
    public float GroupSeparatorWidth { get; set; } = 0.5f;

    // ═══════════════════════════════════════════
    // Časová osa
    // ═══════════════════════════════════════════
    public TimeAxisMode TimeAxisMode { get; set; } = TimeAxisMode.Compact;
    public int TimeAxisRangeFrom { get; set; } = 4;
    public int TimeAxisRangeTo { get; set; } = 24;

    // ═══════════════════════════════════════════
    // Anotace
    // ═══════════════════════════════════════════
    public bool ShowAnotace { get; set; } = true;
    public float AnotaceFontSize { get; set; } = 5f;
    public bool ShowZkraceneOznaceniLinky { get; set; }

    // ═══════════════════════════════════════════
    // Barvy (data-driven)
    // ═══════════════════════════════════════════
    public List<ColorRule> ColorRules { get; set; } = [];

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 60f;

    // ═══════════════════════════════════════════════════════════════════════════
    // Layout computation
    // ═══════════════════════════════════════════════════════════════════════════

    private record GroupLayout(
        SkupinaDnuData Skupina,
        Dictionary<int, List<OdjezdData>> ByHour,
        int MaxMinutesPerHour,
        float GroupWidth,
        string[] HeaderLines,
        float MinWidthForText);

    private record GridLayout(
        List<GroupLayout> Groups,
        List<int> Hours,
        float FontSizeMm,
        float HeaderFontMm,
        float MinuteFontMm,
        float RowH,
        float HeaderH,
        float GroupHeaderH,
        int MaxHeaderLines,
        float TotalWidth);

    // Cache layoutu — aby se nepočítal dvakrát (CalculateHeight + Render) pro stejná data
    [System.Text.Json.Serialization.JsonIgnore]
    private GridLayout? _cachedLayout;
    [System.Text.Json.Serialization.JsonIgnore]
    private DataContext? _cachedLayoutDataRef;

    private GridLayout? ComputeLayout(DataContext? data, float mmToPx = 0f)
    {
        // Cache hit — stejná data, vrátit uložený layout (CalculateHeight + Render se volají po sobě)
        if (_cachedLayout is not null && ReferenceEquals(data, _cachedLayoutDataRef))
            return _cachedLayout;

        var skupinyDnu = BuildSkupinyDnu(data);
        var odjezdyBySkupina = BuildOdjezdyBySkupina(data);

        var targetSkupiny = ResolveSkupiny(skupinyDnu);
        if (targetSkupiny.Count == 0)
        {
            _cachedLayout = null;
            _cachedLayoutDataRef = data;
            return null;
        }

        // Font pro měření textu záhlaví
        float headerLineFontPx = SkupinaDnuHeaderLineHeight * 0.65f * (mmToPx > 0 ? mmToPx : 3.78f);
        using var measureFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerLineFontPx);
        float effectiveMmToPx = mmToPx > 0 ? mmToPx : 3.78f;

        var groupLayouts = new List<GroupLayout>();
        foreach (var sk in targetSkupiny)
        {
            if (!odjezdyBySkupina.TryGetValue(sk.Id, out var byHour))
                byHour = new Dictionary<int, List<OdjezdData>>();

            string fullName = sk.Nadpis;
            if (sk.Nadpis2.Length > 0) fullName += " " + sk.Nadpis2;
            var headerLines = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Minimální šířka = nejdelší slovo záhlaví + 2mm padding
            float maxWordW = 0;
            foreach (var word in headerLines)
            {
                float w = MeasureTextMm(measureFont, word, effectiveMmToPx);
                if (w > maxWordW) maxWordW = w;
            }
            float minTextWidth = maxWordW + 2f;

            groupLayouts.Add(new GroupLayout(sk, byHour, 0, 0, headerLines, minTextWidth));
        }

        if (groupLayouts.All(g => g.ByHour.Count == 0))
        {
            _cachedLayout = null;
            _cachedLayoutDataRef = data;
            return null;
        }

        // Parametry z DataContext přepíší properties elementu
        var effectiveTimeAxis = ParseTimeAxisMode(GetStringParam(data, "CASOVA_OSA", TimeAxisMode.ToString().ToLowerInvariant()));
        int effectiveRangeFrom = GetIntParam(data, "HODINY_OD", TimeAxisRangeFrom);
        int effectiveRangeTo = GetIntParam(data, "HODINY_DO", TimeAxisRangeTo);

        var allHourKeys = groupLayouts.SelectMany(g => g.ByHour.Keys).ToHashSet();
        var hours = GetVisibleHoursFromSet(allHourKeys, effectiveTimeAxis, effectiveRangeFrom, effectiveRangeTo);

        // Šířky skupin: max(minuty, min. pro text záhlaví)
        var finalGroups = new List<GroupLayout>();
        foreach (var g in groupLayouts)
        {
            int maxMin = GetMaxMinutesPerHour(g.ByHour, hours);
            if (maxMin < 1) maxMin = 1;
            float minuteBasedW = maxMin * MinuteWidth;
            float groupW = Math.Max(minuteBasedW, g.MinWidthForText);
            finalGroups.Add(g with { MaxMinutesPerHour = maxMin, GroupWidth = groupW });
        }

        float fontSizeMm = FontSize * PtToMm;
        float headerFontMm = HeaderFontSize * PtToMm;
        float minuteFontMm = MinuteFontSize * PtToMm;
        float rowH = ComputeRowH(FontSize);
        float headerH = ComputeHeaderH(HeaderFontSize);

        int maxHeaderLines = finalGroups.Max(g => g.HeaderLines.Length);
        float groupHeaderH = ShowSkupinaDnuHeader
            ? maxHeaderLines * SkupinaDnuHeaderLineHeight
            : 0f;

        float totalWidth = HourColumnWidth
            + finalGroups.Sum(g => g.GroupWidth)
            + Math.Max(0, finalGroups.Count - 1) * GroupSeparatorWidth;

        var result = new GridLayout(finalGroups, hours, fontSizeMm, headerFontMm, minuteFontMm,
            rowH, headerH, groupHeaderH, maxHeaderLines, totalWidth);

        _cachedLayout = result;
        _cachedLayoutDataRef = data;
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Render
    // ═══════════════════════════════════════════════════════════════════════════

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        if (DisplayMode == ScheduleDisplayMode.MinutesOnly)
            RenderMinutesOnly(ctx);
        else
            RenderFullTime(ctx);
    }

    private void RenderMinutesOnly(RenderContext ctx)
    {
        var layout = ComputeLayout(ctx.Data, ctx.MmToPx);
        if (layout is null)
        {
            RenderPlaceholder(ctx);
            return;
        }

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit vše proporcionálně
        float naturalH = layout.GroupHeaderH + layout.HeaderH + layout.Hours.Count * layout.RowH;
        float scale = ComputeFontScale(naturalH);

        float scaledRowH = layout.RowH * scale;
        float scaledHeaderH = layout.HeaderH * scale;
        float scaledGroupHeaderH = layout.GroupHeaderH * scale;
        float scaledGroupLineH = SkupinaDnuHeaderLineHeight * scale;

        // Clip na hranice elementu
        float clipH = scaledGroupHeaderH + scaledHeaderH + layout.Hours.Count * scaledRowH;
        var clipRect = ctx.ToCanvasRect(X, Y, Width, clipH);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);

        using var paints = CreateStandardPaints();

        float fontPx = layout.FontSizeMm * scale * ctx.MmToPx;
        float headerFontPx = layout.HeaderFontMm * scale * ctx.MmToPx;
        float minuteFontPx = layout.MinuteFontMm * scale * ctx.MmToPx;
        float groupNameFontPx = SkupinaDnuHeaderLineHeight * scale * 0.65f * ctx.MmToPx;

        using var font = new SKFont(SKTypeface.FromFamilyName(FontFamily), fontPx);
        using var headerFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerFontPx);
        using var minuteFont = new SKFont(SKTypeface.FromFamilyName(FontFamily), minuteFontPx);
        using var groupNameFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), groupNameFontPx);

        // Anotace — font a paint vytvořeny jednou, ne per-minuta
        float anotFontPx = AnotaceFontSize * PtToMm * ctx.MmToPx;
        using var anotFont = new SKFont(SKTypeface.FromFamilyName(FontFamily), anotFontPx);
        using var anotPaint = new SKPaint { Color = new SKColor(0xFFCC0000), IsAntialias = true };

        float usedWidth = Math.Min(layout.TotalWidth, Width);
        float currentY = Y;

        // ── Záhlaví skupin dnů ──────────────────────────────────────────────
        if (ShowSkupinaDnuHeader && scaledGroupHeaderH > 0)
        {
            var headerRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledGroupHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

            float gx = X + HourColumnWidth;
            foreach (var g in layout.Groups)
            {
                int lineCount = g.HeaderLines.Length;
                float blockH = lineCount * scaledGroupLineH;

                // Vertikální zarovnání bloku textu
                float startLineY = HeaderVerticalAlignment switch
                {
                    VerticalTextAlignment.Top => currentY,
                    VerticalTextAlignment.Center => currentY + (scaledGroupHeaderH - blockH) / 2f,
                    _ => currentY + scaledGroupHeaderH - blockH, // Bottom
                };

                for (int i = 0; i < lineCount; i++)
                {
                    float lineY = startLineY + (i + 0.8f) * scaledGroupLineH;

                    // Horizontálně centrovat ve sloupci skupiny
                    float textW = MeasureTextMm(groupNameFont, g.HeaderLines[i], ctx.MmToPx);
                    float textX = gx + (g.GroupWidth - textW) / 2f;

                    var pt = ctx.ToCanvas(textX, lineY);
                    ctx.Canvas.DrawText(g.HeaderLines[i], pt.X, pt.Y, groupNameFont, paints.HeaderText);
                }

                gx += g.GroupWidth + GroupSeparatorWidth;
            }

            currentY += scaledGroupHeaderH;
        }

        // ── Záhlaví sloupců (Hod) ───────────────────────────────────────────
        var colHeaderRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledHeaderH);
        ctx.Canvas.DrawRect(colHeaderRect, paints.HeaderBg);

        var hourHeaderPt = ctx.ToCanvas(X + 1f, currentY + scaledHeaderH * HeaderBaselineFactor);
        ctx.Canvas.DrawText("Hod", hourHeaderPt.X, hourHeaderPt.Y, headerFont, paints.HeaderText);

        currentY += scaledHeaderH;

        // ── Řádky hodin ──────────────────────────────────────────────────────
        float scaledMinuteW = MinuteWidth * scale;
        foreach (int hour in layout.Hours)
        {
            var hourPt = ctx.ToCanvas(X + 1f, currentY + scaledRowH * TextBaselineFactor);
            ctx.Canvas.DrawText(hour.ToString(), hourPt.X, hourPt.Y, font, paints.Text);

            float gx = X + HourColumnWidth;
            foreach (var g in layout.Groups)
            {
                if (g.ByHour.TryGetValue(hour, out var odjezdy))
                {
                    float minX = gx;
                    SKPaint? customMinutePaint = null;
                    try
                    {
                        foreach (var odj in odjezdy)
                        {
                            var minuteText = odj.Minuta.ToString("D2");

                            SKPaint minutePaint;
                            if (odj.ForeColor is not null)
                            {
                                customMinutePaint ??= new SKPaint { IsAntialias = true };
                                customMinutePaint.Color = SKColor.Parse(odj.ForeColor);
                                minutePaint = customMinutePaint;
                            }
                            else
                            {
                                minutePaint = paints.Text;
                            }

                            var minPt = ctx.ToCanvas(minX + 0.5f, currentY + scaledRowH * TextBaselineFactor);
                            ctx.Canvas.DrawText(minuteText, minPt.X, minPt.Y, minuteFont, minutePaint);

                            if (ShowAnotace && odj.Anotace.Count > 0)
                            {
                                var anotaceText = string.Join("", odj.Anotace.Select(a => a.Znak));
                                var anotPt = ctx.ToCanvas(minX + scaledMinuteW * 0.7f, currentY + scaledRowH * 0.3f);
                                ctx.Canvas.DrawText(anotaceText, anotPt.X, anotPt.Y, anotFont, anotPaint);
                            }

                            minX += scaledMinuteW;
                        }
                    }
                    finally
                    {
                        customMinutePaint?.Dispose();
                    }
                }

                gx += g.GroupWidth + GroupSeparatorWidth;
            }

            var rowLineStart = ctx.ToCanvas(X, currentY + scaledRowH);
            var rowLineEnd = ctx.ToCanvas(X + usedWidth, currentY + scaledRowH);
            ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
            currentY += scaledRowH;
        }

        // ── Svislé čáry ──────────────────────────────────────────────────────
        float vLineTopY = Y;
        var hourVLineTop = ctx.ToCanvas(X + HourColumnWidth, vLineTopY);
        var hourVLineBottom = ctx.ToCanvas(X + HourColumnWidth, currentY);
        ctx.Canvas.DrawLine(hourVLineTop, hourVLineBottom, paints.Grid);

        float gxSep = X + HourColumnWidth;
        for (int i = 0; i < layout.Groups.Count; i++)
        {
            gxSep += layout.Groups[i].GroupWidth;

            if (i < layout.Groups.Count - 1)
            {
                float sepX = gxSep + GroupSeparatorWidth / 2f;
                var sepTop = ctx.ToCanvas(sepX, vLineTopY);
                var sepBottom = ctx.ToCanvas(sepX, currentY);
                ctx.Canvas.DrawLine(sepTop, sepBottom, paints.Grid);
                gxSep += GroupSeparatorWidth;
            }
        }

        // ── Horizontální čára pod záhlavím skupin ────────────────────────────
        if (ShowSkupinaDnuHeader && scaledGroupHeaderH > 0)
        {
            float lineY = Y + scaledGroupHeaderH;
            var hLineStart = ctx.ToCanvas(X + HourColumnWidth, lineY);
            var hLineEnd = ctx.ToCanvas(X + usedWidth, lineY);
            ctx.Canvas.DrawLine(hLineStart, hLineEnd, paints.Grid);
        }

        RenderOuterBorder(ctx, X, Y, usedWidth, currentY - Y, paints.Grid);

        ctx.Canvas.Restore(); // Uvolnit clip rect
    }

    private void RenderFullTime(RenderContext ctx)
    {
        float fontSizeMm = FontSize * PtToMm;
        float fontPx = fontSizeMm * ctx.MmToPx;
        using var font = new SKFont(SKTypeface.FromFamilyName(FontFamily), fontPx);
        using var paint = new SKPaint { Color = new SKColor(0xFF999999), IsAntialias = true };

        var pt = ctx.ToCanvas(X + 2f, Y + 10f);
        ctx.Canvas.DrawText("FullTime mód (připravuje se)", pt.X, pt.Y, font, paint);
    }

    private void RenderPlaceholder(RenderContext ctx)
    {
        float rowH = ComputeRowH(FontSize);
        float headerH = ComputeHeaderH(HeaderFontSize);

        using var paints = CreateStandardPaints();

        float fontPx = FontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = HeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(SKTypeface.FromFamilyName(FontFamily), fontPx);
        using var headerFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerFontPx);

        float currentY = Y;

        if (ShowSkupinaDnuHeader)
        {
            float placeholderHeaderH = 2 * SkupinaDnuHeaderLineHeight;
            var headerRect = ctx.ToCanvasRect(X, currentY, Width, placeholderHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);
            var pt1 = ctx.ToCanvas(X + 1f, currentY + SkupinaDnuHeaderLineHeight * 0.8f);
            ctx.Canvas.DrawText("PRACOVNÍ", pt1.X, pt1.Y, headerFont, paints.HeaderText);
            var pt2 = ctx.ToCanvas(X + 1f, currentY + SkupinaDnuHeaderLineHeight * 1.8f);
            ctx.Canvas.DrawText("DNY", pt2.X, pt2.Y, headerFont, paints.HeaderText);
            currentY += placeholderHeaderH;
        }

        currentY = RenderSectionHeader(ctx, "Hod",
            X, currentY, Width, headerH, headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        paints.Text.Color = new SKColor(0xFF999999);
        for (int h = 5; h <= 8; h++)
        {
            var pt = ctx.ToCanvas(X + 1f, currentY + rowH * TextBaselineFactor);
            ctx.Canvas.DrawText(h.ToString(), pt.X, pt.Y, font, paints.Text);
            var mpt = ctx.ToCanvas(X + HourColumnWidth + 1f, currentY + rowH * TextBaselineFactor);
            ctx.Canvas.DrawText("00  15  30  45", mpt.X, mpt.Y, font, paints.Text);

            var lineS = ctx.ToCanvas(X, currentY + rowH);
            var lineE = ctx.ToCanvas(X + Width, currentY + rowH);
            ctx.Canvas.DrawLine(lineS, lineE, paints.Grid);
            currentY += rowH;
        }

        RenderOuterBorder(ctx, X, Y, Width, currentY - Y, paints.Grid);
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public float CalculateHeightFromData(DataContext? data)
    {
        var layout = ComputeLayout(data);
        if (layout is null) return DesignerPlaceholderHeight;

        return layout.GroupHeaderH + layout.HeaderH + layout.Hours.Count * layout.RowH;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<SkupinaDnuData> ResolveSkupiny(List<SkupinaDnuData> all)
    {
        if (all.Count == 0) return [];

        if (string.IsNullOrWhiteSpace(SkupinaDnuId))
            return all;

        var ids = SkupinaDnuId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<SkupinaDnuData>();
        foreach (var id in ids)
        {
            var match = all.Find(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (match is not null) result.Add(match);
        }

        return result.Count > 0 ? result : [all[0]];
    }

    private static List<int> GetVisibleHoursFromSet(
        HashSet<int> allHours, TimeAxisMode timeAxis, int rangeFrom, int rangeTo)
    {
        return timeAxis switch
        {
            TimeAxisMode.Compact => allHours.OrderBy(h => h).ToList(),
            TimeAxisMode.Full => Enumerable.Range(0, 24).ToList(),
            TimeAxisMode.Range => Enumerable.Range(rangeFrom,
                Math.Max(1, rangeTo - rangeFrom + 1)).ToList(),
            _ => allHours.OrderBy(h => h).ToList()
        };
    }

    private static TimeAxisMode ParseTimeAxisMode(string s) => s.ToLowerInvariant() switch
    {
        "compact" => TimeAxisMode.Compact,
        "full" => TimeAxisMode.Full,
        "range" => TimeAxisMode.Range,
        _ => TimeAxisMode.Compact
    };

    private static int GetMaxMinutesPerHour(Dictionary<int, List<OdjezdData>> byHour, List<int> hours)
    {
        int max = 0;
        foreach (var h in hours)
        {
            if (byHour.TryGetValue(h, out var list) && list.Count > max)
                max = list.Count;
        }
        return max;
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Skupiny dnů", Group = "Odjezdy", Get = () => SkupinaDnuId, Set = v => SkupinaDnuId = v },
        new EnumPropertyDescriptor  { Label = "Režim zobrazení", Group = "Odjezdy",
            Get = () => DisplayMode, Set = v => DisplayMode = (ScheduleDisplayMode)v,
            Options = [("Minuty", (object)ScheduleDisplayMode.MinutesOnly), ("Plný čas", (object)ScheduleDisplayMode.FullTime)] },

        new BoolPropertyDescriptor  { Label = "Záhlaví skupin",     Group = "Odjezdy", Get = () => ShowSkupinaDnuHeader,       Set = v => ShowSkupinaDnuHeader = v },
        new FloatPropertyDescriptor { Label = "Výška řádku záhl.",  Group = "Odjezdy", Get = () => SkupinaDnuHeaderLineHeight,  Set = v => SkupinaDnuHeaderLineHeight = v, Min = 2, Max = 10, Unit = "mm" },
        new EnumPropertyDescriptor  { Label = "Zarovnání záhlaví", Group = "Odjezdy",
            Get = () => HeaderVerticalAlignment, Set = v => HeaderVerticalAlignment = (VerticalTextAlignment)v,
            Options = [("Nahoru", (object)VerticalTextAlignment.Top), ("Na střed", (object)VerticalTextAlignment.Center), ("Dolů", (object)VerticalTextAlignment.Bottom)] },
        new FloatPropertyDescriptor { Label = "Šířka sl. hodin",    Group = "Odjezdy", Get = () => HourColumnWidth,            Set = v => HourColumnWidth = v, Min = 4, Max = 20, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka minuty",       Group = "Odjezdy", Get = () => MinuteWidth,                Set = v => MinuteWidth = v, Min = 3, Max = 15, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Velikost minut",     Group = "Odjezdy", Get = () => MinuteFontSize,             Set = v => MinuteFontSize = v, Min = 4, Max = 20, Unit = "pt" },
        new FloatPropertyDescriptor { Label = "Oddělovač skupin",   Group = "Odjezdy", Get = () => GroupSeparatorWidth,        Set = v => GroupSeparatorWidth = v, Min = 0, Max = 5, Unit = "mm" },

        new EnumPropertyDescriptor  { Label = "Časová osa", Group = "Časová osa",
            Get = () => TimeAxisMode, Set = v => TimeAxisMode = (TimeAxisMode)v,
            Options = [("Kompaktní", (object)TimeAxisMode.Compact), ("Plná (0–23)", (object)TimeAxisMode.Full), ("Rozsah", (object)TimeAxisMode.Range)] },
        new FloatPropertyDescriptor { Label = "Od hodiny", Group = "Časová osa", Get = () => TimeAxisRangeFrom, Set = v => TimeAxisRangeFrom = (int)v, Min = 0, Max = 23, Format = "0" },
        new FloatPropertyDescriptor { Label = "Do hodiny", Group = "Časová osa", Get = () => TimeAxisRangeTo,   Set = v => TimeAxisRangeTo = (int)v,   Min = 0, Max = 23, Format = "0" },

        new BoolPropertyDescriptor  { Label = "Anotace",             Group = "Anotace", Get = () => ShowAnotace,                Set = v => ShowAnotace = v },
        new FloatPropertyDescriptor { Label = "Velikost anotací",    Group = "Anotace", Get = () => AnotaceFontSize,            Set = v => AnotaceFontSize = v, Min = 3, Max = 12, Unit = "pt" },
        new BoolPropertyDescriptor  { Label = "Zkr. označ. linky",  Group = "Anotace", Get = () => ShowZkraceneOznaceniLinky,   Set = v => ShowZkraceneOznaceniLinky = v },

        .. GetScheduleBaseDescriptors(),
        .. GetPositionDescriptors()
    ];

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new DepartureGridElement
        {
            Id = Guid.NewGuid(),
            X = X, Y = Y, Width = Width, Height = Height,
            ZIndex = ZIndex, IsLocked = IsLocked,
            VisibilityExpression = VisibilityExpression,
            CustomProperties = CustomProperties is null ? null : new(CustomProperties),
            GroupId = GroupId,
            BorderColor = BorderColor, BorderWidth = BorderWidth,

            SkupinaDnuId               = SkupinaDnuId,
            DisplayMode                = DisplayMode,
            ShowSkupinaDnuHeader       = ShowSkupinaDnuHeader,
            SkupinaDnuHeaderLineHeight = SkupinaDnuHeaderLineHeight,
            HeaderVerticalAlignment    = HeaderVerticalAlignment,
            HourColumnWidth            = HourColumnWidth,
            MinuteWidth                = MinuteWidth,
            MinuteFontSize             = MinuteFontSize,
            GroupSeparatorWidth        = GroupSeparatorWidth,
            TimeAxisMode               = TimeAxisMode,
            TimeAxisRangeFrom          = TimeAxisRangeFrom,
            TimeAxisRangeTo            = TimeAxisRangeTo,
            ShowAnotace                = ShowAnotace,
            AnotaceFontSize            = AnotaceFontSize,
            ShowZkraceneOznaceniLinky   = ShowZkraceneOznaceniLinky,
            ColorRules                 = ColorRules.Select(r => r.Clone()).ToList(),
        };
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
