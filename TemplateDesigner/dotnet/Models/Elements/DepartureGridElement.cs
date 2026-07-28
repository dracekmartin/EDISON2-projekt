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
public class DepartureGridElement : StopScheduleElementBase
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

    /// <summary>
    /// Styl 2. řádku záhlaví skupin dnů (např. „den" v „Pracovní / den"). Null = stejný jako HeaderStyle (Line 1).
    /// Aplikuje se POUZE když SkupinaDnu.Nadpis2 je vyplněn. Cascade: HeaderLine2Style → HeaderStyle → ScheduleHeaderDefaults.
    /// </summary>
    public TextStyle? HeaderLine2Style { get; set; }

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

    /// <summary>Maximální počet zobrazených skupin dnů (0 = bez limitu, default). Viditelnost
    /// jednotlivých skupin řídí checkboxy v dialogu sloupců (GroupColumns.IsVisible) — strop je
    /// jen nouzová pojistka; starý default 3 tiše ořezával 4. SMO sloupec (migrace 1.4.0).</summary>
    public int MaxGroupCount { get; set; }

    // ═══════════════════════════════════════════
    // Časová osa
    // ═══════════════════════════════════════════
    public TimeAxisMode TimeAxisMode { get; set; } = TimeAxisMode.Compact;
    public int TimeAxisRangeFrom { get; set; } = 4;
    public int TimeAxisRangeTo { get; set; } = 23;

    /// <summary>
    /// Když <see cref="TemplateElement.Height"/> je pevné a obsah je kratší (méně řádků než kapacita),
    /// roztáhne datové řádky a jejich fonty proporcionálně tak, aby vyplnily celou výšku elementu.
    /// Hlavička (skupiny dnů, „Hod" sloupec) zůstává původní velikost.
    /// Override z PrintDialogu přes parametr <c>ROZTAHNOUT_NA_VYSKU</c>.
    /// Default <c>false</c> — zachovává chování před přidáním této property (prázdné místo dole).
    /// </summary>
    public bool AutoExpandToFillHeight { get; set; }

    /// <summary>
    /// Strop pro <see cref="AutoExpandToFillHeight"/> — datové řádky se roztáhnou max na tento násobek
    /// přirozené výšky. Brání bizarně obřím řádkům u řídkých mřížek (např. linka jedoucí jen v 1 hodinu).
    /// </summary>
    private const float MaxAutoExpandFactor = 1.5f;

    // ═══════════════════════════════════════════
    // Řádky dat (DataRowHeight je na ScheduleElementBase)
    // ═══════════════════════════════════════════

    /// <summary>Minuty tučně.</summary>
    public bool MinuteBold { get; set; } = true;

    /// <summary>Barva pozadí sudých řádků (zebra pattern). Null = bez podbarvení.</summary>
    public string? AlternatingRowColor { get; set; } = "#FFF5F5F5";

    // ═══════════════════════════════════════════
    // Anotace
    // ═══════════════════════════════════════════
    public bool ShowAnotace { get; set; } = true;
    public float AnotaceFontSize { get; set; } = 5f;

    /// <summary>Pozadí invertované NZ u minuty (#AARRGGBB). Default černá.</summary>
    public string NegativniZnackaBackColor { get; set; } = "#FF000000";

    /// <summary>Barva znaku invertované NZ u minuty (#AARRGGBB). Default bílá.</summary>
    public string NegativniZnackaForeColor { get; set; } = "#FFFFFFFF";

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

    /// <summary>Override nadpisu sloupce hodin v šabloně. Prázdné = výchozí „Hod"
    /// (přes <see cref="Rendering.ColumnHeader.Resolve"/>, jednotně se skupinami i ostatními komponentami).</summary>
    public string HourColumnHeader { get; set; } = "";

    public List<DepartureGroupColumnStyle> GroupColumns
    {
        get => _groupColumns;
        set { _groupColumns = value; _cachedLayout = null; }
    }
    private List<DepartureGroupColumnStyle> _groupColumns = [];

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 60f;

    // ═══════════════════════════════════════════════════════════════════════════
    // Layout computation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Jeden řádek záhlaví skupiny dnů. IsSecondary = pochází z Nadpis2 (jiný styl).</summary>
    private record HeaderLine(string Text, bool IsSecondary);

    private record GroupLayout(
        SkupinaDnuData Skupina,
        Dictionary<int, List<OdjezdData>> ByHour,
        int MaxMinutesPerHour,
        float GroupWidth,
        HeaderLine[] HeaderLines,
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
        HashCode.Combine(HeaderStyle.FontSize, DataStyle.FontSize, SkupinaDnuHeaderLineHeight,
            MinuteFontSize, MinuteWidth, HourColumnWidth, MaxGroupCount, GroupColumns.Count),
        HashCode.Combine(TimeAxisMode, TimeAxisRangeFrom, TimeAxisRangeTo, HeaderLine2Style, AutoExpandToFillHeight),
        HashCode.Combine(DataRowHeight, AnotaceGap, OdjezdGap, GroupSeparatorWidth, HourColumnPerGroup,
            ShowSkupinaDnuHeader, SkupinaDnuId),
        GroupHeaderOverridesHash());

    /// <summary>Hash layout-affecting per-group nastavení (nadpis override, šířka override, pořadí) —
    /// mění šířku/wrap/pořadí, musí invalidovat layout cache (Count v hlavním fingerprintu to nezachytí).
    /// Order-sensitive (přeskládání skupin mění hash).</summary>
    private int GroupHeaderOverridesHash()
    {
        var h = new HashCode();
        foreach (var gc in GroupColumns)
        {
            h.Add(gc.SkupinaDnuId);
            h.Add(gc.HeaderOverride);
            h.Add(gc.WidthOverride);
        }
        return h.ToHashCode();
    }

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

        // Override nadpisů skupin ze šablony (jednotně přes ColumnHeader.Resolve s ostatními
        // tabulkovými komponentami). Aplikuje se PŘED měřením šířky a wrapem záhlaví.
        targetSkupiny = ApplyHeaderOverrides(targetSkupiny);

        // Font pro měření textu záhlaví (default, přepíše se per-column)
        float effectiveMmToPx = mmToPx > 0 ? mmToPx : 3.78f;

        // Font záhlaví je element-uniformní (typografie se per-column nepřebíjí — viz
        // WithParentHeaderTypography), proto měření i výšky řádků počítáme z element fontu.
        float defaultHeaderFontPt = HeaderStyle.FontSize ?? 9f;

        var groupLayouts = new List<GroupLayout>();
        foreach (var sk in targetSkupiny)
        {
            if (!odjezdyBySkupina.TryGetValue(sk.Id, out var byHour))
                byHour = new Dictionary<int, List<OdjezdData>>();

            string fullName = sk.Nadpis;
            if (sk.Nadpis2.Length > 0) fullName += " " + sk.Nadpis2;
            var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Element font pro měření minimální šířky
            float fontPx = defaultHeaderFontPt * PtToMm * effectiveMmToPx;
            using var measureFont = new SKFont(
                // resolved rodina + řez (vč. dokumentového defaultu) — měření musí trefit stejný font jako render
                GetTypeface(ResolveGroupHeaderParent()), fontPx);

            // Minimální šířka = nejdelší slovo záhlaví + 2mm padding
            float maxWordW = 0;
            foreach (var word in words)
            {
                float w = MeasureTextMm(measureFont, word, effectiveMmToPx);
                if (w > maxWordW) maxWordW = w;
            }
            float minTextWidth = maxWordW + 2f;

            // HeaderLines se přepočítají po redistribuci šířek (zatím placeholder)
            groupLayouts.Add(new GroupLayout(sk, byHour, 0, 0, Array.Empty<HeaderLine>(), minTextWidth));
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

        // Pevná šířka skupiny (override) přebíjí auto výpočet; pinnuté se neúčastní přerozdělení.
        var widthLookup = GroupColumnIndexResolver.BuildStyleLookup(
            GroupColumns, targetSkupiny.Select(s => s.Id).ToList());
        float? PinnedWidth(GroupLayout g) =>
            widthLookup.TryGetValue(g.Skupina.Id, out var cs) && cs.WidthOverride is float p && p > 0 ? p : null;

        // Šířky skupin: pin (override), jinak max(minuty, min. pro text záhlaví)
        var finalGroups = new List<GroupLayout>();
        foreach (var g in groupLayouts)
        {
            int maxMin = GetMaxMinutesPerHour(g.ByHour, hours);
            if (maxMin < 1) maxMin = 1;
            float effectiveSlotW = MinuteWidth + AnotaceGap;
            float minuteBasedW = maxMin * effectiveSlotW + Math.Max(0, maxMin - 1) * OdjezdGap;
            float autoW = Math.Max(minuteBasedW, g.MinWidthForText);
            float groupW = PinnedWidth(g) ?? autoW;
            finalGroups.Add(g with { MaxMinutesPerHour = maxMin, GroupWidth = groupW });
        }

        // Redistribuce extra prostoru — jen auto (nepinnuté) skupiny zaplní zbytek šířky elementu
        float separatorTotal = Math.Max(0, finalGroups.Count - 1) * GroupSeparatorWidth;
        int hourColumnCount = HourColumnPerGroup ? finalGroups.Count : 1;
        float minTotal = hourColumnCount * HourColumnWidth + finalGroups.Sum(g => g.GroupWidth) + separatorTotal;

        if (minTotal < Width)
        {
            float extraSpace = Width - minTotal;
            float sumAutoW = finalGroups.Where(g => PinnedWidth(g) is null).Sum(g => g.GroupWidth);
            if (sumAutoW > 0)
            {
                finalGroups = finalGroups.Select(g =>
                    PinnedWidth(g) is not null
                        ? g
                        : g with { GroupWidth = g.GroupWidth + extraSpace * (g.GroupWidth / sumAutoW) }
                ).ToList();
            }
        }

        // Layout řádků záhlaví:
        // - Pokud má skupina vyplněn Nadpis2 → 2 distinct řádky bez word-wrap (Line 1 + Line 2, různé styly)
        // - Jinak → word-wrap pouze Nadpis (všechny řádky jako Line 1, společný styl)
        finalGroups = finalGroups.Select(g =>
        {
            if (g.Skupina.Nadpis2.Length > 0)
            {
                // Zachovat hranici Nadpis / Nadpis2 — bez wrap (předpokládáme, že každý se vejde)
                var lines = new HeaderLine[]
                {
                    new(g.Skupina.Nadpis, IsSecondary: false),
                    new(g.Skupina.Nadpis2, IsSecondary: true),
                };
                return g with { HeaderLines = lines };
            }

            // Single-nadpis: word-wrap přes celý text, všechny řádky Line 1 (element font)
            float fontPx = defaultHeaderFontPt * PtToMm * effectiveMmToPx;
            using var wrapFont = new SKFont(
                // resolved rodina + řez (vč. dokumentového defaultu) — wrap musí trefit stejný font jako render
                GetTypeface(ResolveGroupHeaderParent()), fontPx);
            var wrapped = WrapHeaderText(g.Skupina.Nadpis, g.GroupWidth, wrapFont, effectiveMmToPx);
            return g with { HeaderLines = wrapped.Select(w => new HeaderLine(w, IsSecondary: false)).ToArray() };
        }).ToList();

        float effFontSize = DataStyle.FontSize ?? 8f;
        float effHeaderFontSize = HeaderStyle.FontSize ?? 9f;
        float fontSizeMm = effFontSize * PtToMm;
        float headerFontMm = effHeaderFontSize * PtToMm;
        float minuteFontMm = MinuteFontSize * PtToMm;
        float rowH = DataRowHeight ?? ComputeRowH(effFontSize);
        float headerH = ComputeHeaderH(effHeaderFontSize);

        // Výška řádku záhlaví — Line 1 (element font), Line 2 (HeaderLine2Style, element-level).
        // Per-group celková výška = součet výšek jeho řádků (různé výšky pro Line 1 a Line 2).
        float line2FontPt = HeaderLine2Style?.FontSize ?? defaultHeaderFontPt;
        float line2LineH = Math.Max(SkupinaDnuHeaderLineHeight, line2FontPt * PtToMm * 1.5f);

        float line1LineH = Math.Max(SkupinaDnuHeaderLineHeight, defaultHeaderFontPt * PtToMm * 1.5f);
        float maxGroupBlockH = 0f;
        foreach (var g in finalGroups)
        {

            float blockH = 0f;
            foreach (var hl in g.HeaderLines)
                blockH += hl.IsSecondary ? line2LineH : line1LineH;
            if (blockH > maxGroupBlockH) maxGroupBlockH = blockH;
        }

        int maxHeaderLines = finalGroups.Count == 0 ? 0 : finalGroups.Max(g => g.HeaderLines.Length);
        float groupHeaderH = ShowSkupinaDnuHeader
            ? maxGroupBlockH
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

    /// <summary>
    /// Typografie záhlaví odjezdů je element-uniformní — per-column sloupce nesou jen barvy
    /// a zarovnání (per-skupina barva), ne font/velikost/řez. Vrátí resolved styl, kde
    /// typografická pole jsou vždy z element-level rodiče. Tím element-level ovladače (velikost,
    /// tučné, …) vždy fungují, i když má sloupec zapečený starý per-column typografický override.
    /// </summary>
    private static ResolvedTextStyle WithParentHeaderTypography(ResolvedTextStyle resolved, ResolvedTextStyle parent) =>
        resolved with
        {
            FontFamily    = parent.FontFamily,
            FontSize      = parent.FontSize,
            Bold          = parent.Bold,
            Italic        = parent.Italic,
            Underline     = parent.Underline,
            Strikethrough = parent.Strikethrough,
        };

    /// <summary>Vyřeší styl záhlaví sloupce hodin (typografie element-uniformní).</summary>
    private ResolvedTextStyle ResolveHourHeaderStyle()
    {
        var parent = ResolveHourHeaderParent();
        var resolved = HourColumnStyle?.Header is { } h ? h.Resolve(parent) : parent;
        return WithParentHeaderTypography(resolved, parent);
    }

    /// <summary>Vyřeší styl datové buňky sloupce hodin.</summary>
    private ResolvedTextStyle ResolveHourDataStyle()
    {
        var parent = ResolveHourDataParent();
        return HourColumnStyle?.Data is { } d ? d.Resolve(parent) : parent;
    }

    /// <summary>Vyřeší styl záhlaví skupiny dnů (typografie element-uniformní, per-column jen barvy/zarovnání).</summary>
    private ResolvedTextStyle ResolveGroupHeaderStyle(DepartureGroupColumnStyle? cs)
    {
        var parent = ResolveGroupHeaderParent();
        var resolved = cs?.Style.Header is { } h ? h.Resolve(parent) : parent;
        return WithParentHeaderTypography(resolved, parent);
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

    /// <summary>Jednoznakový speciální symbol (piktogram) → kreslí se symbolovým JŘ fontem.
    /// Víceznakové SpecialniZnak (dny „Po"…) i NZ (čísla) zůstávají v normálním fontu.</summary>
    private static bool JeGlyfSymbol(AnotaceData a) =>
        a.Typ == TypAnotace.SpecialniZnak && a.Znak.Length == 1 && !SymbolJeText(a.Znak);

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
            // Placeholder se ukazuje jen v designeru. Print s prázdnými daty nesmí
            // vykreslovat fake mřížku.
            if (ctx.Data is null) RenderPlaceholder(ctx);
            return;
        }

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit vše proporcionálně
        // Záhlaví = jeden sloučený řádek (Hod + názvy skupin)
        float combinedHeaderH = ShowSkupinaDnuHeader
            ? Math.Max(layout.GroupHeaderH, layout.HeaderH)
            : layout.HeaderH;
        // B2: přirozená výška ze skutečné výšky řádku (vč. explicitní DataRowHeight) — shodně
        // s CalculateHeightFromData/ContentOverflows, aby varování a auto-scale nepočítaly jinak.
        // Auto-shrink do fit je konzistentní pattern všech JŘ elementů → velká DataRowHeight se
        // zmenší jako vše ostatní (dřív se tu z ní naopak nezmenšovaly fonty → obsah tiše přetekl).
        float scaleNaturalH = combinedHeaderH + layout.Hours.Count * layout.RowH;
        float scale = ComputeFontScale(scaleNaturalH);

        // Roztáhnout na výšku — pokud obsah je kratší než Height a feature je zapnutá,
        // proporcionálně zvětšit data řádky + jejich fonty (header zůstává).
        // Caché parametr ROZTAHNOUT_NA_VYSKU může override z PrintDialogu.
        bool effectiveAutoExpand = GetBoolParam(ctx.Data, "ROZTAHNOUT_NA_VYSKU", AutoExpandToFillHeight);
        float dataGrow = 1f;
        if (effectiveAutoExpand && Height.HasValue && scale >= 1f && layout.Hours.Count > 0)
        {
            float scaledCombinedH0 = combinedHeaderH;   // header při scale=1
            float dataNaturalH = layout.Hours.Count * layout.RowH;
            float availableDataH = Height.Value - scaledCombinedH0;
            if (dataNaturalH > 0 && availableDataH > dataNaturalH)
                dataGrow = availableDataH / dataNaturalH;
            // Cap — řídká mřížka (např. jen 1 hodina s odjezdy) by se jinak roztáhla bizarně velká.
            // Vyplníme jen rozumně; zbytek místa zůstane prázdný místo obřích řádků.
            if (dataGrow > MaxAutoExpandFactor) dataGrow = MaxAutoExpandFactor;
        }
        float dataScale = scale * dataGrow;

        // B2/TR4: výška řádku škáluje konzistentně (shrink i grow) — i explicitní DataRowHeight se zmenší,
        // když se obsah nevejde. Sdílený helper `ScaledDataRowHeight` = jediné místo pravidla (viz StopList).
        float scaledRowH = ScaledDataRowHeight(DataStyle.FontSize ?? 8f, dataScale);
        float scaledGroupHeaderH = layout.GroupHeaderH * scale;
        float scaledCombinedHeaderH = combinedHeaderH * scale;

        // Clip na hranice elementu
        float clipH = scaledCombinedHeaderH + layout.Hours.Count * scaledRowH;
        var clipRect = ctx.ToCanvasRect(X, Y, Width, clipH);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);

        using var paints = CreateStandardPaints(ctx);

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
        // Symbolový JŘ font (piktogramy) pro jednoznakové SpecialniZnak — dle jazyka šablony.
        // Uživatelský font v AnotaceSymbolStyle (pokud zvolen) má přednost; jinak embedded TimeTable2.
        var anotGlyphStyle = AnotaceSymbolStyle.FontFamily is null
            ? anotSymStyle with { FontFamily = FontResolver.SymbolFontFamily(ctx.Data?.Culture) }
            : anotSymStyle;

        // ── Deklarace zdrojů (perf: 100+ buněk/frame) — plnění až uvnitř try ────
        // Deklarace musí být před try, aby na ně viděl finally i lokální helpery níže;
        // samotná alokace fontů/paintů (a SKColor.Parse nad uživatelskými barvami, které
        // mohou vyhodit) je uvnitř try, aby finally uvolnil i částečně vytvořené zdroje + clip.
        var groupHeaderFonts       = new Dictionary<string, SKFont>();
        var groupHeaderTextPaints  = new Dictionary<string, SKPaint>();
        var groupHeaderBgPaints    = new Dictionary<string, SKPaint?>();
        var groupMinuteFonts       = new Dictionary<string, SKFont>();
        var groupMinuteTextPaints  = new Dictionary<string, SKPaint>();
        var groupMinuteBgPaints    = new Dictionary<string, SKPaint?>();

        // Line 2 záhlaví — vytváří se pouze pokud HeaderLine2Style != null (jinak Line 2 sdílí Line 1).
        bool hasLine2Override = HeaderLine2Style is not null;
        var groupHeaderStyles2     = new Dictionary<string, ResolvedTextStyle>();
        var groupHeaderFonts2      = new Dictionary<string, SKFont>();
        var groupHeaderTextPaints2 = new Dictionary<string, SKPaint>();

        // Anotace fonty/painty — element-level, sdílené pro všechny buňky (nullable = alokace v try).
        SKFont?  anotSymFont   = null;
        SKFont?  anotGlyphFont = null;
        SKPaint? anotSymPaint  = null;
        SKFont?  anotTextFont  = null;
        SKPaint? anotTextPaint = null;

        // Zebra — globální + per-group override (alokace v try kvůli SKColor.Parse).
        SKPaint? zebraPaint = null;
        var perGroupZebraPaints = new Dictionary<string, SKPaint>();
        bool hasPerGroupZebra = false;

        float anotGapPx      = AnotaceGap * dataScale * ctx.MmToPx;
        float anotInterGapPx = AnotaceInterGap * dataScale * ctx.MmToPx;
        float scaledOdjezdGap = OdjezdGap * dataScale; // v mm (model space), ne v px
        float usedWidth = Width;
        float currentY = Y;
        float scaledMinuteW = (MinuteWidth + AnotaceGap) * dataScale;

        try
        {
            foreach (var g in layout.Groups)
            {
                var id = g.Skupina.Id;
                var hs = groupHeaderStyles[id];
                var ms = groupMinuteStyles[id];

                // Header fonty: použít base scale (růst se neaplikuje na header).
                groupHeaderFonts[id]      = hs.CreateFont(scale, ctx.MmToPx);
                groupHeaderTextPaints[id] = hs.CreateTextPaint();
                groupHeaderBgPaints[id]   = hs.CreateBgPaint();

                if (hasLine2Override)
                {
                    var hs2 = HeaderLine2Style!.Resolve(hs);
                    groupHeaderStyles2[id]     = hs2;
                    groupHeaderFonts2[id]      = hs2.CreateFont(scale, ctx.MmToPx);
                    groupHeaderTextPaints2[id] = hs2.CreateTextPaint();
                }

                // Minutové fonty: použít dataScale (růst se aplikuje, header ne).
                groupMinuteFonts[id]      = ms.CreateFont(dataScale, ctx.MmToPx);
                groupMinuteTextPaints[id] = ms.CreateTextPaint();
                groupMinuteBgPaints[id]   = ms.CreateBgPaint();
            }

            // Anotace patří k datovým buňkám → dataScale.
            anotSymFont   = anotSymStyle.CreateFont(dataScale, ctx.MmToPx);
            anotGlyphFont = anotGlyphStyle.CreateFont(dataScale, ctx.MmToPx);
            anotSymPaint  = anotSymStyle.CreateTextPaint();
            anotTextFont  = anotTextStyle.CreateFont(dataScale, ctx.MmToPx);
            anotTextPaint = anotTextStyle.CreateTextPaint();

            if (AlternatingRowColor is not null)
                zebraPaint = new SKPaint { Color = SKColor.Parse(AlternatingRowColor) };

            foreach (var g in layout.Groups)
            {
                if (colStyles.TryGetValue(g.Skupina.Id, out var cs) && cs.AlternatingRowColor is not null)
                    perGroupZebraPaints[g.Skupina.Id] = new SKPaint { Color = SKColor.Parse(cs.AlternatingRowColor) };
            }
            hasPerGroupZebra = perGroupZebraPaints.Count > 0;

            RenderGridHeader();
            RenderGridRows();
            RenderGridVerticalSeparators();
            // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
        }
        finally
        {
            foreach (var f in groupHeaderFonts.Values)      f.Dispose();
            foreach (var p in groupHeaderTextPaints.Values) p.Dispose();
            foreach (var p in groupHeaderBgPaints.Values)   p?.Dispose();
            foreach (var f in groupHeaderFonts2.Values)     f.Dispose();
            foreach (var p in groupHeaderTextPaints2.Values) p.Dispose();
            foreach (var f in groupMinuteFonts.Values)      f.Dispose();
            foreach (var p in groupMinuteTextPaints.Values) p.Dispose();
            foreach (var p in groupMinuteBgPaints.Values)   p?.Dispose();
            anotSymFont?.Dispose();   anotGlyphFont?.Dispose();   anotSymPaint?.Dispose();
            anotTextFont?.Dispose();  anotTextPaint?.Dispose();
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
            CellRenderer.DrawCell(ctx, rect, Rendering.ColumnHeader.Resolve(HourColumnHeader, "Hod"), style, scale);
        }

        // Nakreslí datovou buňku sloupce hodin přes CellRenderer.
        // Hour je „service hour" (raw z dat, 0..47). Header zobrazuje clock hour = H % 24 —
        // night extension (24..) se uživateli ukáže jako 0..3 podle hodin zobrazované doby.
        // Datová buňka → dataScale (růst při AutoExpandToFillHeight).
        void DrawHourDataCell(float hx, int hour, string? bgColorOverride = null)
        {
            var style = bgColorOverride is not null
                ? hourDataStyle with { BackgroundColor = bgColorOverride }
                : hourDataStyle;
            var rect = new SKRect(hx, currentY, hx + HourColumnWidth, currentY + scaledRowH);
            CellRenderer.DrawCell(ctx, rect, (hour % 24).ToString(), style, dataScale);
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

                    // Per-line font/paint: Line 1 = HeaderStyle (hs), Line 2 (IsSecondary) = HeaderLine2Style cascade.
                    // Pokud HeaderLine2Style == null, IsSecondary řádky sdílí Line 1 styl (žádná regrese).
                    ResolvedTextStyle hs2 = hasLine2Override ? groupHeaderStyles2[gid] : hs;
                    SKFont hFont2 = hasLine2Override ? groupHeaderFonts2[gid] : hFont;
                    SKPaint hTextPaint2 = hasLine2Override ? groupHeaderTextPaints2[gid] : hTextPaint;

                    // Per-line výška řádku — Line 1 z hs, Line 2 z hs2 (pokud override).
                    float line1H = hs.FontSize  * PtToMm * 1.5f * scale;
                    float line2H = hs2.FontSize * PtToMm * 1.5f * scale;

                    int lineCount = g.HeaderLines.Length;
                    float blockH = 0f;
                    foreach (var hl in g.HeaderLines) blockH += hl.IsSecondary ? line2H : line1H;

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

                    float runningY = startLineY;
                    for (int i = 0; i < lineCount; i++)
                    {
                        var hl = g.HeaderLines[i];
                        var lineStyle = hl.IsSecondary ? hs2 : hs;
                        var lineFont  = hl.IsSecondary ? hFont2 : hFont;
                        var linePaint = hl.IsSecondary ? hTextPaint2 : hTextPaint;
                        float lineH   = hl.IsSecondary ? line2H : line1H;

                        float baselineY = runningY + lineH * 0.8f;
                        float textW = MeasureTextMm(lineFont, hl.Text, ctx.MmToPx);
                        float textX = lineStyle.HAlign switch
                        {
                            TextHorizontalAlignment.Left  => gx + hPadLeft + 1f,
                            TextHorizontalAlignment.Right => gx + g.GroupWidth - textW - hPadRight - 1f,
                            _ /* Center */                => gx + hPadLeft + (g.GroupWidth - hPadLeft - hPadRight - textW) / 2f
                        };
                        var pt = ctx.ToCanvas(textX, baselineY);
                        ctx.Canvas.DrawText(hl.Text, pt.X, pt.Y, lineFont, linePaint);
                        lineStyle.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, lineFont, linePaint);

                        runningY += lineH;
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

            // Padding minutové buňky — dataScale (s růstem AutoExpandu).
            float padL = ms.PaddingLeft * dataScale;
            float padR = ms.PaddingRight * dataScale;
            float padT = ms.PaddingTop * dataScale;

            // Adaptivní šířka slotu každého odjezdu: nominální (scaledMinuteW) + šířka anotací,
            // aby se minuta s poznámkami nepřekrývala s další minutou (zachová nominální mezeru).
            float availW = g.GroupWidth - padL - padR;

            float SlotWidthMm(OdjezdData o)
            {
                float extraPx = 0f;
                if (ShowAnotace && o.Anotace.Count > 0)
                {
                    extraPx = anotGapPx;
                    foreach (var a in o.Anotace)
                    {
                        SKFont af = JeGlyfSymbol(a) ? anotGlyphFont! : (a.Typ == TypAnotace.Text ? anotTextFont! : anotSymFont!);
                        bool nzBox = a.Typ == TypAnotace.NegativniZnacka;
                        extraPx += af.MeasureText(a.Znak) + (nzBox ? af.Size * 0.12f * 2f : 0f) + anotInterGapPx;
                    }
                }
                return scaledMinuteW + extraPx / ctx.MmToPx;
            }

            var slotW = odjezdy.Select(SlotWidthMm).ToList();
            float blockW = slotW.Sum() + Math.Max(0, odjezdy.Count - 1) * scaledOdjezdGap;
            float alignOffset = padL + ms.HAlign switch
            {
                TextHorizontalAlignment.Center => (availW - blockW) / 2f,
                TextHorizontalAlignment.Right  => availW - blockW - 0.5f,
                _ => 0f
            };
            if (alignOffset < padL) alignOffset = padL;

            float minX = gx + alignOffset;
            SKPaint? customMinutePaint = null;
            SKPaint? customAnotPaint = null;
            SKPaint? nzBgPaint = null;
            SKPaint? nzFgPaint = null;
            try
            {
                for (int oi = 0; oi < odjezdy.Count; oi++)
                {
                    var odj = odjezdy[oi];
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
                            SKFont aFont = JeGlyfSymbol(anot) ? anotGlyphFont! : (anot.Typ == TypAnotace.Text ? anotTextFont! : anotSymFont!);

                            // Negativní značka invertovaně — černý box + bílý znak (jako legacy). Vždy zapnuto.
                            if (anot.Typ == TypAnotace.NegativniZnacka)
                            {
                                nzBgPaint ??= new SKPaint { Color = SKColor.Parse(NegativniZnackaBackColor), IsAntialias = true };
                                nzFgPaint ??= new SKPaint { Color = SKColor.Parse(NegativniZnackaForeColor), IsAntialias = true };
                                float hpad  = aFont.Size * 0.12f;
                                float znakW = aFont.MeasureText(anot.Znak);
                                var m = aFont.Metrics;
                                ctx.Canvas.DrawRect(new SKRect(anotX, minPt.Y + m.Ascent, anotX + znakW + hpad * 2, minPt.Y + m.Descent), nzBgPaint);
                                ctx.Canvas.DrawText(anot.Znak, anotX + hpad, minPt.Y, aFont, nzFgPaint);
                                anotX += znakW + hpad * 2 + anotInterGapPx;
                                continue;
                            }

                            SKPaint aPaint = anot.Typ == TypAnotace.Text ? anotTextPaint! : anotSymPaint!;

                            // Per-anotace barva z DB (konzistence s legendou poznámek).
                            if (anot.ForeColor is not null)
                            {
                                customAnotPaint ??= new SKPaint { IsAntialias = true };
                                customAnotPaint.Color = SKColor.Parse(anot.ForeColor);
                                aPaint = customAnotPaint;
                            }

                            ctx.Canvas.DrawText(anot.Znak, anotX, minPt.Y, aFont, aPaint);
                            anotX += aFont.MeasureText(anot.Znak) + anotInterGapPx;
                        }
                    }

                    minX += slotW[oi] + scaledOdjezdGap;
                }
            }
            finally
            {
                customMinutePaint?.Dispose();
                customAnotPaint?.Dispose();
                nzBgPaint?.Dispose();
                nzFgPaint?.Dispose();
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

        using var paints = CreateStandardPaints(ctx);

        float fontPx = effFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = effHeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(GetTypeface(dataResolved), fontPx);
        using var headerFont = new SKFont(GetTypeface(headerResolved), headerFontPx);

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

        // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public override float CalculateHeightFromData(DataContext? data)
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

        // GroupColumns má přednost — filtruje podle IsVisible a určuje pořadí (přeskládání ▲▼ v editoru).
        if (GroupColumns.Count > 0)
        {
            var hidden = GroupColumns
                .Where(gc => !gc.IsVisible)
                .Select(gc => gc.SkupinaDnuId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Pořadí dle GroupColumns; skupiny mimo seznam zachovají datové pořadí na konci.
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < GroupColumns.Count; i++)
                if (!order.ContainsKey(GroupColumns[i].SkupinaDnuId))
                    order[GroupColumns[i].SkupinaDnuId] = i;

            var result = all
                .Where(s => !hidden.Contains(s.Id))
                .Select((s, dataIdx) => (s, key: order.TryGetValue(s.Id, out var oi) ? oi : int.MaxValue, dataIdx))
                .OrderBy(t => t.key).ThenBy(t => t.dataIdx)
                .Select(t => t.s)
                .ToList();
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

    /// <summary>Aplikuje per-group override nadpisu (<see cref="DepartureGroupColumnStyle.HeaderOverride"/>)
    /// přes sdílený <see cref="Rendering.ColumnHeader.Resolve"/>. Když je override neprázdný, nahradí
    /// celý nadpis skupiny (jeden text — dvouřádek z dat se použije jen bez override). Vrací stejný
    /// seznam pokud žádný override není (žádná alokace navíc).</summary>
    private List<SkupinaDnuData> ApplyHeaderOverrides(List<SkupinaDnuData> skupiny)
    {
        if (GroupColumns.Count == 0) return skupiny;
        var lookup = GroupColumnIndexResolver.BuildStyleLookup(
            GroupColumns, skupiny.Select(s => s.Id).ToList());
        if (lookup.Count == 0) return skupiny;

        List<SkupinaDnuData>? result = null;
        for (int i = 0; i < skupiny.Count; i++)
        {
            var sk = skupiny[i];
            lookup.TryGetValue(sk.Id, out var cs);
            var effective = Rendering.ColumnHeader.Resolve(cs?.HeaderOverride, sk.Nadpis);
            if (ReferenceEquals(effective, sk.Nadpis)) continue;   // bez override

            result ??= new List<SkupinaDnuData>(skupiny);
            result[i] = new SkupinaDnuData { Id = sk.Id, Nadpis = effective, Nadpis2 = "", Poradi = sk.Poradi };
        }
        return result ?? skupiny;
    }

    /// <summary>Vrátí dostupné skupiny dnů z naposledy zpracovaných dat (pro dialog).</summary>
    public List<SkupinaDnuData> GetAvailableSkupinyDnu(DataContext? data) => BuildSkupinyDnu(data);

    private static List<int> GetVisibleHoursFromSet(
        HashSet<int> allHours, TimeAxisMode timeAxis, int rangeFrom, int rangeTo)
    {
        var hours = timeAxis switch
        {
            TimeAxisMode.Compact     => allHours.OrderBy(h => h).ToList(),
            TimeAxisMode.DataBounded => DataBoundedHours(allHours),
            TimeAxisMode.Range       => RangeHoursExtended(rangeFrom, rangeTo),
            TimeAxisMode.Full        => Enumerable.Range(0, 24).ToList(),
            _ => allHours.OrderBy(h => h).ToList()
        };

        // HODINY_OD/DO filtr se aplikuje i mimo Range mode (zúžení Compact/DataBounded/Full).
        // V Range mode už jsou hodiny generované přesně z rangeFrom..rangeTo, filtr přeskočit.
        if (timeAxis != TimeAxisMode.Range && (rangeFrom > 0 || rangeTo < 23))
        {
            // Pokud to < from → wrap přes půlnoc: zahrnuje extended hours (24..to+24).
            // Data hour 27 (= 03:00 následujícího provozního dne) projde filtrem 22→3 jako 27 ∈ [22..27].
            int hi = rangeTo >= rangeFrom ? rangeTo : rangeTo + 24;
            hours = hours.Where(h => h >= rangeFrom && h <= hi).ToList();
        }

        return hours;
    }

    /// <summary>Souvislé hodiny od min(data) do max(data) — včetně prázdných v rozsahu.
    /// Data s extended hours (24..47) přirozeně rozšíří horní mez.</summary>
    private static List<int> DataBoundedHours(HashSet<int> allHours)
    {
        if (allHours.Count == 0) return new List<int>();
        int min = allHours.Min();
        int max = allHours.Max();
        return Enumerable.Range(min, max - min + 1).ToList();
    }

    /// <summary>Service hours [from..to]. Pokud to &lt; from, wrap přes půlnoc se reprezentuje
    /// jako extended hours: [from..23] + [24..to+24]. Tj. 22→3 = [22, 23, 24, 25, 26, 27].
    /// Tyto hodnoty se na header rendrují jako H%24 (např. 27 = „3"), ale v ByHour lookupu zůstávají
    /// raw — Caché dataset s konvencí extended hours se napáruje 1:1.</summary>
    private static List<int> RangeHoursExtended(int from, int to)
    {
        from = Math.Clamp(from, 0, 23);
        to   = Math.Clamp(to, 0, 23);
        if (to >= from)
            return Enumerable.Range(from, to - from + 1).ToList();
        // Wrap → extended: 22-5 → [22, 23, 24, 25, 26, 27, 28, 29]
        // (data lookup byHour[24] → odjezdy v 0:xx následujícího provozního dne)
        return Enumerable.Range(from, 24 - from)
            .Concat(Enumerable.Range(24, to + 1))
            .ToList();
    }

    private static TimeAxisMode ParseTimeAxisMode(string s) => s.ToLowerInvariant() switch
    {
        // Nové popisné labely (varianta A — z Caché AddStandardJRParameters)
        "jen hodiny s odjezdy"          => TimeAxisMode.Compact,
        "souvislá od první do poslední"
            or "souvisla od prvni do posledni" => TimeAxisMode.DataBounded,
        "vlastní rozsah" or "vlastni rozsah"   => TimeAxisMode.Range,
        // Staré české labely (backward compat — předchozí iterace tohoto enumu)
        "souvislá bez mezer"  or "souvisla bez mezer"  => TimeAxisMode.Compact,
        "souvislá s mezerami" or "souvisla s mezerami" => TimeAxisMode.DataBounded,
        "daná intervalem"     or "dana intervalem"     => TimeAxisMode.Range,
        "plný den"            or "plny den"            => TimeAxisMode.Full,
        // Anglické hodnoty (backward compat pro nejstarší šablony / přímé enum názvy)
        "compact"     => TimeAxisMode.Compact,
        "databounded" => TimeAxisMode.DataBounded,
        "range"       => TimeAxisMode.Range,
        "full"        => TimeAxisMode.Full,
        _             => TimeAxisMode.Compact
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

    /// <summary>Lazy-init HeaderLine2Style při prvním nastavení libovolné jeho hodnoty z UI.</summary>
    private TextStyle EnsureHeaderLine2Style() => HeaderLine2Style ??= new TextStyle();

    /// <summary>Nastaví element-level velikost písma záhlaví. Typografie záhlaví je element-uniformní
    /// (per-column se ignoruje v ResolveGroupHeaderStyle/ResolveHourHeaderStyle), takže stačí set + invalidace cache.</summary>
    private void SetHeaderFontSize(float v)
    {
        HeaderStyle.FontSize = v;
        _cachedLayout = null;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        // ── Výchozí styl (základ pro dědění — per-column nastavení ho přepisuje) ──
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Odjezdy", ButtonLabel = "Upravit sloupce…", ActionId = ButtonPropertyDescriptor.ActionOpenDepartureGridEditor },

        new BoolPropertyDescriptor  { Label = "Záhlaví skupin",     Group = "Odjezdy", Get = () => ShowSkupinaDnuHeader,       Set = v => ShowSkupinaDnuHeader = v },
        // Velikost písma záhlaví — přímý ovladač HeaderStyle.FontSize. Bez něj šlo měnit jen
        // „Výška řádku záhl." (mm), která ale text zvětší jen dokud je FontSize null (legacy derive).
        new FloatPropertyDescriptor { Label = "Velikost záhlaví",  Group = "Odjezdy", Get = () => HeaderStyle.FontSize ?? 9f,  Set = SetHeaderFontSize, Min = 3, Max = 24, Unit = "pt" },
        new FloatPropertyDescriptor { Label = "Min. výška řádku",  Group = "Odjezdy", Get = () => SkupinaDnuHeaderLineHeight,  Set = v => SkupinaDnuHeaderLineHeight = v, Min = 2, Max = 10, Unit = "mm" },
        new EnumPropertyDescriptor  { Label = "Zarovnání záhlaví", Group = "Odjezdy",
            Get = () => HeaderVerticalAlignment, Set = v => HeaderVerticalAlignment = (TextVerticalAlignment)v,
            Options = EnumPropertyDescriptor.VAlignOptions },
        new FloatPropertyDescriptor { Label = "Šířka sl. hodin",    Group = "Odjezdy", Get = () => HourColumnWidth,            Set = v => HourColumnWidth = v, Min = 4, Max = 20, Unit = "mm" },

        // Časová osa (rozsah hodin) je parametr šablony „Časová osa odjezdů" (CASOVA_OSA/HODINY_OD/DO),
        // sdílený a autoritativní — NEvystavovat jako property elementu (parametr ho přebíjí → matoucí).
        // Property TimeAxisMode/RangeFrom/RangeTo zůstávají jen jako render fallback, když param chybí.

        // Když je kratší rozsah hodin než kapacita Height, vyplnit zbylé místo (jen data řádky).
        // Caché parametr ROZTAHNOUT_NA_VYSKU může override z PrintDialogu.
        new BoolPropertyDescriptor
        {
            Label = "Roztáhnout na výšku", Group = "Odjezdy",
            Get = () => AutoExpandToFillHeight, Set = v => AutoExpandToFillHeight = v
        },

        new BoolPropertyDescriptor  { Label = "Hodiny u každé sk.",  Group = "Odjezdy", Get = () => HourColumnPerGroup,        Set = v => HourColumnPerGroup = v },
        new FloatPropertyDescriptor { Label = "Max skupin",          Group = "Odjezdy", Get = () => MaxGroupCount,             Set = v => MaxGroupCount = (int)v, Min = 0, Max = 10, Format = "0", Unit = "(0=∞)" },
        new FloatPropertyDescriptor { Label = "Šířka minuty",       Group = "Odjezdy", Get = () => MinuteWidth,                Set = v => MinuteWidth = v, Min = 3, Max = 15, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Oddělovač skupin",   Group = "Odjezdy", Get = () => GroupSeparatorWidth,        Set = v => GroupSeparatorWidth = v, Min = 0, Max = 5, Unit = "mm" },

        GetDataRowHeightDescriptor("Řádky dat"),
        new ColorPropertyDescriptor { Label = "Střídání řádků", Group = "Řádky dat", Get = () => AlternatingRowColor ?? "", Set = v => AlternatingRowColor = string.IsNullOrEmpty(v) ? null : v },

        // ── Styl 2. řádku záhlaví skupin dnů (např. "den" v "Pracovní / den") ──
        // Aplikuje se POUZE pokud SkupinaDnu.Nadpis2 je vyplněn. Null = stejné jako Line 1 (HeaderStyle).
        new FloatPropertyDescriptor  { Label = "Velikost",     Group = "Záhlaví — 2. řádek", Get = () => HeaderLine2Style?.FontSize ?? HeaderStyle.FontSize ?? 9f, Set = v => EnsureHeaderLine2Style().FontSize = v, Min = 3, Max = 24, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Záhlaví — 2. řádek", Get = () => HeaderLine2Style?.Bold ?? HeaderStyle.Bold ?? true, Set = v => EnsureHeaderLine2Style().Bold = v },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Záhlaví — 2. řádek", Get = () => HeaderLine2Style?.Italic ?? HeaderStyle.Italic ?? false, Set = v => EnsureHeaderLine2Style().Italic = v ? true : null },
        new ColorPropertyDescriptor  { Label = "Barva textu",  Group = "Záhlaví — 2. řádek", Get = () => HeaderLine2Style?.TextColor ?? HeaderStyle.TextColor ?? "", Set = v => EnsureHeaderLine2Style().TextColor = string.IsNullOrEmpty(v) ? null : v },

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
        new ColorPropertyDescriptor  { Label = "NZ pozadí",     Group = "Anotace — symboly", Get = () => NegativniZnackaBackColor, Set = v => NegativniZnackaBackColor = v },
        new ColorPropertyDescriptor  { Label = "NZ znak",       Group = "Anotace — symboly", Get = () => NegativniZnackaForeColor, Set = v => NegativniZnackaForeColor = v },

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
            HeaderLine2Style           = HeaderLine2Style?.Clone(),
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
            MinuteBold                 = MinuteBold,
            AlternatingRowColor        = AlternatingRowColor,
            TimeAxisMode               = TimeAxisMode,
            TimeAxisRangeFrom          = TimeAxisRangeFrom,
            TimeAxisRangeTo            = TimeAxisRangeTo,
            AutoExpandToFillHeight     = AutoExpandToFillHeight,
            ShowAnotace                = ShowAnotace,
            AnotaceFontSize            = AnotaceFontSize,
            NegativniZnackaBackColor   = NegativniZnackaBackColor,
            NegativniZnackaForeColor   = NegativniZnackaForeColor,
            HourColumnStyle            = HourColumnStyle?.Clone(),
            HourColumnHeader           = HourColumnHeader,
            GroupColumns               = GroupColumns.Select(gc => gc.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyStopScheduleProperties(clone);
        return clone;
    }
}
