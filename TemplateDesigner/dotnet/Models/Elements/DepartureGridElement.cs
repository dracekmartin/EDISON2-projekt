using SkiaSharp;
using TemplateDesigner.Rendering;

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
    // Záhlaví skupin dnů
    // ═══════════════════════════════════════════
    public bool ShowSkupinaDnuHeader { get; set; } = true;
    public float SkupinaDnuHeaderLineHeight { get; set; } = 4f;

    /// <summary>Zarovnání textu záhlaví skupin: Top, Center, Bottom.</summary>
    public TextVerticalAlignment HeaderVerticalAlignment { get; set; } = TextVerticalAlignment.Bottom;

    // ═══════════════════════════════════════════
    // Mřížka minut
    // ═══════════════════════════════════════════
    public float HourColumnWidth { get; set; } = 8f;
    public float MinuteWidth { get; set; } = 5f;
    public float MinuteFontSize { get; set; } = 7f;

    /// <summary>Styl textu minuty (font, bold, barva). Null properties = element defaults.</summary>
    public TextStyle MinuteStyle { get; set; } = new();

    /// <summary>Styl anotací typu speciální/NZ (symboly ×, F, H). Null properties = 80% velikosti minuty.</summary>
    public TextStyle AnotaceSymbolStyle { get; set; } = new();

    /// <summary>Styl anotací typu text (písmena a, A, dny). Null properties = AnotaceFontSize.</summary>
    public TextStyle AnotaceTextStyle { get; set; } = new();

    /// <summary>Mezera mezi minutou a první anotací (mm). 0 = bez mezery.</summary>
    public float AnotaceGap { get; set; } = 0f;

    /// <summary>Mezera mezi anotacemi navzájem (mm). 0 = bez mezery.</summary>
    public float AnotaceInterGap { get; set; } = 0f;

    /// <summary>Mezera mezi odjezdy v rámci jedné buňky (mm). 0 = jen MinuteWidth slot.</summary>
    public float OdjezdGap { get; set; } = 0f;

    /// <summary>Šířka oddělovače mezi skupinami dnů (mm).</summary>
    public float GroupSeparatorWidth { get; set; } = 0.5f;

    /// <summary>Sloupec hodin před každou skupinou dnů (místo jednoho sdíleného vlevo).</summary>
    public bool HourColumnPerGroup { get; set; }

    /// <summary>Maximální počet zobrazených skupin dnů (0 = bez limitu).</summary>
    public int MaxGroupCount { get; set; } = 3;

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

    // ═══════════════════════════════════════════
    // Nastavení sloupců
    // ═══════════════════════════════════════════

    /// <summary>Vizuální styl sloupce hodin (záhlaví "Hod" + čísla hodin). Null = element defaults.</summary>
    public ColumnVisualStyle? HourColumnStyle
    {
        get => _hourColumnStyle;
        set { _hourColumnStyle = value; _cachedLayout = null; }
    }
    private ColumnVisualStyle? _hourColumnStyle;

    public List<DepartureGroupColumnStyle> GroupColumns
    {
        get => _groupColumns;
        set { _groupColumns = value; _cachedLayout = null; }
    }
    private List<DepartureGroupColumnStyle> _groupColumns = [];

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
    [System.Text.Json.Serialization.JsonIgnore]
    private int _cachedLayoutFingerprint;

    /// <summary>Rychlý fingerprint layout-affecting properties pro invalidaci cache.</summary>
    private int ComputeLayoutFingerprint() => HashCode.Combine(
        HeaderStyle.FontSize, DataStyle.FontSize, SkupinaDnuHeaderLineHeight,
        MinuteFontSize, MinuteWidth, HourColumnWidth, MaxGroupCount,
        GroupColumns.Count);

    private GridLayout? ComputeLayout(DataContext? data, float mmToPx = 0f)
    {
        // Cache hit — stejná data A stejné layout-affecting properties
        int fp = ComputeLayoutFingerprint();
        if (_cachedLayout is not null && ReferenceEquals(data, _cachedLayoutDataRef) && fp == _cachedLayoutFingerprint)
            return _cachedLayout;

        var skupinyDnu = BuildSkupinyDnu(data);
        var odjezdyBySkupina = BuildOdjezdyBySkupina(data);

        var targetSkupiny = ResolveSkupiny(skupinyDnu);
        if (MaxGroupCount > 0 && targetSkupiny.Count > MaxGroupCount)
            targetSkupiny = targetSkupiny.Take(MaxGroupCount).ToList();
        if (targetSkupiny.Count == 0)
        {
            _cachedLayout = null;
            _cachedLayoutDataRef = data;
            return null;
        }

        // Font pro měření textu záhlaví (default, přepíše se per-column)
        float effectiveMmToPx = mmToPx > 0 ? mmToPx : 3.78f;

        // Per-column header font lookup pro měření i wrapping (resolves __INDEX_ keys)
        var groupIds = targetSkupiny.Select(s => s.Id).ToList();
        var colFontLookup = GroupColumnIndexResolver.BuildHeaderFontLookup(GroupColumns, groupIds);
        float defaultHeaderFontPt = HeaderStyle.FontSize ?? 9f;

        var groupLayouts = new List<GroupLayout>();
        foreach (var sk in targetSkupiny)
        {
            if (!odjezdyBySkupina.TryGetValue(sk.Id, out var byHour))
                byHour = new Dictionary<int, List<OdjezdData>>();

            string fullName = sk.Nadpis;
            if (sk.Nadpis2.Length > 0) fullName += " " + sk.Nadpis2;
            var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Per-column font pro měření minimální šířky
            colFontLookup.TryGetValue(sk.Id, out var colFontPt);
            float fontPt = colFontPt ?? defaultHeaderFontPt;
            float fontPx = fontPt * PtToMm * effectiveMmToPx;
            using var measureFont = new SKFont(
                GetTypeface(HeaderStyle.FontFamily, SKFontStyle.Bold), fontPx);

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
        int hourColumnCount = HourColumnPerGroup ? finalGroups.Count : 1;
        float minTotal = hourColumnCount * HourColumnWidth + finalGroups.Sum(g => g.GroupWidth) + separatorTotal;

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

        // Word-wrap záhlaví skupin podle skutečné šířky sloupce a per-column fontu
        finalGroups = finalGroups.Select(g =>
        {
            string fullText = g.Skupina.Nadpis;
            if (g.Skupina.Nadpis2.Length > 0) fullText += " " + g.Skupina.Nadpis2;

            // Použít per-column font nebo element default
            colFontLookup.TryGetValue(g.Skupina.Id, out var colFontPt);
            float fontPt = colFontPt ?? defaultHeaderFontPt;
            float fontPx = fontPt * PtToMm * effectiveMmToPx;
            using var wrapFont = new SKFont(
                GetTypeface(HeaderStyle.FontFamily, SKFontStyle.Bold), fontPx);

            var wrapped = WrapHeaderText(fullText, g.GroupWidth, wrapFont, effectiveMmToPx);
            return g with { HeaderLines = wrapped };
        }).ToList();

        float effFontSize = DataStyle.FontSize ?? 8f;
        float effHeaderFontSize = HeaderStyle.FontSize ?? 9f;
        float fontSizeMm = effFontSize * PtToMm;
        float headerFontMm = effHeaderFontSize * PtToMm;
        float minuteFontMm = MinuteFontSize * PtToMm;
        float rowH = DataRowHeight ?? ComputeRowH(effFontSize);
        float headerH = ComputeHeaderH(effHeaderFontSize);

        // Výška řádku záhlaví — zohlednit per-column font (větší font = vyšší řádek)
        float maxGroupLineH = SkupinaDnuHeaderLineHeight;
        foreach (var g in finalGroups)
        {
            colFontLookup.TryGetValue(g.Skupina.Id, out var colFontPt);
            float fontPt = colFontPt ?? defaultHeaderFontPt;
            float colLineH = fontPt * PtToMm * 1.5f;
            if (colLineH > maxGroupLineH) maxGroupLineH = colLineH;
        }

        int maxHeaderLines = finalGroups.Max(g => g.HeaderLines.Length);
        float groupHeaderH = ShowSkupinaDnuHeader
            ? maxHeaderLines * maxGroupLineH
            : 0f;

        var result = new GridLayout(finalGroups, hours, fontSizeMm, headerFontMm, minuteFontMm,
            rowH, headerH, groupHeaderH, maxHeaderLines);

        _cachedLayout = result;
        _cachedLayoutDataRef = data;
        _cachedLayoutFingerprint = fp;
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Style resolution (kaskáda column → element → CellDefaults)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Parent kaskády pro per-column override záhlaví sloupce hodin.</summary>
    public ResolvedTextStyle ResolveHourHeaderParent()
    {
        var p = HeaderStyle.Resolve(ScheduleHeaderDefaults);
        if (HeaderStyle.VAlign is null)
            p = p with { VAlign = HeaderVerticalAlignment };
        return p;
    }

    /// <summary>Parent kaskády pro per-column override datové buňky sloupce hodin.</summary>
    public ResolvedTextStyle ResolveHourDataParent() => DataStyle.Resolve(ScheduleDataDefaults);

    /// <summary>Parent kaskády pro per-column override záhlaví skupiny dnů.</summary>
    public ResolvedTextStyle ResolveGroupHeaderParent()
    {
        var p = HeaderStyle.Resolve(ScheduleHeaderDefaults);

        // Legacy fallback: pokud user nenastavil FontSize v HeaderStyle, odvodit z SkupinaDnuHeaderLineHeight.
        // (Priorita: HeaderStyle.FontSize > SkupinaDnuHeaderLineHeight-derived > ScheduleHeaderDefaults.FontSize)
        if (HeaderStyle.FontSize is null)
        {
            float legacyDefaultPt = SkupinaDnuHeaderLineHeight * 0.65f / PtToMm;
            p = p with { FontSize = legacyDefaultPt };
        }
        if (HeaderStyle.VAlign is null)
            p = p with { VAlign = HeaderVerticalAlignment };
        if (HeaderStyle.HAlign is null)
            p = p with { HAlign = TextHorizontalAlignment.Center };
        return p;
    }

    /// <summary>Parent kaskády pro per-column override minutové buňky skupiny.</summary>
    public ResolvedTextStyle ResolveGroupMinuteParent()
    {
        var dataResolved = DataStyle.Resolve(ScheduleDataDefaults);
        var p = MinuteStyle.Resolve(dataResolved);

        // Legacy fallback: scalar MinuteFontSize/MinuteBold platí pouze když cascade (MinuteStyle + DataStyle)
        // nenastavil explicit. Tím se user-set DataStyle.FontSize/Bold z tabu Obecné nepřebije.
        if (MinuteStyle.FontSize is null && DataStyle.FontSize is null)
            p = p with { FontSize = MinuteFontSize };
        if (MinuteStyle.Bold is null && DataStyle.Bold is null)
            p = p with { Bold = MinuteBold };

        // Minutové buňky: padding default 0 (hustá mřížka)
        p = p with
        {
            PaddingLeft   = MinuteStyle.PaddingLeft   ?? 0f,
            PaddingRight  = MinuteStyle.PaddingRight  ?? 0f,
            PaddingTop    = MinuteStyle.PaddingTop    ?? 0f,
            PaddingBottom = MinuteStyle.PaddingBottom ?? 0f,
        };
        return p;
    }

    /// <summary>Vyřeší styl záhlaví sloupce hodin.</summary>
    private ResolvedTextStyle ResolveHourHeaderStyle()
    {
        var parent = ResolveHourHeaderParent();
        return HourColumnStyle?.Header is { } h ? h.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl datové buňky sloupce hodin.</summary>
    private ResolvedTextStyle ResolveHourDataStyle()
    {
        var parent = ResolveHourDataParent();
        return HourColumnStyle?.Data is { } d ? d.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl záhlaví skupiny dnů.</summary>
    private ResolvedTextStyle ResolveGroupHeaderStyle(DepartureGroupColumnStyle? cs)
    {
        var parent = ResolveGroupHeaderParent();
        return cs?.Style.Header is { } h ? h.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl minutové buňky (per-skupina).</summary>
    private ResolvedTextStyle ResolveGroupMinuteStyle(DepartureGroupColumnStyle? cs)
    {
        var parent = ResolveGroupMinuteParent();
        return cs?.Style.Data is { } d ? d.Resolve(parent) : parent;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Render
    // ═══════════════════════════════════════════════════════════════════════════

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        RenderGrid(ctx);
    }

    private void RenderGrid(RenderContext ctx)
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
        float fontRowH = ComputeRowH(DataStyle.FontSize ?? 8f);
        float scaleNaturalH = combinedHeaderH + layout.Hours.Count * fontRowH;
        float scale = ComputeFontScale(scaleNaturalH);

        // Pokud je DataRowHeight explicitně nastavená, nescalovat ji — uživatel ji řídí přímo.
        // Scale se aplikuje jen na fonty a záhlaví.
        float scaledRowH = DataRowHeight.HasValue ? layout.RowH : layout.RowH * scale;
        float scaledGroupHeaderH = layout.GroupHeaderH * scale;
        float scaledCombinedHeaderH = combinedHeaderH * scale;

        // Clip na hranice elementu
        float clipH = scaledCombinedHeaderH + layout.Hours.Count * scaledRowH;
        var clipRect = ctx.ToCanvasRect(X, Y, Width, clipH);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);

        using var paints = CreateStandardPaints();

        // ── Style lookup — jednou na frame, resolves __INDEX_ keys ──────────
        var layoutGroupIds = layout.Groups.Select(g => g.Skupina.Id).ToList();
        var colStyles = GroupColumnIndexResolver.BuildStyleLookup(GroupColumns, layoutGroupIds);

        // ── Kaskáda stylů (column → element → CellDefaults) ─────────────────
        var hourHeaderStyle = ResolveHourHeaderStyle();
        var hourDataStyle = ResolveHourDataStyle();

        var groupHeaderStyles = new Dictionary<string, ResolvedTextStyle>();
        var groupMinuteStyles = new Dictionary<string, ResolvedTextStyle>();
        foreach (var g in layout.Groups)
        {
            colStyles.TryGetValue(g.Skupina.Id, out var cs);
            groupHeaderStyles[g.Skupina.Id] = ResolveGroupHeaderStyle(cs);
            groupMinuteStyles[g.Skupina.Id] = ResolveGroupMinuteStyle(cs);
        }

        // Anotace — element-level (nedědí z column.Data)
        var dataBase = DataStyle.Resolve(ScheduleDataDefaults);
        var minuteElementStyle = ResolveGroupMinuteStyle(null);
        var anotSymStyle = AnotaceSymbolStyle.Resolve(dataBase);
        if (AnotaceSymbolStyle.FontSize is null)
            anotSymStyle = anotSymStyle with { FontSize = minuteElementStyle.FontSize * 0.8f };
        var anotTextStyle = AnotaceTextStyle.Resolve(dataBase);
        if (AnotaceTextStyle.FontSize is null)
            anotTextStyle = anotTextStyle with { FontSize = AnotaceFontSize };

        // ── Pre-create per-group fonts + paints (perf: 100+ buněk/frame) ────
        var groupHeaderFonts       = new Dictionary<string, SKFont>();
        var groupHeaderTextPaints  = new Dictionary<string, SKPaint>();
        var groupHeaderBgPaints    = new Dictionary<string, SKPaint?>();
        var groupMinuteFonts       = new Dictionary<string, SKFont>();
        var groupMinuteTextPaints  = new Dictionary<string, SKPaint>();
        var groupMinuteBgPaints    = new Dictionary<string, SKPaint?>();

        foreach (var g in layout.Groups)
        {
            var id = g.Skupina.Id;
            var hs = groupHeaderStyles[id];
            var ms = groupMinuteStyles[id];

            groupHeaderFonts[id]      = hs.CreateFont(scale, ctx.MmToPx);
            groupHeaderTextPaints[id] = hs.CreateTextPaint();
            groupHeaderBgPaints[id]   = hs.CreateBgPaint();

            groupMinuteFonts[id]      = ms.CreateFont(scale, ctx.MmToPx);
            groupMinuteTextPaints[id] = ms.CreateTextPaint();
            groupMinuteBgPaints[id]   = ms.CreateBgPaint();
        }

        // Anotace fonty/painty — element-level, sdílené pro všechny buňky
        var anotSymFont   = anotSymStyle.CreateFont(scale, ctx.MmToPx);
        var anotSymPaint  = anotSymStyle.CreateTextPaint();
        var anotTextFont  = anotTextStyle.CreateFont(scale, ctx.MmToPx);
        var anotTextPaint = anotTextStyle.CreateTextPaint();

        float anotGapPx      = AnotaceGap * scale * ctx.MmToPx;
        float anotInterGapPx = AnotaceInterGap * scale * ctx.MmToPx;
        float scaledOdjezdGap = OdjezdGap * scale; // v mm (model space), ne v px

        // Zebra — globální + per-group override
        SKPaint? zebraPaint = null;
        if (AlternatingRowColor is not null)
            zebraPaint = new SKPaint { Color = SKColor.Parse(AlternatingRowColor) };

        var perGroupZebraPaints = new Dictionary<string, SKPaint>();
        foreach (var g in layout.Groups)
        {
            if (colStyles.TryGetValue(g.Skupina.Id, out var cs) && cs.AlternatingRowColor is not null)
                perGroupZebraPaints[g.Skupina.Id] = new SKPaint { Color = SKColor.Parse(cs.AlternatingRowColor) };
        }
        bool hasPerGroupZebra = perGroupZebraPaints.Count > 0;

        float usedWidth = Width;
        float currentY = Y;
        float scaledMinuteW = (MinuteWidth + AnotaceGap) * scale;

        try
        {
            RenderGridHeader();
            RenderGridRows();
            RenderGridVerticalSeparators();
            RenderOuterBorder(ctx, X, Y, usedWidth, currentY - Y, paints.Grid);
        }
        finally
        {
            foreach (var f in groupHeaderFonts.Values)      f.Dispose();
            foreach (var p in groupHeaderTextPaints.Values) p.Dispose();
            foreach (var p in groupHeaderBgPaints.Values)   p?.Dispose();
            foreach (var f in groupMinuteFonts.Values)      f.Dispose();
            foreach (var p in groupMinuteTextPaints.Values) p.Dispose();
            foreach (var p in groupMinuteBgPaints.Values)   p?.Dispose();
            anotSymFont.Dispose();   anotSymPaint.Dispose();
            anotTextFont.Dispose();  anotTextPaint.Dispose();
            zebraPaint?.Dispose();
            foreach (var p in perGroupZebraPaints.Values)   p.Dispose();

            ctx.Canvas.Restore(); // Uvolnit clip rect
        }

        // ─── Local helpers (capture outer state) ─────────────────────────────

        // Nakreslí "Hod" záhlaví (celá kombinovaná výška) přes CellRenderer.
        // bgColorOverride: HourColumnPerGroup přebírá barvu pozadí skupiny.
        void DrawHourHeaderCell(float hx, string? bgColorOverride = null)
        {
            var style = bgColorOverride is not null
                ? hourHeaderStyle with { BackgroundColor = bgColorOverride }
                : hourHeaderStyle;
            var rect = new SKRect(hx, currentY, hx + HourColumnWidth, currentY + scaledCombinedHeaderH);
            CellRenderer.DrawCell(ctx, rect, "Hod", style, scale);
        }

        // Nakreslí datovou buňku sloupce hodin přes CellRenderer.
        void DrawHourDataCell(float hx, int hour, string? bgColorOverride = null)
        {
            var style = bgColorOverride is not null
                ? hourDataStyle with { BackgroundColor = bgColorOverride }
                : hourDataStyle;
            var rect = new SKRect(hx, currentY, hx + HourColumnWidth, currentY + scaledRowH);
            CellRenderer.DrawCell(ctx, rect, hour.ToString(), style, scale);
        }

        // ── Sloučené záhlaví (Hod + názvy skupin dnů) ────────────────────────
        void RenderGridHeader()
        {
            // Base BG přes celou šířku — pokryje mezery mezi skupinami (GroupSeparatorWidth).
            var headerRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledCombinedHeaderH);
            ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

            if (!HourColumnPerGroup)
                DrawHourHeaderCell(X);

            if (ShowSkupinaDnuHeader && scaledGroupHeaderH > 0)
            {
                float gx = HourColumnPerGroup ? X : X + HourColumnWidth;
                foreach (var g in layout.Groups)
                {
                    var gid = g.Skupina.Id;
                    var hs = groupHeaderStyles[gid];
                    var hFont = groupHeaderFonts[gid];
                    var hTextPaint = groupHeaderTextPaints[gid];
                    var hBgPaint = groupHeaderBgPaints[gid];

                    // HourColumnPerGroup — "Hod" buňka dědí BG skupiny
                    if (HourColumnPerGroup)
                    {
                        DrawHourHeaderCell(gx, hs.BackgroundColor);
                        gx += HourColumnWidth;
                    }

                    // BG skupiny
                    if (hBgPaint is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(gx, currentY, g.GroupWidth, scaledCombinedHeaderH);
                        ctx.Canvas.DrawRect(bgRect, hBgPaint);
                    }

                    // Multi-line text — line height odvozená z cascade FontSize
                    float colLineH = hs.FontSize * PtToMm * 1.5f * scale;
                    int lineCount = g.HeaderLines.Length;
                    float blockH = lineCount * colLineH;

                    float hPadTop    = hs.PaddingTop    * scale;
                    float hPadBottom = hs.PaddingBottom * scale;
                    float hPadLeft   = hs.PaddingLeft   * scale;
                    float hPadRight  = hs.PaddingRight  * scale;

                    float startLineY = hs.VAlign switch
                    {
                        TextVerticalAlignment.Top    => currentY + hPadTop,
                        TextVerticalAlignment.Middle => currentY + (scaledCombinedHeaderH - blockH) / 2f,
                        _ /* Bottom */               => currentY + scaledCombinedHeaderH - blockH - hPadBottom
                    };

                    for (int i = 0; i < lineCount; i++)
                    {
                        float lineY = startLineY + (i + 0.8f) * colLineH;
                        float textW = MeasureTextMm(hFont, g.HeaderLines[i], ctx.MmToPx);
                        float textX = hs.HAlign switch
                        {
                            TextHorizontalAlignment.Left  => gx + hPadLeft + 1f,
                            TextHorizontalAlignment.Right => gx + g.GroupWidth - textW - hPadRight - 1f,
                            _ /* Center */                => gx + hPadLeft + (g.GroupWidth - hPadLeft - hPadRight - textW) / 2f
                        };
                        var pt = ctx.ToCanvas(textX, lineY);
                        ctx.Canvas.DrawText(g.HeaderLines[i], pt.X, pt.Y, hFont, hTextPaint);
                    }

                    gx += g.GroupWidth + GroupSeparatorWidth;
                }
            }
            else if (HourColumnPerGroup)
            {
                // Bez záhlaví skupin, ale stále kreslit hour headers per group
                float gx = X;
                foreach (var g in layout.Groups)
                {
                    DrawHourHeaderCell(gx);
                    gx += HourColumnWidth + g.GroupWidth + GroupSeparatorWidth;
                }
            }

            currentY += scaledCombinedHeaderH;
        }

        // ── Řádky hodin (zebra + hour buňky + minuty + anotace + h-čáry) ─────
        void RenderGridRows()
        {
            int rowIndex = 0;
            foreach (int hour in layout.Hours)
            {
                DrawRowZebra(rowIndex);

                if (!HourColumnPerGroup)
                    DrawHourDataCell(X, hour);

                float gx = HourColumnPerGroup ? X : X + HourColumnWidth;
                foreach (var g in layout.Groups)
                {
                    var gid = g.Skupina.Id;
                    var ms = groupMinuteStyles[gid];
                    var mBgPaint = groupMinuteBgPaints[gid];

                    if (HourColumnPerGroup)
                    {
                        DrawHourDataCell(gx, hour, ms.BackgroundColor);
                        gx += HourColumnWidth;
                    }

                    // BG datové buňky skupiny (null = průhledná, ponechá zebra)
                    if (mBgPaint is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(gx, currentY, g.GroupWidth, scaledRowH);
                        ctx.Canvas.DrawRect(bgRect, mBgPaint);
                    }

                    if (g.ByHour.TryGetValue(hour, out var odjezdy))
                        DrawMinuteCell(g, gx, odjezdy);

                    gx += g.GroupWidth + GroupSeparatorWidth;
                }

                var rowLineStart = ctx.ToCanvas(X, currentY + scaledRowH);
                var rowLineEnd = ctx.ToCanvas(X + usedWidth, currentY + scaledRowH);
                ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
                currentY += scaledRowH;
                rowIndex++;
            }
        }

        // Zebra podbarvení sudých řádků — globální nebo per-group
        void DrawRowZebra(int rowIndex)
        {
            if (rowIndex % 2 != 1) return;

            if (hasPerGroupZebra)
            {
                float zx = HourColumnPerGroup ? X : X + HourColumnWidth;

                // Sdílený hour column (pokud není per-group)
                if (!HourColumnPerGroup && zebraPaint is not null)
                {
                    var r = ctx.ToCanvasRect(X, currentY, HourColumnWidth, scaledRowH);
                    ctx.Canvas.DrawRect(r, zebraPaint);
                }

                foreach (var g in layout.Groups)
                {
                    var gZebra = perGroupZebraPaints.TryGetValue(g.Skupina.Id, out var gzp)
                        ? gzp : zebraPaint;
                    if (gZebra is not null)
                    {
                        if (HourColumnPerGroup)
                        {
                            var hr = ctx.ToCanvasRect(zx, currentY, HourColumnWidth, scaledRowH);
                            ctx.Canvas.DrawRect(hr, gZebra);
                            zx += HourColumnWidth;
                        }
                        var dr = ctx.ToCanvasRect(zx, currentY, g.GroupWidth, scaledRowH);
                        ctx.Canvas.DrawRect(dr, gZebra);
                    }
                    else if (HourColumnPerGroup)
                        zx += HourColumnWidth;

                    zx += g.GroupWidth + GroupSeparatorWidth;
                }
            }
            else if (zebraPaint is not null)
            {
                // Globální zebra fallback — celá šířka
                var zebraRect = ctx.ToCanvasRect(X, currentY, usedWidth, scaledRowH);
                ctx.Canvas.DrawRect(zebraRect, zebraPaint);
            }
        }

        // Nakreslí minuty + anotace jedné buňky (jedna skupina × jedna hodina)
        void DrawMinuteCell(GroupLayout g, float gx, List<OdjezdData> odjezdy)
        {
            var gid = g.Skupina.Id;
            var ms = groupMinuteStyles[gid];
            var mFont = groupMinuteFonts[gid];
            var mTextPaint = groupMinuteTextPaints[gid];

            float padL = ms.PaddingLeft * scale;
            float padR = ms.PaddingRight * scale;
            float padT = ms.PaddingTop * scale;

            // Zarovnání bloku minut ve sloupci
            float availW = g.GroupWidth - padL - padR;
            float blockW = odjezdy.Count * (scaledMinuteW + scaledOdjezdGap) - scaledOdjezdGap;
            float alignOffset = padL + ms.HAlign switch
            {
                TextHorizontalAlignment.Center => (availW - blockW) / 2f,
                TextHorizontalAlignment.Right  => availW - blockW - 0.5f,
                _ => 0f
            };
            if (alignOffset < padL) alignOffset = padL;

            float minX = gx + alignOffset;
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
                        minutePaint = mTextPaint;
                    }

                    var minPt = ctx.ToCanvas(minX + 0.5f, currentY + padT + scaledRowH * TextBaselineFactor);
                    ctx.Canvas.DrawText(minuteText, minPt.X, minPt.Y, mFont, minutePaint);

                    // Anotace — inline za minutou
                    if (ShowAnotace && odj.Anotace.Count > 0)
                    {
                        float minuteTextW = mFont.MeasureText(minuteText);
                        float anotX = minPt.X + minuteTextW + anotGapPx;

                        foreach (var anot in odj.Anotace)
                        {
                            SKFont aFont;
                            SKPaint aPaint;
                            if (anot.Typ == TypAnotace.Text)
                            {
                                aFont = anotTextFont;
                                aPaint = anotTextPaint;
                            }
                            else
                            {
                                aFont = anotSymFont;
                                aPaint = anotSymPaint;
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

        // ── Svislé čáry mezi sloupci hodin a skupinami ───────────────────────
        void RenderGridVerticalSeparators()
        {
            float vLineTopY = Y;

            if (!HourColumnPerGroup)
            {
                // Jedna čára za sdíleným sloupcem hodin
                var hourVLineTop = ctx.ToCanvas(X + HourColumnWidth, vLineTopY);
                var hourVLineBottom = ctx.ToCanvas(X + HourColumnWidth, currentY);
                ctx.Canvas.DrawLine(hourVLineTop, hourVLineBottom, paints.Grid);
            }

            float gxSep = HourColumnPerGroup ? X : X + HourColumnWidth;
            for (int i = 0; i < layout.Groups.Count; i++)
            {
                if (HourColumnPerGroup)
                {
                    // Čára za hour sloupcem této skupiny
                    gxSep += HourColumnWidth;
                    var hLineTop = ctx.ToCanvas(gxSep, vLineTopY);
                    var hLineBottom = ctx.ToCanvas(gxSep, currentY);
                    ctx.Canvas.DrawLine(hLineTop, hLineBottom, paints.Grid);
                }

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
        }
    }


    private void RenderPlaceholder(RenderContext ctx)
    {
        // Použít kaskádu (element → ScheduleDefaults → CellDefaults), aby placeholder
        // v designeru ukazoval to samé, co preview s daty (bez per-column override).
        var headerResolved = ResolveElementHeaderParent();
        var dataResolved   = ResolveElementDataParent();

        float effFontSize = dataResolved.FontSize;
        float effHeaderFontSize = headerResolved.FontSize;
        float rowH = DataRowHeight ?? ComputeRowH(effFontSize);
        float headerH = ComputeHeaderH(effHeaderFontSize);

        using var paints = CreateStandardPaints();

        float fontPx = effFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = effHeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(GetTypeface(dataResolved.FontFamily), fontPx);
        using var headerFont = new SKFont(
            GetTypeface(headerResolved.FontFamily, SKFontStyle.Bold), headerFontPx);

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
            Get = () => HeaderVerticalAlignment, Set = v => HeaderVerticalAlignment = (TextVerticalAlignment)v,
            Options = [("Nahoru", (object)TextVerticalAlignment.Top), ("Na střed", (object)TextVerticalAlignment.Middle), ("Dolů", (object)TextVerticalAlignment.Bottom)] },
        new FloatPropertyDescriptor { Label = "Šířka sl. hodin",    Group = "Odjezdy", Get = () => HourColumnWidth,            Set = v => HourColumnWidth = v, Min = 4, Max = 20, Unit = "mm" },
        new BoolPropertyDescriptor  { Label = "Hodiny u každé sk.",  Group = "Odjezdy", Get = () => HourColumnPerGroup,        Set = v => HourColumnPerGroup = v },
        new FloatPropertyDescriptor { Label = "Max skupin",          Group = "Odjezdy", Get = () => MaxGroupCount,             Set = v => MaxGroupCount = (int)v, Min = 0, Max = 10, Format = "0", Unit = "(0=∞)" },
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

        new FloatPropertyDescriptor { Label = "Mezera min. ↔ anot.",  Group = "Mezery", Get = () => AnotaceGap,      Set = v => AnotaceGap = v,      Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera anot. ↔ anot.", Group = "Mezery", Get = () => AnotaceInterGap, Set = v => AnotaceInterGap = v, Min = 0, Max = 5, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Mezera mezi odjezdy",  Group = "Mezery", Get = () => OdjezdGap,       Set = v => OdjezdGap = v,       Min = 0, Max = 10, Unit = "mm" },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        SkupinaDnuHeaderLineHeight *= scale;
        HourColumnWidth *= scale;
        MinuteWidth *= scale;
        MinuteFontSize *= scale;
        AnotaceFontSize *= scale;
        AnotaceGap *= scale;
        AnotaceInterGap *= scale;
        OdjezdGap *= scale;
        GroupSeparatorWidth *= scale;
        if (DataRowHeight is not null) DataRowHeight *= scale;
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new DepartureGridElement
        {
            SkupinaDnuId               = SkupinaDnuId,
            ShowSkupinaDnuHeader       = ShowSkupinaDnuHeader,
            SkupinaDnuHeaderLineHeight = SkupinaDnuHeaderLineHeight,
            HeaderVerticalAlignment    = HeaderVerticalAlignment,
            HourColumnWidth            = HourColumnWidth,
            HourColumnPerGroup         = HourColumnPerGroup,
            MaxGroupCount              = MaxGroupCount,
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
            HourColumnStyle            = HourColumnStyle?.Clone(),
            GroupColumns               = GroupColumns.Select(gc => gc.Clone()).ToList(),
            ColorRules                 = ColorRules.Select(r => r.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
