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

    /// <summary>Styl textu minuty (font, bold, barva). Null properties = element defaults.</summary>
    public InlineTextStyle MinuteStyle { get; set; } = new();

    /// <summary>Styl anotací typu speciální/NZ (symboly ×, F, H). Null properties = 80% velikosti minuty.</summary>
    public InlineTextStyle AnotaceSymbolStyle { get; set; } = new();

    /// <summary>Styl anotací typu text (písmena a, A, dny). Null properties = AnotaceFontSize.</summary>
    public InlineTextStyle AnotaceTextStyle { get; set; } = new();

    /// <summary>Mezera mezi minutou a první anotací (mm). 0 = bez mezery.</summary>
    public float AnotaceGap { get; set; } = 0f;

    /// <summary>Mezera mezi anotacemi navzájem (mm). 0 = bez mezery.</summary>
    public float AnotaceInterGap { get; set; } = 0f;

    /// <summary>Mezera mezi odjezdy v rámci jedné buňky (mm). 0 = jen MinuteWidth slot.</summary>
    public float OdjezdGap { get; set; } = 0f;

    /// <summary>Šířka oddělovače mezi skupinami dnů (mm).</summary>
    public float GroupSeparatorWidth { get; set; } = 0.5f;

    // ═══════════════════════════════════════════
    // Časová osa
    // ═══════════════════════════════════════════
    public TimeAxisMode TimeAxisMode { get; set; } = TimeAxisMode.Compact;
    public int TimeAxisRangeFrom { get; set; } = 4;
    public int TimeAxisRangeTo { get; set; } = 24;

    // ═══════════════════════════════════════════
    // Řádky dat
    // ═══════════════════════════════════════════

    /// <summary>Výška datového řádku v mm. Null = vypočítat z FontSize.</summary>
    public float? DataRowHeight { get; set; }

    /// <summary>Minuty tučně.</summary>
    public bool MinuteBold { get; set; } = true;

    /// <summary>Barva pozadí sudých řádků (zebra pattern). Null = bez podbarvení.</summary>
    public string? AlternatingRowColor { get; set; } = "#FFF5F5F5";

    // ═══════════════════════════════════════════
    // Anotace
    // ═══════════════════════════════════════════
    public bool ShowAnotace { get; set; } = true;
    public float AnotaceFontSize { get; set; } = 5f;
    public bool ShowZkraceneOznaceniLinky { get; set; }

    // ═══════════════════════════════════════════
    // Nastavení sloupců
    // ═══════════════════════════════════════════

    /// <summary>Vizuální styl sloupce hodin (záhlaví "Hod" + čísla hodin). Null = element defaults.</summary>
    public ColumnVisualStyle? HourColumnStyle { get; set; }

    public List<DepartureGroupColumnStyle> GroupColumns { get; set; } = [];

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
        int MaxHeaderLines);

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
            var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Minimální šířka = nejdelší slovo záhlaví + 2mm padding
            float maxWordW = 0;
            foreach (var word in words)
            {
                float w = MeasureTextMm(measureFont, word, effectiveMmToPx);
                if (w > maxWordW) maxWordW = w;
            }
            float minTextWidth = maxWordW + 2f;

            // HeaderLines se přepočítají po redistribuci šířek (zatím placeholder)
            groupLayouts.Add(new GroupLayout(sk, byHour, 0, 0, words, minTextWidth));
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
            float effectiveSlotW = MinuteWidth + AnotaceGap;
            float minuteBasedW = maxMin * effectiveSlotW + Math.Max(0, maxMin - 1) * OdjezdGap;
            float groupW = Math.Max(minuteBasedW, g.MinWidthForText);
            finalGroups.Add(g with { MaxMinutesPerHour = maxMin, GroupWidth = groupW });
        }

        // Redistribuce extra prostoru — sloupce skupin zaplní celou šířku elementu
        float separatorTotal = Math.Max(0, finalGroups.Count - 1) * GroupSeparatorWidth;
        float minTotal = HourColumnWidth + finalGroups.Sum(g => g.GroupWidth) + separatorTotal;

        if (minTotal < Width)
        {
            float extraSpace = Width - minTotal;
            float sumGroupW = finalGroups.Sum(g => g.GroupWidth);
            if (sumGroupW > 0)
            {
                finalGroups = finalGroups.Select(g =>
                    g with { GroupWidth = g.GroupWidth + extraSpace * (g.GroupWidth / sumGroupW) }
                ).ToList();
            }
        }

        // Word-wrap záhlaví skupin podle skutečné šířky sloupce
        finalGroups = finalGroups.Select(g =>
        {
            string fullText = g.Skupina.Nadpis;
            if (g.Skupina.Nadpis2.Length > 0) fullText += " " + g.Skupina.Nadpis2;
            var wrapped = WrapHeaderText(fullText, g.GroupWidth, measureFont, effectiveMmToPx);
            return g with { HeaderLines = wrapped };
        }).ToList();

        float fontSizeMm = FontSize * PtToMm;
        float headerFontMm = HeaderFontSize * PtToMm;
        float minuteFontMm = MinuteFontSize * PtToMm;
        float rowH = DataRowHeight ?? ComputeRowH(FontSize);
        float headerH = ComputeHeaderH(HeaderFontSize);

        int maxHeaderLines = finalGroups.Max(g => g.HeaderLines.Length);
        float groupHeaderH = ShowSkupinaDnuHeader
            ? maxHeaderLines * SkupinaDnuHeaderLineHeight
            : 0f;

        var result = new GridLayout(finalGroups, hours, fontSizeMm, headerFontMm, minuteFontMm,
            rowH, headerH, groupHeaderH, maxHeaderLines);

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

        RenderMinutesOnly(ctx);
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
        // Záhlaví = jeden sloučený řádek (Hod + názvy skupin)
        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        // Pro scale použít font-driven výšku řádku, ne uživatelskou DataRowHeight —
        // jinak zvětšení řádku paradoxně zmenšuje fonty
        float fontRowH = ComputeRowH(FontSize);
        float scaleNaturalH = combinedHeaderH + layout.Hours.Count * fontRowH;
        float scale = ComputeFontScale(scaleNaturalH);
        float naturalH = combinedHeaderH + layout.Hours.Count * layout.RowH;

        // Pokud je DataRowHeight explicitně nastavená, nescalovat ji — uživatel ji řídí přímo.
        // Scale se aplikuje jen na fonty a záhlaví.
        float scaledRowH = DataRowHeight.HasValue ? layout.RowH : layout.RowH * scale;
        float scaledHeaderH = layout.HeaderH * scale;
        float scaledGroupHeaderH = layout.GroupHeaderH * scale;
        float scaledGroupLineH = SkupinaDnuHeaderLineHeight * scale;
        float scaledCombinedHeaderH = combinedHeaderH * scale;

        // Clip na hranice elementu
        float clipH = scaledCombinedHeaderH + layout.Hours.Count * scaledRowH;
        var clipRect = ctx.ToCanvasRect(X, Y, Width, clipH);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);

        using var paints = CreateStandardPaints();

        // ── Style lookup — jednou na frame, ne per-group ────────────────────
        var colStyles = new Dictionary<string, DepartureGroupColumnStyle>(StringComparer.OrdinalIgnoreCase);
        foreach (var gc in GroupColumns)
            colStyles[gc.SkupinaDnuId] = gc;

        float fontPx = layout.FontSizeMm * scale * ctx.MmToPx;
        float headerFontPx = layout.HeaderFontMm * scale * ctx.MmToPx;
        float minuteFontPx = layout.MinuteFontMm * scale * ctx.MmToPx;
        float groupNameFontPx = SkupinaDnuHeaderLineHeight * scale * 0.65f * ctx.MmToPx;

        using var font = new SKFont(SKTypeface.FromFamilyName(FontFamily), fontPx);
        using var headerFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerFontPx);

        // Minuty — InlineTextStyle override nebo element defaults
        var ms = MinuteStyle;
        float minuteEffFontPx = (ms.FontSize ?? MinuteFontSize) * PtToMm * scale * ctx.MmToPx;
        bool minuteEffBold = ms.Bold ?? MinuteBold;
        bool minuteEffItalic = ms.Italic ?? false;
        using var minuteFont = new SKFont(
            SKTypeface.FromFamilyName(ms.FontFamily ?? FontFamily,
                MakeFontStyle(minuteEffBold, minuteEffItalic)),
            minuteEffFontPx);
        // Manuálně spravované resources — dispose v finally bloku na konci metody
        var manualDisposables = new List<IDisposable>();

        SKPaint? minuteStylePaint = null;
        if (ms.TextColor is not null)
        {
            minuteStylePaint = new SKPaint { Color = SKColor.Parse(ms.TextColor), IsAntialias = true };
            manualDisposables.Add(minuteStylePaint);
        }

        using var groupNameFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), groupNameFontPx);

        // Anotace symboly (speciální/NZ) — InlineTextStyle override nebo 80% minut
        var ass = AnotaceSymbolStyle;
        float anotSymFontPx = ass.FontSize is not null
            ? ass.FontSize.Value * PtToMm * scale * ctx.MmToPx
            : minuteEffFontPx * 0.8f;
        using var anotPrimaryFont = new SKFont(
            SKTypeface.FromFamilyName(ass.FontFamily ?? FontFamily,
                MakeFontStyle(ass.Bold ?? false, ass.Italic ?? false)),
            anotSymFontPx);
        SKPaint? anotSymPaint = null;
        if (ass.TextColor is not null)
        {
            anotSymPaint = new SKPaint { Color = SKColor.Parse(ass.TextColor), IsAntialias = true };
            manualDisposables.Add(anotSymPaint);
        }

        // Anotace text — InlineTextStyle override nebo AnotaceFontSize
        var ats = AnotaceTextStyle;
        float anotTextFontPx = (ats.FontSize ?? AnotaceFontSize) * PtToMm * scale * ctx.MmToPx;
        using var anotSecondaryFont = new SKFont(
            SKTypeface.FromFamilyName(ats.FontFamily ?? FontFamily,
                MakeFontStyle(ats.Bold ?? false, ats.Italic ?? false)),
            anotTextFontPx);
        SKPaint? anotTextPaint = null;
        if (ats.TextColor is not null)
        {
            anotTextPaint = new SKPaint { Color = SKColor.Parse(ats.TextColor), IsAntialias = true };
            manualDisposables.Add(anotTextPaint);
        }

        // Default anotace barva (fallback pokud žádný InlineTextStyle nemá TextColor)
        using var anotDefaultPaint = new SKPaint { Color = new SKColor(0xFF222222), IsAntialias = true };

        float anotGapPx = AnotaceGap * scale * ctx.MmToPx;
        float anotInterGapPx = AnotaceInterGap * scale * ctx.MmToPx;
        float scaledOdjezdGap = OdjezdGap * scale; // v mm (model space), ne v px

        // Zebra pattern
        SKPaint? zebraPaint = null;
        if (AlternatingRowColor is not null)
        {
            zebraPaint = new SKPaint { Color = SKColor.Parse(AlternatingRowColor) };
            manualDisposables.Add(zebraPaint);
        }

        float usedWidth = Width;
        float currentY = Y;

        // ── Hour column style resources ─────────────────────────────────────
        var hourHs = HourColumnStyle?.Header;
        var hourDs = HourColumnStyle?.Data;
        var hourHeaderFontCustom = CreateCellFont(hourHs, HeaderFontSize, true, scale, ctx.MmToPx);
        var hourHeaderTextPaint = CreateCellTextPaint(hourHs);
        var hourHeaderBgPaint = CreateCellBgPaint(hourHs);
        var hourDataFontCustom = CreateCellFont(hourDs, FontSize, false, scale, ctx.MmToPx);
        var hourDataTextPaint = CreateCellTextPaint(hourDs);
        var hourDataBgPaint = CreateCellBgPaint(hourDs);
        if (hourHeaderFontCustom is not null) manualDisposables.Add(hourHeaderFontCustom);
        if (hourHeaderTextPaint is not null) manualDisposables.Add(hourHeaderTextPaint);
        if (hourHeaderBgPaint is not null) manualDisposables.Add(hourHeaderBgPaint);
        if (hourDataFontCustom is not null) manualDisposables.Add(hourDataFontCustom);
        if (hourDataTextPaint is not null) manualDisposables.Add(hourDataTextPaint);
        if (hourDataBgPaint is not null) manualDisposables.Add(hourDataBgPaint);

        // Per-column resources — deklarace před try, aby finally mohl uvolnit
        var perGroupDataFonts = new Dictionary<string, SKFont>();
        var perGroupColBgPaints = new Dictionary<string, SKPaint>();
        var perGroupDataTextPaints = new Dictionary<string, SKPaint>();

        try
        {
        // ── Sloučené záhlaví (Hod + názvy skupin dnů) ────────────────────────
        {
            var headerRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledCombinedHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

            // Pozadí sloupce hodin v záhlaví
            RenderCellBackground(ctx, hourHeaderBgPaint, X, currentY, HourColumnWidth, scaledCombinedHeaderH);

            // "Hod" v levém sloupci — s per-column zarovnáním a paddingem
            var hourHdrFont = hourHeaderFontCustom ?? headerFont;
            var hourHdrHAlign = hourHs?.HAlignment ?? TextHorizontalAlignment.Left;
            float hourHPadLeft = (hourHs?.PaddingLeft ?? 0f) * scale;
            float hourHPadRight = (hourHs?.PaddingRight ?? 0f) * scale;
            float hourHPadBottom = (hourHs?.PaddingBottom ?? 0f) * scale;
            float hodTextW = MeasureTextMm(hourHdrFont, "Hod", ctx.MmToPx);
            float hodTextX = hourHdrHAlign switch
            {
                TextHorizontalAlignment.Center => X + hourHPadLeft + (HourColumnWidth - hourHPadLeft - hourHPadRight - hodTextW) / 2f,
                TextHorizontalAlignment.Right  => X + HourColumnWidth - hodTextW - hourHPadRight - 1f,
                _ => X + hourHPadLeft + 1f
            };
            float hodTextY = hourHs?.PaddingTop is float hourHPadTop
                ? currentY + hourHPadTop * scale + scaledHeaderH * HeaderBaselineFactor
                : currentY + scaledCombinedHeaderH - scaledHeaderH * (1f - HeaderBaselineFactor) - hourHPadBottom;
            var hourHeaderPt = ctx.ToCanvas(hodTextX, hodTextY);
            ctx.Canvas.DrawText("Hod", hourHeaderPt.X, hourHeaderPt.Y,
                hourHdrFont,
                hourHeaderTextPaint ?? paints.HeaderText);

            // Názvy skupin dnů
            if (ShowSkupinaDnuHeader && scaledGroupHeaderH > 0)
            {
                float gx = X + HourColumnWidth;
                foreach (var g in layout.Groups)
                {
                    colStyles.TryGetValue(g.Skupina.Id, out var colStyle);

                    // Per-column záhlaví pozadí
                    var hs = colStyle?.Style.Header;
                    using var colHeaderBg = CreateCellBgPaint(hs);
                    RenderCellBackground(ctx, colHeaderBg, gx, currentY, g.GroupWidth, scaledCombinedHeaderH);

                    // Per-column font záhlaví
                    float colHeaderFontMm = hs?.FontSize is float fs
                        ? fs * PtToMm : SkupinaDnuHeaderLineHeight * 0.65f;
                    float colLineH = hs?.FontSize is not null
                        ? colHeaderFontMm * 1.5f * scale : scaledGroupLineH;
                    bool colBold = hs?.Bold ?? true;
                    bool colItalic = hs?.Italic ?? false;
                    float colHeaderFontPx = colHeaderFontMm * scale * ctx.MmToPx;
                    using var colFont = new SKFont(
                        SKTypeface.FromFamilyName(hs?.FontFamily ?? FontFamily, MakeFontStyle(colBold, colItalic)),
                        colHeaderFontPx);

                    int lineCount = g.HeaderLines.Length;
                    float blockH = lineCount * colLineH;

                    // Vertikální pozice — padding nebo element default
                    float hPadTop = (hs?.PaddingTop ?? 0f) * scale;
                    float hPadBottom = (hs?.PaddingBottom ?? 0f) * scale;
                    float hPadLeft = (hs?.PaddingLeft ?? 0f) * scale;
                    float hPadRight = (hs?.PaddingRight ?? 0f) * scale;
                    float startLineY;
                    if (hs?.PaddingTop is not null)
                        startLineY = currentY + hPadTop;
                    else
                    {
                        // Fallback: element-level vertikální zarovnání
                        startLineY = HeaderVerticalAlignment switch
                        {
                            VerticalTextAlignment.Top => currentY,
                            VerticalTextAlignment.Center => currentY + (scaledCombinedHeaderH - blockH) / 2f,
                            _ => currentY + scaledCombinedHeaderH - blockH, // Bottom
                        };
                    }

                    // Per-column text barva
                    SKPaint textPaint = paints.HeaderText;
                    SKPaint? customTextPaint = null;
                    if (hs?.TextColor is not null)
                    {
                        customTextPaint = new SKPaint { Color = SKColor.Parse(hs.TextColor), IsAntialias = true };
                        textPaint = customTextPaint;
                    }

                    // Per-column horizontální zarovnání (s padding)
                    var hAlign = hs?.HAlignment ?? TextHorizontalAlignment.Center;

                    for (int i = 0; i < lineCount; i++)
                    {
                        float lineY = startLineY + (i + 0.8f) * colLineH;

                        float textW = MeasureTextMm(colFont, g.HeaderLines[i], ctx.MmToPx);
                        float textX = hAlign switch
                        {
                            TextHorizontalAlignment.Left  => gx + hPadLeft + 1f,
                            TextHorizontalAlignment.Right => gx + g.GroupWidth - textW - hPadRight - 1f,
                            _ => gx + hPadLeft + (g.GroupWidth - hPadLeft - hPadRight - textW) / 2f
                        };

                        var pt = ctx.ToCanvas(textX, lineY);
                        ctx.Canvas.DrawText(g.HeaderLines[i], pt.X, pt.Y, colFont, textPaint);
                    }

                    customTextPaint?.Dispose();
                    gx += g.GroupWidth + GroupSeparatorWidth;
                }
            }

            currentY += scaledCombinedHeaderH;
        }

        // ── Per-column resources (vytvoříme jednou, ne per řádek) ─────────────
        var perGroupDataHAlign = new Dictionary<string, TextHorizontalAlignment>();
        // TryGetValue vrací default (0,0,0,0) pokud klíč neexistuje — žádný padding
        var perGroupDataPadding = new Dictionary<string, (float Top, float Right, float Bottom, float Left)>();
        foreach (var g in layout.Groups)
        {
            colStyles.TryGetValue(g.Skupina.Id, out var cs);
            var ds = cs?.Style.Data;
            if (CreateCellFont(ds, MinuteFontSize, MinuteBold, scale, ctx.MmToPx) is { } gdf)
            { perGroupDataFonts[g.Skupina.Id] = gdf; manualDisposables.Add(gdf); }
            if (CreateCellBgPaint(ds) is { } gbp)
            { perGroupColBgPaints[g.Skupina.Id] = gbp; manualDisposables.Add(gbp); }
            if (CreateCellTextPaint(ds) is { } gtp)
            { perGroupDataTextPaints[g.Skupina.Id] = gtp; manualDisposables.Add(gtp); }
            if (ds?.HAlignment is not null)
                perGroupDataHAlign[g.Skupina.Id] = ds.HAlignment.Value;
            if (ds?.PaddingTop is not null || ds?.PaddingRight is not null || ds?.PaddingBottom is not null || ds?.PaddingLeft is not null)
                perGroupDataPadding[g.Skupina.Id] = (
                    (ds?.PaddingTop ?? 0f) * scale, (ds?.PaddingRight ?? 0f) * scale,
                    (ds?.PaddingBottom ?? 0f) * scale, (ds?.PaddingLeft ?? 0f) * scale);
        }

        // ── Řádky hodin ──────────────────────────────────────────────────────
        float scaledMinuteW = (MinuteWidth + AnotaceGap) * scale;

        // Hoisted: hour column data style — konstantní přes všechny řádky
        var hourDFont = hourDataFontCustom ?? font;
        var hourDataHAlign = hourDs?.HAlignment ?? TextHorizontalAlignment.Left;
        float hourDPadLeft = (hourDs?.PaddingLeft ?? 0f) * scale;
        float hourDPadRight = (hourDs?.PaddingRight ?? 0f) * scale;
        float hourDPadTop = (hourDs?.PaddingTop ?? 0f) * scale;
        var hourDPaint = hourDataTextPaint ?? paints.Text;

        int rowIndex = 0;
        foreach (int hour in layout.Hours)
        {
            // Zebra pattern — pozadí celého řádku
            if (zebraPaint is not null && rowIndex % 2 == 1)
            {
                var zebraRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledRowH);
                ctx.Canvas.DrawRect(zebraRect, zebraPaint);
            }

            // Pozadí sloupce hodin
            RenderCellBackground(ctx, hourDataBgPaint, X, currentY, HourColumnWidth, scaledRowH);

            var hourStr = hour.ToString();
            float hourTextW = MeasureTextMm(hourDFont, hourStr, ctx.MmToPx);
            float hourTextX = hourDataHAlign switch
            {
                TextHorizontalAlignment.Center => X + hourDPadLeft + (HourColumnWidth - hourDPadLeft - hourDPadRight - hourTextW) / 2f,
                TextHorizontalAlignment.Right  => X + HourColumnWidth - hourTextW - hourDPadRight - 1f,
                _ => X + hourDPadLeft + 1f
            };
            var hourPt = ctx.ToCanvas(hourTextX, currentY + hourDPadTop + scaledRowH * TextBaselineFactor);
            ctx.Canvas.DrawText(hourStr, hourPt.X, hourPt.Y, hourDFont, hourDPaint);

            float gx = X + HourColumnWidth;
            foreach (var g in layout.Groups)
            {
                // Per-column pozadí datové oblasti (přes zebru)
                perGroupColBgPaints.TryGetValue(g.Skupina.Id, out var colBg);
                RenderCellBackground(ctx, colBg, gx, currentY, g.GroupWidth, scaledRowH);

                // Vybrat font pro data (per-column override nebo výchozí)
                var dataFont = perGroupDataFonts.TryGetValue(g.Skupina.Id, out var customDataFont)
                    ? customDataFont : minuteFont;

                if (g.ByHour.TryGetValue(hour, out var odjezdy))
                {
                    // Per-column padding
                    perGroupDataPadding.TryGetValue(g.Skupina.Id, out var dPad);

                    // Zarovnání bloku minut ve sloupci (s padding)
                    float availW = g.GroupWidth - dPad.Left - dPad.Right;
                    float blockW = odjezdy.Count * (scaledMinuteW + scaledOdjezdGap) - scaledOdjezdGap;
                    float alignOffset = dPad.Left;
                    if (perGroupDataHAlign.TryGetValue(g.Skupina.Id, out var dataHAlign))
                    {
                        alignOffset += dataHAlign switch
                        {
                            TextHorizontalAlignment.Center => (availW - blockW) / 2f,
                            TextHorizontalAlignment.Right  => availW - blockW - 0.5f,
                            _ => 0f
                        };
                    }
                    if (alignOffset < dPad.Left) alignOffset = dPad.Left;

                    float minX = gx + alignOffset;
                    SKPaint? customMinutePaint = null;
                    // Per-column text barva pro data
                    perGroupDataTextPaints.TryGetValue(g.Skupina.Id, out var colDataTextPaint);
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
                                minutePaint = colDataTextPaint ?? minuteStylePaint ?? paints.Text;
                            }

                            // Minuta
                            var minPt = ctx.ToCanvas(minX + 0.5f, currentY + dPad.Top + scaledRowH * TextBaselineFactor);
                            ctx.Canvas.DrawText(minuteText, minPt.X, minPt.Y, dataFont, minutePaint);

                            // Anotace — inline za minutou, s mezerou a per-typ stylem
                            if (ShowAnotace && odj.Anotace.Count > 0)
                            {
                                float minuteTextW = dataFont.MeasureText(minuteText);
                                float anotX = minPt.X + minuteTextW + anotGapPx;

                                foreach (var anot in odj.Anotace)
                                {
                                    SKFont aFont;
                                    SKPaint aPaint;
                                    if (anot.Typ == TypAnotace.Text)
                                    {
                                        aFont = anotSecondaryFont;
                                        aPaint = anotTextPaint ?? anotDefaultPaint;
                                    }
                                    else
                                    {
                                        aFont = anotPrimaryFont;
                                        aPaint = anotSymPaint ?? anotDefaultPaint;
                                    }

                                    ctx.Canvas.DrawText(anot.Znak, anotX, minPt.Y, aFont, aPaint);
                                    anotX += aFont.MeasureText(anot.Znak) + anotInterGapPx;
                                }
                            }

                            minX += scaledMinuteW + scaledOdjezdGap;
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
            rowIndex++;
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

        RenderOuterBorder(ctx, X, Y, usedWidth, currentY - Y, paints.Grid);

        } // try
        finally
        {
            foreach (var d in manualDisposables) d.Dispose();

            ctx.Canvas.Restore(); // Uvolnit clip rect
        }
    }


    private void RenderPlaceholder(RenderContext ctx)
    {
        float rowH = DataRowHeight ?? ComputeRowH(FontSize);
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

        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        return combinedHeaderH + layout.Hours.Count * layout.RowH;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<SkupinaDnuData> ResolveSkupiny(List<SkupinaDnuData> all)
    {
        if (all.Count == 0) return [];

        // GroupColumns má přednost — filtrovat podle IsVisible
        if (GroupColumns.Count > 0)
        {
            var hidden = GroupColumns
                .Where(gc => !gc.IsVisible)
                .Select(gc => gc.SkupinaDnuId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = all.Where(s => !hidden.Contains(s.Id)).ToList();
            return result.Count > 0 ? result : [all[0]];
        }

        // Fallback: starý SkupinaDnuId filtr (zpětná kompatibilita)
        if (string.IsNullOrWhiteSpace(SkupinaDnuId))
            return all;

        var ids = SkupinaDnuId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = new List<SkupinaDnuData>();
        foreach (var id in ids)
        {
            var match = all.Find(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (match is not null) filtered.Add(match);
        }

        return filtered.Count > 0 ? filtered : [all[0]];
    }

    /// <summary>Vrátí dostupné skupiny dnů z naposledy zpracovaných dat (pro dialog).</summary>
    public List<SkupinaDnuData> GetAvailableSkupinyDnu(DataContext? data) => BuildSkupinyDnu(data);

    private static List<int> GetVisibleHoursFromSet(
        HashSet<int> allHours, TimeAxisMode timeAxis, int rangeFrom, int rangeTo)
    {
        var hours = timeAxis switch
        {
            TimeAxisMode.Compact => allHours.OrderBy(h => h).ToList(),
            TimeAxisMode.Full => Enumerable.Range(0, 24).ToList(),
            TimeAxisMode.Range => Enumerable.Range(rangeFrom,
                Math.Max(1, rangeTo - rangeFrom + 1)).ToList(),
            _ => allHours.OrderBy(h => h).ToList()
        };

        // HODINY_OD/DO filtr se aplikuje i v Compact/Full režimu
        if (timeAxis != TimeAxisMode.Range && (rangeFrom > 0 || rangeTo < 23))
            hours = hours.Where(h => h >= rangeFrom && h <= rangeTo).ToList();

        return hours;
    }

    private static TimeAxisMode ParseTimeAxisMode(string s) => s.ToLowerInvariant() switch
    {
        "compact" => TimeAxisMode.Compact,
        "full" => TimeAxisMode.Full,
        "range" => TimeAxisMode.Range,
        _ => TimeAxisMode.Compact
    };

    private string[] WrapHeaderText(string fullText, float availableWidthMm, SKFont font, float mmToPx)
    {
        float padding = 2f;
        float maxW = availableWidthMm - padding;

        // Zkusit celý text na jeden řádek
        if (MeasureTextMm(font, fullText, mmToPx) <= maxW)
            return [fullText];

        // Word wrap — skládat slova do řádků
        var words = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (MeasureTextMm(font, testLine, mmToPx) <= maxW || currentLine.Length == 0)
            {
                currentLine = testLine;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = word;
            }
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine);

        return [.. lines];
    }

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
        // ── Výchozí styl (základ pro dědění — per-column nastavení ho přepisuje) ──
        .. GetScheduleBaseDescriptors(),

        new ButtonPropertyDescriptor { Label = "Nastavení odjezdů", Group = "Odjezdy", ButtonLabel = "Upravit odjezdy...", ActionId = ButtonPropertyDescriptor.ActionOpenDepartureGridEditor },

        new BoolPropertyDescriptor  { Label = "Záhlaví skupin",     Group = "Odjezdy", Get = () => ShowSkupinaDnuHeader,       Set = v => ShowSkupinaDnuHeader = v },
        new FloatPropertyDescriptor { Label = "Výška řádku záhl.",  Group = "Odjezdy", Get = () => SkupinaDnuHeaderLineHeight,  Set = v => SkupinaDnuHeaderLineHeight = v, Min = 2, Max = 10, Unit = "mm" },
        new EnumPropertyDescriptor  { Label = "Zarovnání záhlaví", Group = "Odjezdy",
            Get = () => HeaderVerticalAlignment, Set = v => HeaderVerticalAlignment = (VerticalTextAlignment)v,
            Options = [("Nahoru", (object)VerticalTextAlignment.Top), ("Na střed", (object)VerticalTextAlignment.Center), ("Dolů", (object)VerticalTextAlignment.Bottom)] },
        new FloatPropertyDescriptor { Label = "Šířka sl. hodin",    Group = "Odjezdy", Get = () => HourColumnWidth,            Set = v => HourColumnWidth = v, Min = 4, Max = 20, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Šířka minuty",       Group = "Odjezdy", Get = () => MinuteWidth,                Set = v => MinuteWidth = v, Min = 3, Max = 15, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Oddělovač skupin",   Group = "Odjezdy", Get = () => GroupSeparatorWidth,        Set = v => GroupSeparatorWidth = v, Min = 0, Max = 5, Unit = "mm" },

        new FloatPropertyDescriptor { Label = "Výška řádku",       Group = "Řádky dat", Get = () => DataRowHeight ?? 0, Set = v => DataRowHeight = v > 0 ? v : null, Min = 0, Max = 15, Unit = "mm" },
        new ColorPropertyDescriptor { Label = "Zebra barva",       Group = "Řádky dat", Get = () => AlternatingRowColor ?? "", Set = v => AlternatingRowColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl minuty ──
        new StringPropertyDescriptor { Label = "Font",         Group = "Styl minuty", Get = () => MinuteStyle.FontFamily ?? "", Set = v => MinuteStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Velikost",     Group = "Styl minuty", Get = () => MinuteStyle.FontSize ?? MinuteFontSize, Set = v => MinuteStyle.FontSize = v, Min = 3, Max = 24, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Styl minuty", Get = () => MinuteStyle.Bold ?? MinuteBold,         Set = v => MinuteStyle.Bold = v },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Styl minuty", Get = () => MinuteStyle.Italic ?? false,            Set = v => MinuteStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Barva textu",  Group = "Styl minuty", Get = () => MinuteStyle.TextColor ?? "",            Set = v => MinuteStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl anotací — symboly (speciální/NZ) ──
        new StringPropertyDescriptor { Label = "Font",         Group = "Anotace — symboly", Get = () => AnotaceSymbolStyle.FontFamily ?? "", Set = v => AnotaceSymbolStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Velikost",     Group = "Anotace — symboly", Get = () => AnotaceSymbolStyle.FontSize ?? 0, Set = v => AnotaceSymbolStyle.FontSize = v > 0 ? v : null, Min = 0, Max = 20, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Anotace — symboly", Get = () => AnotaceSymbolStyle.Bold ?? false,   Set = v => AnotaceSymbolStyle.Bold = v ? true : null },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Anotace — symboly", Get = () => AnotaceSymbolStyle.Italic ?? false, Set = v => AnotaceSymbolStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Barva textu",  Group = "Anotace — symboly", Get = () => AnotaceSymbolStyle.TextColor ?? "", Set = v => AnotaceSymbolStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl anotací — text (písmena, dny) ──
        new StringPropertyDescriptor { Label = "Font",         Group = "Anotace — text", Get = () => AnotaceTextStyle.FontFamily ?? "", Set = v => AnotaceTextStyle.FontFamily = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor  { Label = "Velikost",     Group = "Anotace — text", Get = () => AnotaceTextStyle.FontSize ?? AnotaceFontSize, Set = v => AnotaceTextStyle.FontSize = v, Min = 3, Max = 20, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Anotace — text", Get = () => AnotaceTextStyle.Bold ?? false,   Set = v => AnotaceTextStyle.Bold = v ? true : null },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Anotace — text", Get = () => AnotaceTextStyle.Italic ?? false, Set = v => AnotaceTextStyle.Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Barva textu",  Group = "Anotace — text", Get = () => AnotaceTextStyle.TextColor ?? "", Set = v => AnotaceTextStyle.TextColor = string.IsNullOrEmpty(v) ? null : v },

        new BoolPropertyDescriptor  { Label = "Anotace",              Group = "Anotace", Get = () => ShowAnotace,              Set = v => ShowAnotace = v },
        new BoolPropertyDescriptor  { Label = "Zkr. označ. linky",   Group = "Anotace", Get = () => ShowZkraceneOznaceniLinky, Set = v => ShowZkraceneOznaceniLinky = v },

        new FloatPropertyDescriptor { Label = "Mezera min. ↔ anot.",  Group = "Mezery", Get = () => AnotaceGap,      Set = v => AnotaceGap = v,      Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera anot. ↔ anot.", Group = "Mezery", Get = () => AnotaceInterGap, Set = v => AnotaceInterGap = v, Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera mezi odjezdy",  Group = "Mezery", Get = () => OdjezdGap,       Set = v => OdjezdGap = v,       Min = 0, Max = 10, Unit = "mm" },

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
            MinuteStyle                = MinuteStyle.Clone(),
            AnotaceSymbolStyle         = AnotaceSymbolStyle.Clone(),
            AnotaceTextStyle           = AnotaceTextStyle.Clone(),
            AnotaceGap                 = AnotaceGap,
            AnotaceInterGap            = AnotaceInterGap,
            OdjezdGap                  = OdjezdGap,
            GroupSeparatorWidth        = GroupSeparatorWidth,
            DataRowHeight              = DataRowHeight,
            MinuteBold                 = MinuteBold,
            AlternatingRowColor        = AlternatingRowColor,
            TimeAxisMode               = TimeAxisMode,
            TimeAxisRangeFrom          = TimeAxisRangeFrom,
            TimeAxisRangeTo            = TimeAxisRangeTo,
            ShowAnotace                = ShowAnotace,
            AnotaceFontSize            = AnotaceFontSize,
            ShowZkraceneOznaceniLinky   = ShowZkraceneOznaceniLinky,
            HourColumnStyle            = HourColumnStyle?.Clone(),
            GroupColumns               = GroupColumns.Select(gc => gc.Clone()).ToList(),
            ColorRules                 = ColorRules.Select(r => r.Clone()).ToList(),
        };
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
