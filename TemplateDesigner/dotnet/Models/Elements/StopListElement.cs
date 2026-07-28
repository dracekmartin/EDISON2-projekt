using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Tabulka zastávek (levý sloupec zastávkového JŘ).
/// Zobrazuje: TČ, název zastávky, čas jízdy, zóna, průběhy, atributy, poznámky.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// Uživatel umístí na canvas vedle DepartureGridElement(ů).
/// </summary>
public class StopListElement : StopScheduleElementBase, IRowHighlightable
{
    // ═══════════════════════════════════════════
    // Sloupce — konfigurovatelné pořadí, šířky, záhlaví
    // ═══════════════════════════════════════════
    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = StopFields.TravelTime,   Header = "Čas",       Width = 8f,  Visible = true },
        new() { Key = StopFields.StopName,     Header = "Zastávka",  Width = 50f, Visible = true },
        new() { Key = StopFields.Zone,         Header = "Zóna",      Width = 12f, Visible = false },
        new() { Key = StopFields.Poradi,       Header = "Poř.",      Width = 6f,  Visible = false },
        new() { Key = StopFields.JeAktualni,   Header = "Akt.",      Width = 6f,  Visible = false },
        new() { Key = StopFields.Nastupiste,   Header = "Nástupiště", Width = 12f, Visible = false },
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
    /// <summary>Přestupy na jiný druh dopravy (vlak „L" / autobus „A") u zastávky. Default vyp =
    /// jako legacy (legacy StopList je netiskne). Data je posílají vždy, přepínač řídí zobrazení.</summary>
    public bool ShowTransferAttributes { get; set; }
    public bool ShowStopNotes { get; set; } = true;

    /// <summary>Strana značek (na znamení, atributy, poznámky) vůči názvu zastávky.</summary>
    /// <remarks>Default AfterName = za názvem zastávky (CBDP layout). BeforeName = před názvem (DPKV layout, např. „× Globus").</remarks>
    public StopMarkersSide MarkersPosition { get; set; } = StopMarkersSide.AfterName;

    /// <summary>Zdroj sloupce „Nástupiště" při více sloupcích u zastávky (KOVED #13048 bod 4).
    /// FromRoute (default) = z trasy linky (dnešní chování); MostFrequent = nástupiště, odkud jede
    /// nejvíc spojů; All = výčet všech („2,3"). Obojí dle spojů (přednost před hodnotou z trasy).</summary>
    public MultiPlatformDisplay MultiPlatformMode { get; set; } = MultiPlatformDisplay.FromRoute;

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
    // Podmíněné řádkové featury (sdílené s DynamicTable/LineTable přes RowConditions).
    // Vyhodnocují výraz nad daty zastávky (z.SourceRow). Prázdný výraz = vypnuto (nulová změna).
    // ═══════════════════════════════════════════
    /// <summary>Zastávka, kde výraz neplatí, se nevykreslí (a nezapočítá do výšky). Prázdné = všechny.</summary>
    public string? RowVisibilityExpression { get; set; }
    /// <summary>Podmíněné zvýraznění: seznam pravidel (výraz → barva pozadí), první splněné vyhrává.
    /// Priorita nad podbarvením aktuální zastávky. Null/prázdné = bez zvýraznění.</summary>
    public List<HighlightRule>? HighlightRules { get; set; }

    // ═══════════════════════════════════════════
    // Projeté zastávky
    // ═══════════════════════════════════════════
    public bool ShowPassedStops { get; set; }
    public string PassedStopsForeColor { get; set; } = "#FF999999";

    /// <summary>Multi-varianta časů tras: true = zobrazit všechny varianty (N sloupců dle dat, hlavičky = písmena),
    /// false = jen výchozí trasa (jeden sloupec „Čas jízdy" = ZakladniCisloTrasy). Default všechny (= legacy).</summary>
    public bool ShowAllRouteVariants { get; set; } = true;

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

    /// <summary>Padding sub-cell značek kolem textu — pro vizuální vzdušnost vlevo i vpravo (mm celkem).</summary>
    private const float MarkersSubCellPaddingMm = 1.0f;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var zastavky = ExpandPlatformRows(BuildZastavky(ctx.Data));
        bool hasData = zastavky.Count > 0;

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit font
        float scale = hasData ? ComputeFontScale(CalculateHeightFromData(ctx.Data)) : 1f;
        float baseFontSize = DataStyle.FontSize ?? 8f;
        // Výška záhlaví/řádku musí odpovídat NEJVĚTŠÍMU reálně vykreslenému (per-column resolved)
        // fontu, ne jen element-level fontu. Jinak vzniká asymetrie: per-column font glyf zvětší,
        // ale výška se řídí element fontem (nebo naopak) → výška ≠ text.
        float baseHeaderFontSize = MaxResolvedHeaderFontPt();
        float effectiveFontSize = baseFontSize * scale;
        float effectiveHeaderFontSize = baseHeaderFontSize * scale;

        // TR4: fixed-then-shrink — pevná DataRowHeight se taky zmenší při přetečení (sdílený helper, jako DepartureGrid).
        float rowH = ScaledDataRowHeight(MaxResolvedDataFontPt(), scale);
        float headerH = ComputeHeaderH(effectiveHeaderFontSize) * MaxHeaderLines();

        using var paints = CreateStandardPaints(ctx);

        var columns = GetVisibleColumns();

        // Multi-varianta časů tras: rozvine sloupec „CAS_JIZDY" na N variantních sub-sloupců
        // (dynamicky dle dat, hlavičky = písmena z VARIANTY_TRAS). < 2 varianty → beze změny (jeden sloupec).
        var variantHeaders = GetVariantHeaders(ctx.Data);
        if (ShowAllRouteVariants && variantHeaders.Count >= 2)
            columns = ExpandTravelTimeColumns(columns, variantHeaders);

        // Parametry z tiskového dialogu přepíší element properties
        bool showPassed = GetBoolParam(ctx.Data, "TISK_PROJETYCH_ZAST", ShowPassedStops);
        bool showArrow = GetBoolParam(ctx.Data, "TISK_SIPKY", ShowDirectionArrow);
        bool arrowLeft = showArrow && DirectionArrowPosition == DirectionArrowSide.Left;

        // Šipka se vykresluje UVNITŘ sloupce zastávek (Key = NAZEV_ZASTAVKY) — vertikální čára je
        // kousek od vnitřního okraje sloupce, bracket horizontálně směřuje do sloupce.
        // Nepřičítá se nic k šířce layoutu → element a sloupce zachovávají Width.
        // Pro běžně krátké názvy zastávek se text se šipkou nepřekrývá.
        // Fallback (žádný StopName sloupec): poslední sloupec.
        int stopNameIdx = columns.FindIndex(c => c.Key == StopFields.StopName);
        if (stopNameIdx < 0) stopNameIdx = columns.Count - 1;
        var colW = ResolveColumnWidths(columns);
        float stopNameLeftMm = X;
        for (int i = 0; i < stopNameIdx && i < columns.Count; i++) stopNameLeftMm += colW[columns[i]];
        float stopNameRightMm = stopNameLeftMm + (stopNameIdx >= 0 && stopNameIdx < columns.Count ? colW[columns[stopNameIdx]] : 0f);

        // Symbolový font pro piktogramové značky (MHD „na znamení" = drátový kód „D" → piktogram).
        var symbolFace = GetTypeface(FontResolver.SymbolFontFamily(ctx.Data?.Culture));

        // Šířka sub-cell pro značky (BeforeName) — auto-fit podle nejširšího marker textu v datech.
        float markersSubCellWidth = MarkersPosition == StopMarkersSide.BeforeName
            ? ComputeMarkersSubCellWidth(zastavky, effectiveFontSize, ctx.MmToPx, symbolFace)
            : 0f;

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
                ? RenderColumnHeaders(ctx, columns, stopNameIdx, markersSubCellWidth, colW, headerH, paints, scale)
                : Y;

            if (hasData)
            {
                bool foundCurrent = false;
                float arrowStartY = -1f;
                float arrowEndY = -1f;

                // Rezerva „Přizpůsobit buňce" pro sloupec názvů: při šipce vpravo nesmí zmenšený text
                // podlézt její vodorovnou část (svislice + bracket míří do sloupce) — cíl končí před ní.
                float nameShrinkReserveMm = (showArrow && !arrowLeft)
                    ? ArrowInsetFromEdgeMm + ArrowBracketLengthMm + 0.5f
                    : CellRenderer.DefaultShrinkReserveMm;

                foreach (var z in zastavky)
                {
                    if (!showPassed && !foundCurrent && !z.JeAktualni)
                        continue;
                    if (z.JeAktualni) foundCurrent = true;

                    bool isCurrent = z.JeAktualni;
                    bool isPassed = !foundCurrent && !z.JeAktualni;

                    // Podmíněné skrytí řádku (po určení current/passed, ať se nerozbije sledování current).
                    if (!RowConditions.IsVisible(RowVisibilityExpression, z.SourceRow)) continue;

                    // Sledovat Y pro šipku směru
                    if (isCurrent) arrowStartY = currentY;
                    arrowEndY = currentY;

                    // Podbarvení řádku přes celou šířku elementu (za sloupci i v gap za posledním).
                    // Priorita: podmíněné zvýraznění (uživatelský záměr) > podbarvení aktuální zastávky.
                    // Per-column Style.Data.BackgroundColor (pokud je neprůhledná) toto lokálně přepíše.
                    string? rowBg =
                        RowConditions.HighlightColor(HighlightRules, z.SourceRow)
                        ?? (isCurrent ? CurrentStopBackgroundColor : null);
                    if (rowBg is not null)
                    {
                        var bgRect = ctx.ToCanvasRect(X, currentY, Width, rowH);
                        using var bgPaint = new SKPaint { Color = SKColor.Parse(rowBg) };
                        ctx.Canvas.DrawRect(bgRect, bgPaint);
                    }

                    // „Přizpůsobit buňce" + značky za názvem: zmenšení názvu počítá i s šířkou
                    // značek (škálují se spolu), aby se za zmenšený název vešly a nepodlézaly šipku.
                    float nameTrailingMm = 0f;
                    if (stopNameIdx >= 0 && MarkersPosition == StopMarkersSide.AfterName)
                    {
                        var nameStyleRow = ResolveRowStyle(columns[stopNameIdx], isCurrent, isPassed);
                        nameTrailingMm = MeasureMarkersTrailingMm(ctx, BuildStopMarkerRuns(z), nameStyleRow, symbolFace, scale);
                    }

                    var specs = BuildDataRowSpecs(columns, z, isCurrent, isPassed, stopNameIdx, markersSubCellWidth, colW, nameShrinkReserveMm, nameTrailingMm);
                    ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, fontScale: scale);

                    // Značky (na znamení / atributy / poznámky) se kreslí zvlášť po úsecích: piktogramové
                    // symboly symbolovým Timetable fontem (MHD „na znamení" = kód „D"), text a poznámky
                    // normálním fontem. V CellRenderu nejde míchat font v jedné buňce (#7161 MHD parita).
                    DrawStopMarkers(ctx, z, currentY, rowH, columns[stopNameIdx], stopNameLeftMm,
                        colW[columns[stopNameIdx]], markersSubCellWidth, isCurrent, isPassed, symbolFace, scale,
                        nameShrinkReserveMm);

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
                using var placeholderFont = new SKFont(GetTypeface(ResolveColumnDataParent()), placeholderFontPx);
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
        RenderContext ctx, List<StopColumn> columns, int stopNameIdx, float markersSubCellWidth,
        Dictionary<StopColumn, float> widths, float headerH, SchedulePaints paints, float scale)
    {
        // Výchozí pozadí záhlaví (přes celou šířku elementu).
        // Per-column header bg z kaskády to lokálně přepíše.
        var headerRect = ctx.ToCanvasRect(X, Y, Width, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        var specs = BuildHeaderRowSpecs(columns, stopNameIdx, markersSubCellWidth, widths);
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

    /// <summary>Vyřeší styl záhlaví sloupce přes sdílené jádro (kaskáda column → element → baked +
    /// legacy StopColumn.Alignment fallback). Public kvůli testovatelnosti (StopListCascadeTests).</summary>
    public ResolvedTextStyle ResolveHeaderStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Header, ResolveColumnHeaderParent(), col.Alignment,
            elementDefinesHAlign: HeaderStyle.HAlign is not null);

    /// <summary>Vyřeší styl datové buňky sloupce (stejná kaskáda + legacy Alignment fallback).</summary>
    public ResolvedTextStyle ResolveDataStyle(StopColumn col) =>
        TableLayout.ResolveCascade(col.Style?.Data, ResolveColumnDataParent(), col.Alignment,
            elementDefinesHAlign: DataStyle.HAlign is not null);

    /// <summary>
    /// Vyřeší styl řádku s overridy pro aktuální / projetou zastávku.
    /// Current stop: override Bold (CurrentStopBold), FontSize (CurrentStopFontSize), Underline (CurrentStopUnderline).
    /// Passed stop: override TextColor (PassedStopsForeColor).
    /// </summary>
    public ResolvedTextStyle ResolveRowStyle(StopColumn col, bool isCurrent, bool isPassed)
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
    /// Sestaví CellSpec[] pro datový řádek. Když MarkersPosition = BeforeName a existuje sloupec zastávek,
    /// rozdělí cell sloupce zastávek na [markers sub-cell, name sub-cell] aby názvy zůstaly zarovnané.
    /// </summary>
    private ColumnRenderer.CellSpec[] BuildDataRowSpecs(
        List<StopColumn> columns, ZastavkaData z, bool isCurrent, bool isPassed,
        int stopNameIdx, float markersSubCellWidth, Dictionary<StopColumn, float> widths,
        float nameShrinkReserveMm = CellRenderer.DefaultShrinkReserveMm, float nameTrailingMm = 0f)
    {
        bool split = markersSubCellWidth > 0
                     && stopNameIdx >= 0 && stopNameIdx < columns.Count
                     && widths[columns[stopNameIdx]] > markersSubCellWidth;

        if (!split)
        {
            var specs = new ColumnRenderer.CellSpec[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                specs[i] = new ColumnRenderer.CellSpec(
                    widths[col],
                    GetCellText(z, col.Key),
                    ResolveRowStyle(col, isCurrent, isPassed),
                    i == stopNameIdx ? nameShrinkReserveMm : CellRenderer.DefaultShrinkReserveMm,
                    i == stopNameIdx ? nameTrailingMm : 0f);
            }
            return specs;
        }

        var result = new ColumnRenderer.CellSpec[columns.Count + 1];
        int idx = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (i == stopNameIdx)
            {
                var nameStyle = ResolveRowStyle(col, isCurrent, isPassed);
                var markerStyle = nameStyle with { HAlign = TextHorizontalAlignment.Right };
                // Text značek prázdný — kreslí je DrawStopMarkers zvlášť (per-run font). Sub-cell drží jen šířku a pozadí.
                result[idx++] = new ColumnRenderer.CellSpec(markersSubCellWidth, "", markerStyle);
                result[idx++] = new ColumnRenderer.CellSpec(widths[col] - markersSubCellWidth, GetCellText(z, col.Key), nameStyle, nameShrinkReserveMm, nameTrailingMm);
            }
            else
            {
                result[idx++] = new ColumnRenderer.CellSpec(
                    widths[col],
                    GetCellText(z, col.Key),
                    ResolveRowStyle(col, isCurrent, isPassed));
            }
        }
        return result;
    }

    /// <summary>Sestaví CellSpec[] pro řádek záhlaví. Pokud BeforeName a markersSubCellWidth > 0, rozdělí stop name sloupec stejně jako datové řádky.</summary>
    private ColumnRenderer.CellSpec[] BuildHeaderRowSpecs(List<StopColumn> columns, int stopNameIdx, float markersSubCellWidth, Dictionary<StopColumn, float> widths)
    {
        bool split = markersSubCellWidth > 0
                     && stopNameIdx >= 0 && stopNameIdx < columns.Count
                     && widths[columns[stopNameIdx]] > markersSubCellWidth;

        if (!split)
        {
            var specs = new ColumnRenderer.CellSpec[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                specs[i] = new ColumnRenderer.CellSpec(widths[col], Rendering.ColumnHeader.Resolve(col.Header, ""), ResolveHeaderStyle(col));
            }
            return specs;
        }

        var result = new ColumnRenderer.CellSpec[columns.Count + 1];
        int idx = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (i == stopNameIdx)
            {
                var headerStyle = ResolveHeaderStyle(col);
                result[idx++] = new ColumnRenderer.CellSpec(markersSubCellWidth, "", headerStyle);
                result[idx++] = new ColumnRenderer.CellSpec(widths[col] - markersSubCellWidth, Rendering.ColumnHeader.Resolve(col.Header, ""), headerStyle);
            }
            else
            {
                result[idx++] = new ColumnRenderer.CellSpec(widths[col], Rendering.ColumnHeader.Resolve(col.Header, ""), ResolveHeaderStyle(col));
            }
        }
        return result;
    }

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

    public override float CalculateHeightFromData(DataContext? data)
    {
        var zastavky = ExpandPlatformRows(BuildZastavky(data));
        if (zastavky.Count == 0) return DesignerPlaceholderHeight;

        bool showPassed = GetBoolParam(data, "TISK_PROJETYCH_ZAST", ShowPassedStops);
        int visibleStops;
        if (string.IsNullOrWhiteSpace(RowVisibilityExpression))
        {
            // Beze změny (behavior-preserving): počet dle logiky projetých zastávek.
            visibleStops = zastavky.Count;
            if (!showPassed)
            {
                int currentIdx = zastavky.FindIndex(z => z.JeAktualni);
                if (currentIdx >= 0) visibleStops = zastavky.Count - currentIdx;
            }
        }
        else
        {
            // Aktivní podmínka viditelnosti — spočítat řádky přesně jako v Render (parita výška↔render).
            visibleStops = 0;
            bool foundCurrent = false;
            foreach (var z in zastavky)
            {
                if (!showPassed && !foundCurrent && !z.JeAktualni) continue;
                if (z.JeAktualni) foundCurrent = true;
                if (!RowConditions.IsVisible(RowVisibilityExpression, z.SourceRow)) continue;
                visibleStops++;
            }
        }

        // Stejný zdroj fontu jako v Render() — max resolved per-column, ať scale/overflow sedí.
        float headerH = ShowHeader ? ComputeHeaderH(MaxResolvedHeaderFontPt()) * MaxHeaderLines() : 0f;
        float rowH = DataRowHeight ?? ComputeRowH(MaxResolvedDataFontPt());
        return headerH + visibleStops * rowH;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<StopColumn> GetVisibleColumns() => Columns.Where(c => c.Visible).ToList();

    /// <summary>Vyřeší efektivní šířky sloupců přes sdílené jádro (<see cref="TableLayout.ResolveWidths"/>):
    /// pevné (Width != null) zůstanou, auto (null) si rovnoměrně rozdělí zbytek. StopList je mm-only.</summary>
    private Dictionary<StopColumn, float> ResolveColumnWidths(List<StopColumn> visible)
    {
        var widths = TableLayout.ResolveWidths(
            visible.Select(c => new TableLayout.ColWidth(c.Width)).ToList(), Width);
        var result = new Dictionary<StopColumn, float>(visible.Count);
        for (int i = 0; i < visible.Count; i++) result[visible[i]] = widths[i];
        return result;
    }

    /// <summary>Největší resolved font záhlaví napříč viditelnými sloupci (pt) — vč. per-column overridů.
    /// Výška záhlaví se počítá z této hodnoty, aby odpovídala reálně vykreslenému textu.</summary>
    private float MaxResolvedHeaderFontPt()
    {
        var cols = GetVisibleColumns();
        return cols.Count > 0 ? cols.Max(c => ResolveHeaderStyle(c).FontSize) : (HeaderStyle.FontSize ?? 9f);
    }

    /// <summary>Nejvyšší počet řádků záhlaví napříč viditelnými sloupci — dvouřádkové záhlaví přes \n
    /// (např. „Zastávka" / „Stop"). Výška záhlaví se násobí tímto počtem, ať se text vejde.</summary>
    private int MaxHeaderLines()
    {
        var cols = GetVisibleColumns();
        int max = 1;
        foreach (var c in cols)
            max = Math.Max(max, Rendering.CellLayout.LineCount(Rendering.ColumnHeader.Resolve(c.Header, "")));
        return max;
    }

    /// <summary>Největší resolved font datové buňky napříč viditelnými sloupci (pt) — vč. per-column overridů.</summary>
    private float MaxResolvedDataFontPt()
    {
        var cols = GetVisibleColumns();
        return cols.Count > 0 ? cols.Max(c => ResolveDataStyle(c).FontSize) : (DataStyle.FontSize ?? 8f);
    }

    private string GetCellText(ZastavkaData z, string columnKey)
    {
        // Variantní časový sub-sloupec „CAS_JIZDY#i" → z.TrasaMin[i] (číslo / „<" / "" jak přišlo z Caché)
        if (columnKey.StartsWith(StopFields.TravelTimeVariantPrefix, System.StringComparison.Ordinal))
        {
            return int.TryParse(columnKey.AsSpan(StopFields.TravelTimeVariantPrefix.Length), out int vi)
                   && vi >= 0 && vi < z.TrasaMin.Count ? z.TrasaMin[vi] : "";
        }
        return columnKey switch
        {
            StopFields.StopName     => BuildStopDisplayText(z),
            StopFields.TravelTime   => z.CasJizdy > 0 ? z.CasJizdy.ToString() : "",
            StopFields.Zone         => string.Join("/", z.Zony),
            StopFields.Nastupiste   => MultiPlatformMode switch
            {
                // Dle spojů: první položka CSV = nejčetnější / celý výčet; bez per-spoj dat fallback na trasu
                MultiPlatformDisplay.MostFrequent => z.NastupisteSpoju.Length > 0
                    ? z.NastupisteSpoju.Split(',')[0] : z.Nastupiste,
                MultiPlatformDisplay.All => z.NastupisteSpoju.Length > 0
                    ? z.NastupisteSpoju.Replace(",", ", ") : z.Nastupiste,
                _ => z.Nastupiste,
            },
            StopFields.Poradi       => z.Poradi > 0 ? z.Poradi.ToString() : "",
            StopFields.JeAktualni   => z.JeAktualni ? "1" : "",
            _                       => ""
        };
    }

    private const float VariantColumnWidthMm = 6f;

    /// <summary>Hlavičky variantních časových sloupců z page values (VARIANTY_TRAS). Prázdné = jedna varianta.</summary>
    private static List<string> GetVariantHeaders(DataContext? data)
    {
        if (data?.GetValue("VARIANTY_TRAS") is System.Text.Json.Nodes.JsonArray arr)
            return arr.Select(n => n?.ToString() ?? "").ToList();
        return [];
    }

    /// <summary>Rozvine sloupec „CAS_JIZDY" na N variantních sub-sloupců (Key „CAS_JIZDY#i", Header = písmeno,
    /// pevná šířka). Ostatní sloupce beze změny. Volá se jen při >= 2 variantách.</summary>
    private static List<StopColumn> ExpandTravelTimeColumns(List<StopColumn> cols, List<string> headers)
    {
        var result = new List<StopColumn>(cols.Count + headers.Count);
        foreach (var col in cols)
        {
            if (col.Key == StopFields.TravelTime)
            {
                for (int i = 0; i < headers.Count; i++)
                    result.Add(new StopColumn { Key = $"{StopFields.TravelTimeVariantPrefix}{i}", Header = headers[i], Width = VariantColumnWidthMm, Visible = true });
            }
            else result.Add(col);
        }
        return result;
    }

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

    /// <summary>
    /// Spočítá optimální šířku sub-cell pro značky (mm) podle nejširšího marker textu v datech.
    /// 0 = žádné značky → není potřeba split. Měří per-úsek jeho fontem (piktogramy symbolovým),
    /// aby šířka seděla i pro pictogramové znaky. Padding zajistí vizuální vzdušnost.
    /// </summary>
    private float ComputeMarkersSubCellWidth(List<ZastavkaData> zastavky, float fontSizePt, float mmToPx, SKTypeface symbolFace)
    {
        if (zastavky.Count == 0) return 0f;
        float fontPx = fontSizePt * PtToMm * mmToPx;
        using var font = new SKFont(GetTypeface(ResolveColumnDataParent()), fontPx);
        using var symbolFont = new SKFont(symbolFace, fontPx);
        float maxPx = 0f;
        foreach (var z in zastavky)
        {
            float w = 0f;
            foreach (var r in BuildStopMarkerRuns(z))
                w += (r.Symbol ? symbolFont : font).MeasureText(r.Text);
            if (w > maxPx) maxPx = w;
        }
        return maxPx > 0 ? maxPx / mmToPx + MarkersSubCellPaddingMm : 0f;
    }

    /// <summary>Jeden úsek značek se svým fontem: <c>Symbol</c>=true → piktogramový Timetable font,
    /// jinak normální font.</summary>
    private readonly record struct MarkerRun(string Text, bool Symbol);

    /// <summary>
    /// Značky zastávky (na znamení, atributy, poznámky) rozdělené na úseky podle fontu.
    /// Piktogramové symboly (jednoznakový SpecZnak, který není textové „x/y/z") jdou symbolovým
    /// Timetable fontem — bez toho MHD „na znamení" (kód „D") vykreslí surové písmeno „D".
    /// Textové značky a poznámky jdou normálním fontem (AD „x" zůstává „x"). Úseky navzájem
    /// oddělené mezerou (shodně s dřívějším spojeným textem).
    /// </summary>
    private List<MarkerRun> BuildStopMarkerRuns(ZastavkaData z)
    {
        var runs = new List<MarkerRun>();
        void Separator() { if (runs.Count > 0) runs.Add(new MarkerRun(" ", false)); }

        // „Na znamení" přes dedikované pole (mock data) — zobrazovací symbol, normální font.
        if (ShowSignalStopIndicator && z.NaZnameni)
            runs.Add(new MarkerRun(SignalStopSymbol, false));

        // Atributy — každý vlastní úsek (piktogram, pokud jednoznakový a ne textové „x/y/z").
        if (z.Atributy.Count > 0)
        {
            var excluded = GetExcludedSet();
            var attrs = z.Atributy
                .Where(a => !excluded.Contains(a.Znak))
                .Where(a => IsNaZnameniAttr(a) ? ShowSignalStopIndicator
                          : IsTransferAttr(a) ? ShowTransferAttributes
                          : ShowStopAttributes)
                .ToList();
            if (attrs.Count > 0)
            {
                Separator();
                foreach (var a in attrs)
                    runs.Add(new MarkerRun(a.Znak, a.Znak.Length == 1 && !SymbolJeText(a.Znak)));
            }
        }

        // Poznámky zastávky — textové značky (legenda je má jako „text"), normální font.
        if (ShowStopNotes && z.PoznamkyZastavky.Count > 0)
        {
            var noteSymbols = string.Join("", z.PoznamkyZastavky.Select(p => p.Znak));
            if (noteSymbols.Length > 0)
            {
                Separator();
                runs.Add(new MarkerRun(noteSymbols, false));
            }
        }

        return runs;
    }

    /// <summary>Vykreslí značky zastávky po úsecích (per-run font). BeforeName → do rezervované
    /// sub-cell před názvem (zarovnané vpravo); AfterName → za text názvu. Volá se po DrawRow.</summary>
    private void DrawStopMarkers(RenderContext ctx, ZastavkaData z, float rowTopMm, float rowH,
        StopColumn nameCol, float stopNameLeftMm, float nameColWidthMm, float markersSubCellWidth,
        bool isCurrent, bool isPassed, SKTypeface symbolFace, float scale,
        float nameShrinkReserveMm = CellRenderer.DefaultShrinkReserveMm)
    {
        var runs = BuildStopMarkerRuns(z);
        if (runs.Count == 0) return;
        var style = ResolveRowStyle(nameCol, isCurrent, isPassed);

        if (MarkersPosition == StopMarkersSide.BeforeName)
        {
            if (markersSubCellWidth <= 0) return;
            var cellMm = new SKRect(stopNameLeftMm, rowTopMm, stopNameLeftMm + markersSubCellWidth, rowTopMm + rowH);
            var contentMm = CellLayout.ApplyPadding(cellMm, style);
            DrawMarkerRuns(ctx, contentMm, runs, style, symbolFace, scale, TextHorizontalAlignment.Right, startAfterNamePx: null);
        }
        else
        {
            // AfterName: za názvem — odměř šířku názvu ve stejném fontu a začni značky za ní.
            // Při „Přizpůsobit buňce" musí měření i značky použít TUTÉŽ efektivní škálu jako
            // CellRenderer (stejné vstupy vč. rezervy a šířky značek), jinak se pozice rozjedou.
            var cellMm = new SKRect(stopNameLeftMm, rowTopMm, stopNameLeftMm + nameColWidthMm, rowTopMm + rowH);
            var contentMm = CellLayout.ApplyPadding(cellMm, style);
            string displayText = BuildStopDisplayText(z);
            float trailingMm = MeasureMarkersTrailingMm(ctx, runs, style, symbolFace, scale);
            float effScale = CellRenderer.ComputeShrinkScale(
                style, [displayText], contentMm.Width, ctx.MmToPx, scale, nameShrinkReserveMm, trailingMm);
            using var nameFont = style.CreateFont(effScale, ctx.MmToPx);
            float nameWidthPx = nameFont.MeasureText(displayText);
            float spacePx = nameFont.MeasureText(" ");
            var m = nameFont.Metrics;
            var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
            var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);
            var (nameX, _) = CellLayout.TextAnchorPx(tl, br, nameWidthPx, -m.Ascent, m.Descent, style.HAlign, style.VAlign);
            DrawMarkerRuns(ctx, contentMm, runs, style, symbolFace, effScale, TextHorizontalAlignment.Left, startAfterNamePx: nameX + nameWidthPx + spacePx);
        }
    }

    /// <summary>Šířka bloku značek za názvem (oddělovací mezera + úseky) v mm při dané škále —
    /// vstup pro „Přizpůsobit buňce": o ni se název zmenšuje navíc, aby se za něj značky vešly.</summary>
    private float MeasureMarkersTrailingMm(RenderContext ctx, List<MarkerRun> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        if (runs.Count == 0) return 0f;
        using var normalFont = style.CreateFont(scale, ctx.MmToPx);
        using var symbolFont = new SKFont(symbolFace, normalFont.Size);
        float totalPx = normalFont.MeasureText(" ");
        foreach (var r in runs) totalPx += (r.Symbol ? symbolFont : normalFont).MeasureText(r.Text);
        return totalPx / ctx.MmToPx;
    }

    /// <summary>Nakreslí úseky značek do obsahové oblasti. <paramref name="startAfterNamePx"/> = pevný
    /// začátek X (za názvem, AfterName); null = zarovná celý blok dle <paramref name="hAlign"/> (BeforeName).</summary>
    private void DrawMarkerRuns(RenderContext ctx, SKRect contentMm, List<MarkerRun> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale, TextHorizontalAlignment hAlign, float? startAfterNamePx)
    {
        using var normalFont = style.CreateFont(scale, ctx.MmToPx);
        using var symbolFont = new SKFont(symbolFace, normalFont.Size);
        using var paint = style.CreateTextPaint();

        var m = normalFont.Metrics;
        var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);

        float totalPx = 0f;
        foreach (var r in runs) totalPx += (r.Symbol ? symbolFont : normalFont).MeasureText(r.Text);

        var (anchorX, baselineY) = CellLayout.TextAnchorPx(tl, br, totalPx, -m.Ascent, m.Descent, hAlign, style.VAlign);
        float xPx = startAfterNamePx ?? anchorX;

        foreach (var r in runs)
        {
            var f = r.Symbol ? symbolFont : normalFont;
            ctx.Canvas.DrawText(r.Text, xPx, baselineY, f, paint);
            xPx += f.MeasureText(r.Text);
        }
    }

    /// <summary>Je atribut zastávky „na znamení"? AD i MHD ho posílají jako atribut (ne přes pole
    /// NaZnameni), symbol se liší podle číselníku firmy (AD „x", MHD např. „D") — rozpoznáváme ho
    /// proto podle popisu (CZ „na znamení" / SK „na znamenie"), aby ho řídil přepínač „Na znamení".</summary>
    private static bool IsNaZnameniAttr(AtributData a) =>
        a.Popis.StartsWith("na znamen", StringComparison.OrdinalIgnoreCase);

    /// <summary>Je atribut zastávky přestup na jiný druh dopravy? V1: vlak „L" / autobus „A"
    /// (číselník 16). Řídí ho přepínač „Přestupy" — nad rámec legacy StopListu.</summary>
    private static bool IsTransferAttr(AtributData a) =>
        a.Znak is "A" or "L";

    /// <summary>Režim „Duplikace řádku per stanoviště" (KOVED #13048 bod 4): zastávku obsluhovanou
    /// z více stanovišť rozvine na N řádků na jejím místě v trase (pořadí = první výskyt stanoviště
    /// napříč variantami, netřídí se). Každý řádek nese své stanoviště a časy jen variant, které z něj
    /// jedou. Vývěsková (aktuální) zastávka i zastávka s ≤1 stanovištěm zůstávají jedním řádkem.
    /// Ostatní režimy = beze změny (vrací původní seznam).</summary>
    public List<ZastavkaData> ExpandPlatformRows(List<ZastavkaData> stops)
    {
        if (MultiPlatformMode != MultiPlatformDisplay.RowPerPlatform) return stops;

        var result = new List<ZastavkaData>(stops.Count);
        foreach (var z in stops)
        {
            // Odlišná stanoviště variant, které zde staví — v pořadí prvního výskytu (bez třídění).
            // Jen indexy v rozsahu TrasaMin (obojí server drží stejně dlouhé; guard proti fantom řádku).
            var platforms = new List<string>();
            int pocetSloupcu = Math.Min(z.NastupisteVariant.Count, z.TrasaMin.Count);
            for (int i = 0; i < pocetSloupcu; i++)
            {
                var p = z.NastupisteVariant[i];
                if (!string.IsNullOrEmpty(p) && !platforms.Contains(p)) platforms.Add(p);
            }

            if (z.JeAktualni || platforms.Count < 2)
            {
                result.Add(z);
                continue;
            }

            bool first = true;
            foreach (var plat in platforms)
            {
                result.Add(CloneStopForPlatform(z, plat, keepMarkers: first));
                first = false;
            }
        }
        return result;
    }

    /// <summary>Kopie zastávky pro jedno stanoviště: v <see cref="ZastavkaData.TrasaMin"/> ponechá časy
    /// jen sloupců variant jedoucích z tohoto stanoviště (ostatní prázdné). Značka „projíždí bez stání"
    /// („&lt;") není vázaná na stanoviště → zůstane jen na prvním řádku (aby se info neztratilo, ale
    /// neopakovalo). Značky (atributy / na znamení / poznámky) jen na prvním řádku; název a zóna se
    /// opakují (je to táž zastávka). Neaktivní (aktuální řeší jediný řádek výše).</summary>
    private static ZastavkaData CloneStopForPlatform(ZastavkaData z, string platform, bool keepMarkers)
    {
        var trasa = new List<string>(z.TrasaMin.Count);
        for (int i = 0; i < z.TrasaMin.Count; i++)
        {
            string nv = i < z.NastupisteVariant.Count ? z.NastupisteVariant[i] : "";
            string val = z.TrasaMin[i];
            if (nv == platform)
                trasa.Add(val);                           // čas varianty jedoucí z tohoto stanoviště
            else if (keepMarkers && val == "<")
                trasa.Add(val);                           // „projíždí" (mimo stanoviště) — 1× na prvním řádku
            else
                trasa.Add("");
        }
        return new ZastavkaData
        {
            Id               = z.Id,
            Popis            = z.Popis,
            CasJizdy         = z.CasJizdy,
            Poradi           = z.Poradi,
            JeAktualni       = false,
            Odsazeni         = z.Odsazeni,
            NaZnameni        = keepMarkers && z.NaZnameni,
            Nastupiste       = platform,
            NastupisteSpoju  = z.NastupisteSpoju,
            Zony             = z.Zony,
            Atributy         = keepMarkers ? z.Atributy : [],
            PoznamkyZastavky = keepMarkers ? z.PoznamkyZastavky : [],
            TrasaMin         = trasa,
            NastupisteVariant = z.NastupisteVariant,
            SourceRow        = z.SourceRow,
        };
    }

    private string BuildStopDisplayText(ZastavkaData z)
    {
        var name = z.Popis;
        if (ShowStopIndentation && z.Odsazeni > 0)
            name = new string(' ', z.Odsazeni) + name;

        // Značky (na znamení / atributy / poznámky) kreslí DrawStopMarkers zvlášť po úsecích
        // (per-run font) — v obou režimech (Before/AfterName). Buňka názvu nese jen název.
        return name;
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Zastávky", ButtonLabel = "Upravit sloupce...", ActionId = ButtonPropertyDescriptor.ActionOpenStopColumnsEditor },
        new BoolPropertyDescriptor  { Label = "Všechny varianty tras", Group = "Zastávky", Get = () => ShowAllRouteVariants, Set = v => ShowAllRouteVariants = v },

        GetDataRowHeightDescriptor(),
        new BoolPropertyDescriptor { Label = "Oddělovací čáry",   Group = "Řádky",    Get = () => ShowRowSeparators, Set = v => ShowRowSeparators = v },

        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor   { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new StringPropertyDescriptor { Label = "Symbol na znamení", Group = "Zastávky", Get = () => SignalStopSymbol,      Set = v => SignalStopSymbol = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Přestupy (vlak/bus)",Group = "Zastávky", Get = () => ShowTransferAttributes, Set = v => ShowTransferAttributes = v },
        new EnumPropertyDescriptor
        {
            Label = "Více nástupišť", Group = "Zastávky",
            Get = () => MultiPlatformMode,
            Set = v => MultiPlatformMode = (MultiPlatformDisplay)v,
            Options = [
                ("Z trasy linky", (object)MultiPlatformDisplay.FromRoute),
                ("Nejčetnější dle spojů", (object)MultiPlatformDisplay.MostFrequent),
                ("Výčet všech", (object)MultiPlatformDisplay.All),
                ("Duplikace řádku per stanoviště", (object)MultiPlatformDisplay.RowPerPlatform)
            ]
        },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },
        new EnumPropertyDescriptor
        {
            Label = "Pozice značek", Group = "Zastávky",
            Get = () => MarkersPosition,
            Set = v => MarkersPosition = (StopMarkersSide)v,
            Options =
            [
                ("Za zastávkou",  (object)StopMarkersSide.AfterName),
                ("Před zastávkou", (object)StopMarkersSide.BeforeName),
            ]
        },

        new BoolPropertyDescriptor  { Label = "Šipka směru",      Group = "Směr jízdy", Get = () => ShowDirectionArrow,    Set = v => ShowDirectionArrow = v },
        new EnumPropertyDescriptor
        {
            Label = "Pozice šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowPosition,
            Set = v => DirectionArrowPosition = (DirectionArrowSide)v,
            // „u okraje elementu" v labelu záměrně — šipka se kreslí na hraně elementu, ne u sloupců;
            // u širokého elementu je tedy daleko vpravo a uživateli se zdálo, že se „nic nezměnilo".
            Options =
            [
                ("Vpravo (u pravého okraje elementu)", (object)DirectionArrowSide.Right),
                ("Vlevo (u levého okraje elementu)",   (object)DirectionArrowSide.Left),
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

        RowConditionDescriptors.RowVisibility(() => RowVisibilityExpression, v => RowVisibilityExpression = v),

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (DataRowHeight is not null) DataRowHeight *= scale;
        if (CurrentStopFontSize is not null) CurrentStopFontSize *= scale;
        foreach (var col in Columns)
            if (col.Width is float w) col.Width = w * scale;   // auto (null) se neškáluje
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new StopListElement
        {
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
            ShowTransferAttributes  = ShowTransferAttributes,
            ShowStopNotes           = ShowStopNotes,
            MarkersPosition         = MarkersPosition,
            MultiPlatformMode       = MultiPlatformMode,
            CurrentStopUnderline    = CurrentStopUnderline,
            ShowPassedStops         = ShowPassedStops,
            PassedStopsForeColor    = PassedStopsForeColor,
            ShowAllRouteVariants    = ShowAllRouteVariants,
            ExcludedAttributeIds    = [.. ExcludedAttributeIds],
            ShowHeader              = ShowHeader,
            CurrentStopBold         = CurrentStopBold,
            CurrentStopFontSize     = CurrentStopFontSize,
            CurrentStopBackgroundColor = CurrentStopBackgroundColor,
            RowVisibilityExpression = RowVisibilityExpression,
            HighlightRules          = HighlightRules?.Select(r => r.Clone()).ToList(),
        };
        CopyBaseTo(clone);
        CopyStopScheduleProperties(clone);
        return clone;
    }
}

/// <summary>Strana, na které se vykresluje šipka směru jízdy. Right = výchozí pro DPKV. Left = pro CBDP.</summary>
public enum DirectionArrowSide
{
    Right,
    Left
}

/// <summary>Zdroj sloupce „Nástupiště" při více sloupcích u zastávky (KOVED #13048 bod 4).</summary>
public enum MultiPlatformDisplay
{
    /// <summary>Z trasy linky (J-věta) — dnešní chování, default.</summary>
    FromRoute,
    /// <summary>Nástupiště, odkud jede nejvíc spojů (per-spoj hodnota má přednost před trasou).</summary>
    MostFrequent,
    /// <summary>Výčet všech nástupišť dle spojů („2, 3"), seřazený dle četnosti.</summary>
    All,
    /// <summary>Duplikace řádku: zastávka obsluhovaná z více stanovišť se vypíše jako N řádků (jeden
    /// per stanoviště) na svém místě v trase; časy jen variant daného stanoviště (KOVED #13048 bod 4).</summary>
    RowPerPlatform,
}

/// <summary>Strana značek (na znamení, atributy, poznámky) vůči názvu zastávky.</summary>
public enum StopMarkersSide
{
    /// <summary>Značky za názvem zastávky (default, CBDP layout).</summary>
    AfterName,
    /// <summary>Značky před názvem zastávky (DPKV layout, např. „× Globus").</summary>
    BeforeName
}
