using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Tabulka zastávek (levý sloupec zastávkového JŘ).
/// Zobrazuje: TČ, název zastávky, čas jízdy, zóna, průběhy, atributy, poznámky.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// Uživatel umístí na canvas vedle DepartureGridElement(ů).
/// </summary>
public class StopListElement : ScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Sloupce — konfigurovatelné pořadí, šířky, záhlaví
    // ═══════════════════════════════════════════
    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = "TravelTime",   Header = "Čas",       Width = 8f,  Visible = true },
        new() { Key = "Tc",           Header = "TČ",       Width = 8f,  Visible = false },
        new() { Key = "StopName",     Header = "Zastávka",  Width = 50f, Visible = true },
        new() { Key = "Zone",         Header = "Zóna",      Width = 12f, Visible = false },
        new() { Key = "RouteLetters", Header = "Trasa",     Width = 8f,  Visible = false },
        new() { Key = "BaseRoute",    Header = "ZT",        Width = 6f,  Visible = false },
    ];

    // ═══════════════════════════════════════════
    // Řádky (DataRowHeight je na ScheduleElementBase)
    // ═══════════════════════════════════════════
    public bool ShowRowSeparators { get; set; } = true;

    // ═══════════════════════════════════════════
    // Zobrazení zastávek (nesouvisí se sloupci)
    // ═══════════════════════════════════════════
    public bool ShowStopIndentation { get; set; } = true;
    public bool ShowSignalStopIndicator { get; set; } = true;
    public string SignalStopSymbol { get; set; } = "×";
    public bool ShowStopAttributes { get; set; } = true;
    public bool ShowStopNotes { get; set; } = true;

    // ═══════════════════════════════════════════
    // Šipka směru jízdy (┌─▼ od aktuální zastávky dolů)
    // ═══════════════════════════════════════════
    public bool ShowDirectionArrow { get; set; } = true;
    public string DirectionArrowColor { get; set; } = "#FF000000";
    public bool DirectionArrowBold { get; set; }
    public DirectionArrowSide DirectionArrowPosition { get; set; } = DirectionArrowSide.Right;

    // ═══════════════════════════════════════════
    // Záhlaví sloupců
    // ═══════════════════════════════════════════
    public bool ShowHeader { get; set; } = true;

    // ═══════════════════════════════════════════
    // Zvýraznění aktuální zastávky
    // Highlight se aktivuje automaticky, když je nastavená alespoň jedna z těchto vlastností.
    // ═══════════════════════════════════════════
    public bool CurrentStopUnderline { get; set; } = true;
    public bool CurrentStopBold { get; set; } = true;
    public float? CurrentStopFontSize { get; set; }
    public string? CurrentStopBackgroundColor { get; set; }

    // ═══════════════════════════════════════════
    // Projeté zastávky
    // ═══════════════════════════════════════════
    public bool ShowPassedStops { get; set; }
    public string PassedStopsForeColor { get; set; } = "#FF999999";

    // ═══════════════════════════════════════════
    // Vyloučené atributy
    // ═══════════════════════════════════════════
    public List<string> ExcludedAttributeIds { get; set; } = [];

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 60f;

    // ─── Render ─────────────────────────────────────────────────────────────

    /// <summary>Odsazení vertikální čáry šipky od pravého okraje sloupce zastávek (mm) — pro Vpravo.</summary>
    private const float ArrowInsetFromEdgeMm = 0.6f;

    /// <summary>Délka horizontálního bracketu šipky pro Vpravo (mm) — bracket míří doleva do sloupce.</summary>
    private const float ArrowBracketLengthMm = 1.5f;

    /// <summary>Odsazení vertikální čáry šipky doleva mimo sloupec zastávek (mm) — pro Vlevo.</summary>
    /// <remarks>Šipka pro Vlevo jde mimo sloupec, aby se nepřekrývala s left-aligned textem. Bracket pak vede k levé hraně sloupce.</remarks>
    private const float ArrowOutsideOffsetMm = 1.5f;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var zastavky = BuildZastavky(ctx.Data);
        bool hasData = zastavky.Count > 0;

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit font
        float scale = hasData ? ComputeFontScale(CalculateHeightFromData(ctx.Data)) : 1f;
        float baseFontSize = DataStyle.FontSize ?? 8f;
        float baseHeaderFontSize = HeaderStyle.FontSize ?? 9f;
        float effectiveFontSize = baseFontSize * scale;
        float effectiveHeaderFontSize = baseHeaderFontSize * scale;

        float rowH = DataRowHeight ?? ComputeRowH(effectiveFontSize);
        float headerH = ComputeHeaderH(effectiveHeaderFontSize);

        using var paints = CreateStandardPaints(ctx);

        var columns = GetVisibleColumns();

        // Parametry z tiskového dialogu přepíší element properties
        bool showPassed = GetBoolParam(ctx.Data, "TISK_PROJETYCH_ZAST", ShowPassedStops);
        bool showArrow = GetBoolParam(ctx.Data, "TISK_SIPKY", ShowDirectionArrow);
        bool arrowLeft = showArrow && DirectionArrowPosition == DirectionArrowSide.Left;

        // Šipka se vykresluje UVNITŘ sloupce zastávek (Key = "StopName") — vertikální čára je
        // kousek od vnitřního okraje sloupce, bracket horizontálně směřuje do sloupce.
        // Nepřičítá se nic k šířce layoutu → element a sloupce zachovávají Width.
        // Pro běžně krátké názvy zastávek se text se šipkou nepřekrývá.
        // Fallback (žádný StopName sloupec): poslední sloupec.
        int stopNameIdx = columns.FindIndex(c => c.Key == "StopName");
        if (stopNameIdx < 0) stopNameIdx = columns.Count - 1;
        float stopNameLeftMm = X;
        for (int i = 0; i < stopNameIdx && i < columns.Count; i++) stopNameLeftMm += columns[i].Width;
        float stopNameRightMm = stopNameLeftMm + (stopNameIdx >= 0 && stopNameIdx < columns.Count ? columns[stopNameIdx].Width : 0f);

        // Asymetrická pozice šipky:
        //   Vpravo: šipka UVNITŘ sloupce u pravé hrany — text je left-aligned, k pravé hraně obvykle nezasahuje.
        //   Vlevo:  šipka MIMO sloupec (vlevo od jeho levé hrany) — left-aligned text by jinak kolidoval.
        //           Bracket pak vede zpět až k levé hraně sloupce (do prázdné padding-right oblasti předchozího sloupce).
        float arrowLineXMm = arrowLeft
            ? stopNameLeftMm - ArrowOutsideOffsetMm
            : stopNameRightMm - ArrowInsetFromEdgeMm;
        float bracketColumnsSideMm = arrowLeft
            ? stopNameLeftMm                         // bracket od šipky doprava až k levé hraně sloupce zastávek
            : arrowLineXMm - ArrowBracketLengthMm;   // bracket od šipky doleva do sloupce

        // Clip na hranice elementu — chrání před přesahem, pokud součet šířek sloupců
        // přesahuje Width (nekonzistentní konfigurace). Konzistentní s DepartureGridElement.
        var clipRect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            // Záhlaví sloupců
            float currentY = ShowHeader
                ? RenderColumnHeaders(ctx, columns, headerH, paints, scale)
                : Y;

            if (hasData)
            {
                bool foundCurrent = false;
                float arrowStartY = -1f;
                float arrowEndY = -1f;

                foreach (var z in zastavky)
                {
                    if (!showPassed && !foundCurrent && !z.JeAktualni)
                        continue;
                    if (z.JeAktualni) foundCurrent = true;

                    bool isCurrent = z.JeAktualni;
                    bool isPassed = !foundCurrent && !z.JeAktualni;

                    // Sledovat Y pro šipku směru
                    if (isCurrent) arrowStartY = currentY;
                    arrowEndY = currentY;

                    // Podbarvení aktuálního řádku přes celou šířku elementu (za sloupci i v gap za posledním).
                    // Per-column Style.Data.BackgroundColor (pokud je neprůhledná) toto lokálně přepíše.
                    if (isCurrent && CurrentStopBackgroundColor is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(X, currentY, Width, rowH);
                        using var bgPaint = new SKPaint { Color = SKColor.Parse(CurrentStopBackgroundColor) };
                        ctx.Canvas.DrawRect(bgRect, bgPaint);
                    }

                    var specs = new ColumnRenderer.CellSpec[columns.Count];
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var col = columns[i];
                        specs[i] = new ColumnRenderer.CellSpec(
                            col.Width,
                            GetCellText(z, col.Key),
                            ResolveRowStyle(col, isCurrent, isPassed));
                    }
                    ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, fontScale: scale);

                    if (ShowRowSeparators)
                    {
                        var rowLineStart = ctx.ToCanvas(X, currentY + rowH);
                        var rowLineEnd = ctx.ToCanvas(X + Width, currentY + rowH);
                        ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
                    }
                    currentY += rowH;
                }

                // Šipka směru jízdy v rezervovaném gutteru — ┌─▼ (vlevo) nebo ─┐▼ (vpravo)
                if (showArrow && arrowStartY >= 0 && arrowEndY > arrowStartY)
                    RenderDirectionArrow(ctx, arrowLineXMm, bracketColumnsSideMm, arrowStartY, arrowEndY, rowH);
            }
            else if (ctx.Data is null)
            {
                // Placeholder se ukazuje jen v designeru. Print s prázdnými daty nesmí
                // vykreslovat fake zastávky.
                float placeholderFontPx = effectiveFontSize * PtToMm * ctx.MmToPx;
                using var placeholderFont = new SKFont(GetTypeface(DataStyle.FontFamily), placeholderFontPx);
                currentY = RenderPlaceholderRows(ctx, 4, "Zastávka",
                    X, currentY, Width, rowH, placeholderFont, paints.Text, paints.Grid);
            }

            // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    /// <summary>Vykreslí záhlaví sloupců přes ColumnRenderer.</summary>
    private float RenderColumnHeaders(
        RenderContext ctx, List<StopColumn> columns, float headerH, SchedulePaints paints, float scale)
    {
        // Výchozí pozadí záhlaví (přes celou šířku elementu).
        // Per-column header bg z kaskády to lokálně přepíše.
        var headerRect = ctx.ToCanvasRect(X, Y, Width, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        var specs = new ColumnRenderer.CellSpec[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            specs[i] = new ColumnRenderer.CellSpec(col.Width, col.Header, ResolveHeaderStyle(col));
        }
        ColumnRenderer.DrawRow(ctx, X, Y, headerH, specs, fontScale: scale);

        // Čára pod záhlavím
        var lineStart = ctx.ToCanvas(X, Y + headerH);
        var lineEnd = ctx.ToCanvas(X + Width, Y + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, paints.Grid);

        return Y + headerH;
    }

    // ─── Style resolution (kaskáda column → element → CellDefaults) ─────────

    /// <summary>Parent kaskády pro per-column header override — co sloupec dědí, když vlastní styl nemá.</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() => HeaderStyle.Resolve(ScheduleHeaderDefaults);

    /// <summary>Parent kaskády pro per-column data override — co sloupec dědí, když vlastní styl nemá.</summary>
    public ResolvedTextStyle ResolveColumnDataParent() => DataStyle.Resolve(ScheduleDataDefaults);

    /// <summary>Vyřeší styl záhlaví sloupce. Legacy StopColumn.Alignment je fallback pokud HAlign není v cascade.</summary>
    private ResolvedTextStyle ResolveHeaderStyle(StopColumn col)
    {
        var elementResolved = ResolveColumnHeaderParent();
        var resolved = col.Style?.Header is { } h ? h.Resolve(elementResolved) : elementResolved;

        // Legacy Alignment fallback: pokud ani sloupec, ani element HAlign nedefinuje, vezmi col.Alignment
        if (col.Style?.Header?.HAlign is null && HeaderStyle.HAlign is null)
            resolved = resolved with { HAlign = col.Alignment };

        return resolved;
    }

    /// <summary>Vyřeší styl datové buňky sloupce. Legacy Alignment fallback stejně jako pro header.</summary>
    private ResolvedTextStyle ResolveDataStyle(StopColumn col)
    {
        var elementResolved = ResolveColumnDataParent();
        var resolved = col.Style?.Data is { } d ? d.Resolve(elementResolved) : elementResolved;

        if (col.Style?.Data?.HAlign is null && DataStyle.HAlign is null)
            resolved = resolved with { HAlign = col.Alignment };

        return resolved;
    }

    /// <summary>
    /// Vyřeší styl řádku s overridy pro aktuální / projetou zastávku.
    /// Current stop: override Bold (CurrentStopBold), FontSize (CurrentStopFontSize), Underline (CurrentStopUnderline).
    /// Passed stop: override TextColor (PassedStopsForeColor).
    /// </summary>
    private ResolvedTextStyle ResolveRowStyle(StopColumn col, bool isCurrent, bool isPassed)
    {
        var resolved = ResolveDataStyle(col);

        if (isCurrent)
        {
            resolved = resolved with
            {
                Bold      = CurrentStopBold || resolved.Bold,
                FontSize  = CurrentStopFontSize ?? resolved.FontSize,
                Underline = CurrentStopUnderline || resolved.Underline,
            };
        }

        if (isPassed)
        {
            resolved = resolved with { TextColor = PassedStopsForeColor };
        }

        return resolved;
    }

    // ─── Šipka směru jízdy ──────────────────────────────────────────────────

    /// <summary>
    /// Vykreslí šipku směru jízdy uvnitř sloupce zastávek. Bracket míří do sloupce
    /// (vpravo: ──┐▼ u pravé hrany sloupce, vlevo: ┌── u levé hrany sloupce).
    /// </summary>
    private void RenderDirectionArrow(
        RenderContext ctx, float lineXMm, float bracketColumnsSideMm,
        float startY, float endY, float rowH)
    {
        float strokeW = DirectionArrowBold ? 1.5f : 0.8f;
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(DirectionArrowColor),
            IsStroke = true,
            StrokeWidth = strokeW,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Souřadnice v px — start uprostřed písmen aktuální zastávky, konec u spodní hrany poslední zastávky
        float topY = startY + rowH * 0.5f;       // uprostřed písmen aktuální zastávky
        float bottomY = endY + rowH * 0.8f;       // kousek nad spodní hranou řádku poslední zastávky
        var lineTop = ctx.ToCanvas(lineXMm, topY);
        var lineBottom = ctx.ToCanvas(lineXMm, bottomY);
        var bracketColumnsSide = ctx.ToCanvas(bracketColumnsSideMm, topY);

        // Horizontální bracket od strany sloupců k čáře + vertikální dolů (vlevo: ┌─, vpravo: ─┐)
        ctx.Canvas.DrawLine(bracketColumnsSide, lineTop, paint);
        ctx.Canvas.DrawLine(lineTop, lineBottom, paint);

        // ▼ — šipka na konci
        float arrowSize = 1.2f * ctx.MmToPx;
        using var arrowPaint = new SKPaint
        {
            Color = paint.Color,
            IsStroke = false,
            IsAntialias = true
        };
        var path = new SKPath();
        path.MoveTo(lineBottom.X - arrowSize * 0.5f, lineBottom.Y - arrowSize);
        path.LineTo(lineBottom.X + arrowSize * 0.5f, lineBottom.Y - arrowSize);
        path.LineTo(lineBottom.X, lineBottom.Y);
        path.Close();
        ctx.Canvas.DrawPath(path, arrowPaint);
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public float CalculateHeightFromData(DataContext? data)
    {
        var zastavky = BuildZastavky(data);
        if (zastavky.Count == 0) return DesignerPlaceholderHeight;

        bool showPassed = GetBoolParam(data, "TISK_PROJETYCH_ZAST", ShowPassedStops);
        int visibleStops = zastavky.Count;
        if (!showPassed)
        {
            int currentIdx = zastavky.FindIndex(z => z.JeAktualni);
            if (currentIdx >= 0) visibleStops = zastavky.Count - currentIdx;
        }

        float headerH = ShowHeader ? ComputeHeaderH(HeaderStyle.FontSize ?? 9f) : 0f;
        float rowH = DataRowHeight ?? ComputeRowH(DataStyle.FontSize ?? 8f);
        return headerH + visibleStops * rowH;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<StopColumn> GetVisibleColumns() => Columns.Where(c => c.Visible).ToList();

    private string GetCellText(ZastavkaData z, string columnKey) => columnKey switch
    {
        "Tc"           => z.TarifniCislo,
        "StopName"     => BuildStopDisplayText(z),
        "TravelTime"   => z.CasJizdy > 0 ? z.CasJizdy.ToString() : "",
        "Zone"         => string.Join("/", z.Zony),
        "RouteLetters" => string.Join("", z.Prubehy),
        "BaseRoute"    => z.ZakladniTrasa ? "1" : "",
        _              => ""
    };

    /// <summary>Cached excluded set — vytvořen jednou per render pass.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    private HashSet<string>? _excludedSet;
    [System.Text.Json.Serialization.JsonIgnore]
    private int _excludedSetHash = -1;

    private HashSet<string> GetExcludedSet()
    {
        int hash = ExcludedAttributeIds.Count;
        foreach (var id in ExcludedAttributeIds)
            hash = HashCode.Combine(hash, StringComparer.OrdinalIgnoreCase.GetHashCode(id));
        if (_excludedSet is null || _excludedSetHash != hash)
        {
            _excludedSet = new HashSet<string>(ExcludedAttributeIds, StringComparer.OrdinalIgnoreCase);
            _excludedSetHash = hash;
        }
        return _excludedSet;
    }

    private string BuildStopDisplayText(ZastavkaData z)
    {
        var text = z.Popis;

        if (ShowStopIndentation && z.Odsazeni > 0)
            text = new string(' ', z.Odsazeni) + text;

        if (ShowSignalStopIndicator && z.NaZnameni)
            text += " " + SignalStopSymbol;

        if (ShowStopAttributes && z.Atributy.Count > 0)
        {
            var excluded = GetExcludedSet();
            var symbols = z.Atributy
                .Where(a => !excluded.Contains(a.Znak))
                .Select(a => a.Znak);
            var symbolStr = string.Join("", symbols);
            if (symbolStr.Length > 0)
                text += " " + symbolStr;
        }

        if (ShowStopNotes && z.PoznamkyZastavky.Count > 0)
        {
            var noteSymbols = string.Join("", z.PoznamkyZastavky.Select(p => p.Znak));
            if (noteSymbols.Length > 0)
                text += noteSymbols;
        }

        return text;
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Zastávky", ButtonLabel = "Upravit sloupce...", ActionId = ButtonPropertyDescriptor.ActionOpenStopColumnsEditor },

        GetDataRowHeightDescriptor(),
        new BoolPropertyDescriptor { Label = "Oddělovací čáry",   Group = "Řádky",    Get = () => ShowRowSeparators, Set = v => ShowRowSeparators = v },

        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor   { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new StringPropertyDescriptor { Label = "Symbol na znamení", Group = "Zastávky", Get = () => SignalStopSymbol,      Set = v => SignalStopSymbol = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },

        new BoolPropertyDescriptor  { Label = "Šipka směru",      Group = "Směr jízdy", Get = () => ShowDirectionArrow,    Set = v => ShowDirectionArrow = v },
        new EnumPropertyDescriptor
        {
            Label = "Pozice šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowPosition,
            Set = v => DirectionArrowPosition = (DirectionArrowSide)v,
            Options =
            [
                ("Vpravo", (object)DirectionArrowSide.Right),
                ("Vlevo",  (object)DirectionArrowSide.Left),
            ]
        },
        new ColorPropertyDescriptor { Label = "Barva šipky",     Group = "Směr jízdy", Get = () => DirectionArrowColor,  Set = v => DirectionArrowColor = v },
        new BoolPropertyDescriptor  { Label = "Tučná šipka",     Group = "Směr jízdy", Get = () => DirectionArrowBold,   Set = v => DirectionArrowBold = v },

        new BoolPropertyDescriptor  { Label = "Záhlaví sloupců",      Group = "Záhlaví",    Get = () => ShowHeader,            Set = v => ShowHeader = v },

        new BoolPropertyDescriptor  { Label = "Podtržení aktuální",  Group = "Zvýraznění", Get = () => CurrentStopUnderline, Set = v => CurrentStopUnderline = v },
        new BoolPropertyDescriptor  { Label = "Tučně aktuální",      Group = "Zvýraznění", Get = () => CurrentStopBold,      Set = v => CurrentStopBold = v },
        new FloatPropertyDescriptor { Label = "Font aktuální (pt)",  Group = "Zvýraznění", Get = () => CurrentStopFontSize ?? (DataStyle.FontSize ?? 8f), Set = v => CurrentStopFontSize = Math.Abs(v - (DataStyle.FontSize ?? 8f)) < 0.01f ? null : v, Min = 4, Max = 200, Unit = "pt" },
        new ColorPropertyDescriptor { Label = "Pozadí aktuální",     Group = "Zvýraznění", Get = () => CurrentStopBackgroundColor ?? "", Set = v => CurrentStopBackgroundColor = string.IsNullOrEmpty(v) ? null : v },
        new BoolPropertyDescriptor  { Label = "Projeté zastávky",    Group = "Zvýraznění", Get = () => ShowPassedStops,      Set = v => ShowPassedStops = v },
        new ColorPropertyDescriptor { Label = "Barva projetých",     Group = "Zvýraznění", Get = () => PassedStopsForeColor, Set = v => PassedStopsForeColor = v },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (DataRowHeight is not null) DataRowHeight *= scale;
        if (CurrentStopFontSize is not null) CurrentStopFontSize *= scale;
        foreach (var col in Columns)
            col.Width *= scale;
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new StopListElement
        {
            DataRowHeight           = DataRowHeight,
            ShowRowSeparators       = ShowRowSeparators,
            Columns                 = Columns.Select(c => c.Clone()).ToList(),
            ShowDirectionArrow      = ShowDirectionArrow,
            DirectionArrowColor     = DirectionArrowColor,
            DirectionArrowBold      = DirectionArrowBold,
            DirectionArrowPosition  = DirectionArrowPosition,
            ShowStopIndentation     = ShowStopIndentation,
            ShowSignalStopIndicator = ShowSignalStopIndicator,
            SignalStopSymbol        = SignalStopSymbol,
            ShowStopAttributes      = ShowStopAttributes,
            ShowStopNotes           = ShowStopNotes,
            CurrentStopUnderline    = CurrentStopUnderline,
            ShowPassedStops         = ShowPassedStops,
            PassedStopsForeColor    = PassedStopsForeColor,
            ExcludedAttributeIds    = [.. ExcludedAttributeIds],
            ShowHeader              = ShowHeader,
            CurrentStopBold         = CurrentStopBold,
            CurrentStopFontSize     = CurrentStopFontSize,
            CurrentStopBackgroundColor = CurrentStopBackgroundColor,
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}

/// <summary>Strana, na které se vykresluje šipka směru jízdy. Right = výchozí pro DPKV. Left = pro CBDP.</summary>
public enum DirectionArrowSide
{
    Right,
    Left
}
