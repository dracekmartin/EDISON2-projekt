using System.Text.Json.Serialization;
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
    /// <summary>Svislé čáry mezi sloupci byly u seznamu zastávek vždy opt-in (na rozdíl od ostatních JŘ prvků).</summary>
    public StopListElement() => Grid.ShowColumnLines = false;

    // ═══════════════════════════════════════════
    // Sloupce - konfigurovatelné pořadí, šířky, záhlaví
    // ═══════════════════════════════════════════
    public List<StopColumn> Columns { get; set; } = GetDefaultColumns();

    public static List<StopColumn> GetDefaultColumns() =>
    [
        new() { Key = StopFields.TravelTime,   Header = "Čas",       Width = 8f,  Visible = true },
        // Rozvine se při renderu na N sloupců dle variant linky; šířka platí pro jeden z nich.
        new() { Key = StopFields.TravelTimeVariants, Header = "",    Width = VariantColumnWidthMm, Visible = true },
        new() { Key = StopFields.StopName,     Header = "Zastávka",  Width = 50f, Visible = true },
        new() { Key = StopFields.Zone,         Header = "Zóna",      Width = 12f, Visible = false },
        new() { Key = StopFields.Poradi,       Header = "Poř.",      Width = 6f,  Visible = false },
        new() { Key = StopFields.JeAktualni,   Header = "Akt.",      Width = 6f,  Visible = false },
        new() { Key = StopFields.Nastupiste,   Header = "Označník", Width = 12f, Visible = false },
        new() { Key = StopFields.Sloupek,      Header = "Sloupek",  Width = 12f, Visible = false },
        // Popis sloupku (StopFields.SloupekPopis) se v nabídce sloupců ZÁMĚRNĚ neobjevuje - nikdo si
        // o něj neřekl a texty z číselníku jsou zčásti provozní („točna", „výstup"), ne pro cestující.
        // Data ho nesou dál, takže jde použít v dynamické tabulce i jako placeholder; do nabídky
        // sloupců ho vrať, teprve když ho někdo bude chtít (rozhodnutí Jan 2026-08-03).
    ];

    // ═══════════════════════════════════════════
    // Řádky (DataRowHeight je na ScheduleElementBase)
    // ═══════════════════════════════════════════
    

    /// <summary>Svislé čáry mezi sloupci (tloušťka, styl a barva = mřížka elementu).
    /// Default vyp = dosavadní vzhled; uvnitř sloupce zastávek rozděleného na značky + název se nekreslí.</summary>
    

    /// <summary>Na kolik oddílů vedle sebe se seznam rozřeže (KOVED #13097), max
    /// <see cref="MaxPartCount"/>. 1 = jeden svislý seznam (dosavadní stav). Každý oddíl nese
    /// **celou tabulku znovu** včetně záhlaví a všech sloupců, jen s další porcí zastávek; řádky
    /// se dělí rovnoměrně, takže prvek je nižší. Šířka sloupců i zmenšování textu se pak počítá
    /// ze šířky oddílu, ne prvku.</summary>
    public int PartCount { get; set; } = 1;

    /// <summary>Mezera mezi oddíly v mm.</summary>
    public float PartGap { get; set; } = 3f;

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

    /// <summary>Jízda kratší než minuta se ve sloupci časů vypíše jako „&lt;1" místo nuly (TRANSDEV,
    /// praxe části dopravců v SR). Default vyp = dnešní chování (nula se netiskne vůbec).
    /// Časy jsou kumulativní od vývěskové zastávky, takže nula pro *jízdu* vzniká jen na zastávkách
    /// hned za vývěskovou; nula za prvním nenulovým časem znamená „chybí údaj", ne jízdu pod minutu -
    /// viz <see cref="MarkSubMinuteCells"/>.</summary>
    public bool ShowSubMinuteTravel { get; set; }

    /// <summary>Strana značek (na znamení, atributy, poznámky) vůči názvu zastávky.</summary>
    /// <remarks>Default AfterName = za názvem zastávky (CBDP layout). BeforeName = před názvem (DPKV layout, např. „× Globus").</remarks>
    public StopMarkersSide MarkersPosition { get; set; } = StopMarkersSide.AfterName;

    /// <summary>Zdroj sloupce „Označník" při více označnících u zastávky (KOVED #13048 bod 4).
    /// FromRoute (default) = z trasy linky (dnešní chování); MostFrequent = označník, odkud jede
    /// nejvíc spojů; All = výčet všech („2,3"). Obojí dle spojů (přednost před hodnotou z trasy).</summary>
    public MultiPlatformDisplay MultiPlatformMode { get; set; } = MultiPlatformDisplay.FromRoute;

    // ═══════════════════════════════════════════
    // Šipka směru jízdy (┌─▼ od aktuální zastávky dolů)
    // ═══════════════════════════════════════════
    /// <summary>Zda se šipka tiskne, řídí **parametr sestavy** `TISK_SIPKY` - agendy JŘ ho posílají
    /// vždy, takže vlastnost prvku by byla mrtvé UI (parametr ji přebije). Tady je jen výchozí
    /// hodnota shodná s parametrem, pro kreslení bez dat.</summary>
    private const bool DefaultShowArrow = true;

    public string DirectionArrowColor { get; set; } = "#FF000000";
    public DirectionArrowSide DirectionArrowPosition { get; set; } = DirectionArrowSide.Right;

    /// <summary>Tloušťka čáry šipky v mm.</summary>
    public float DirectionArrowWidth { get; set; } = DefaultArrowWidthMm;

    /// <summary>Velikost hrotu šipky v mm.</summary>
    public float DirectionArrowHeadSize { get; set; } = DefaultArrowHeadSizeMm;

    /// <summary>Tvar hrotu - plný trojúhelník (dosavadní), otevřené „V", nebo bez hrotu.</summary>
    public ArrowHeadShape DirectionArrowHead { get; set; } = ArrowHeadShape.Filled;

    /// <summary>Styl čáry šipky. Hrot zůstává plnou čarou i u čárkované - jinak se rozpadne na tečky.</summary>
    public LineStyle DirectionArrowLineStyle { get; set; } = LineStyle.Solid;

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

    /// <summary>Barva písma aktuální zastávky; null = dědit z kaskády (podnět #12843 bod 2 -
    /// pozadí šlo nastavit, barvu písma ne).</summary>
    public string? CurrentStopTextColor { get; set; }

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
    /// <summary>Zda se projeté zastávky tisknou, řídí **parametr sestavy** `TISK_PROJETYCH_ZAST`
    /// (viz <see cref="DefaultShowArrow"/> - totéž pravidlo). Barva a čas u projetých níže jsou
    /// vzhled, ten do šablony patří.</summary>
    private const bool DefaultShowPassedStops = false;

    public string PassedStopsForeColor { get; set; } = "#FF999999";

    /// <summary>Čas jízdy u projeté zastávky. Časy jsou kumulativní od vývěskové zastávky, takže
    /// u projetých vycházejí záporné (KOVED #13091: „údaj není relevantní") - default je proto skryje.
    /// Ne-číselné značky (např. „&lt;" = projíždí) zůstávají, nejsou to časy.</summary>
    public PassedStopsTimeDisplay PassedStopsTime { get; set; } = PassedStopsTimeDisplay.Hide;

    /// <summary>Zobrazují se varianty jízdních dob? Řídí to **viditelnost sloupce**
    /// <see cref="StopFields.TravelTimeVariants"/>, ne vlastnost prvku - do 8/2026 to bylo
    /// zaškrtávátko v panelu, což byl pozůstatek doby, kdy varianty sloupec „Čas" nahrazovaly.
    /// Dokud na nich viselo rozvinutí sloupce „Čas", jeho skrytí varianty tiše vyplo.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool ShowsRouteVariants =>
        Columns.Any(c => c.Visible && c.Key == StopFields.TravelTimeVariants);

    // ═══════════════════════════════════════════
    // Vyloučené atributy
    // ═══════════════════════════════════════════
    public List<string> ExcludedAttributeIds { get; set; } = [];

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 60f;

    // ─── Render ─────────────────────────────────────────────────────────────

    /// <summary>Odsazení vertikální čáry šipky od pravého okraje sloupce zastávek (mm) - pro Vpravo.</summary>
    private const float ArrowInsetFromEdgeMm = 0.6f;

    /// <summary>Délka horizontálního bracketu šipky pro Vpravo (mm) - bracket míří doleva do sloupce.</summary>
    private const float ArrowBracketLengthMm = 1.5f;

    /// <summary>Odsazení vertikální čáry šipky doleva mimo sloupec zastávek (mm) - pro Vlevo.</summary>
    /// <remarks>Šipka pro Vlevo jde mimo sloupec, aby se nepřekrývala s left-aligned textem. Bracket pak vede k levé hraně sloupce.</remarks>
    private const float ArrowOutsideOffsetMm = 1.5f;

    /// <summary>Padding sub-cell značek kolem textu - pro vizuální vzdušnost vlevo i vpravo (mm celkem).</summary>
    private const float MarkersSubCellPaddingMm = 1.0f;

    /// <summary>Výchozí tloušťka čáry šipky (mm).</summary>
    /// <remarks>Do 7/2026 se tloušťka zadávala přímo v pixelech (0.8 / 1.5 pro „tučnou") a nešla
    /// nastavit, takže na obrazovce vypadala správně, ale v tisku (vyšší rozlišení) z ní byla
    /// vláskovka - proto milimetry. Hodnota odpovídá dosavadnímu vzhledu při zobrazení 1:1.</remarks>
    public const float DefaultArrowWidthMm = 0.2f;

    /// <summary>Výchozí velikost hrotu šipky (mm).</summary>
    public const float DefaultArrowHeadSizeMm = 1.2f;

    /// <summary>Šířka jednoho oddílu v mm - rámec prvku bez mezer, dělený počtem oddílů.</summary>
    private float PartWidthMm()
    {
        int parts = EffectivePartCount;
        return (Width - (parts - 1) * PartGap) / parts;
    }

    /// <summary>Počet oddílů omezený na rozumný rozsah (0 nebo záporné číslo z šablony = jeden).
    /// Strop 3 je vědomý: každý oddíl zužuje sloupce, u čtyř a víc už se seznam nedá přečíst.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    private int EffectivePartCount => Math.Clamp(PartCount, 1, MaxPartCount);

    /// <summary>Nejvyšší počet oddílů (rozhodnutí Jan 2026-07-31 - víc se na vývěsku nevejde).</summary>
    public const int MaxPartCount = 3;

    /// <summary>Počet řádků na oddíl - dělí se rovnoměrně, poslední oddíl může být kratší.</summary>
    internal static int RowsPerPart(int totalRows, int parts)
    {
        if (parts < 1) parts = 1;
        return (totalRows + parts - 1) / parts;
    }

    /// <summary>Jeden viditelný řádek seznamu se svým stavem - výběr řádků je společný pro
    /// všechny oddíly, geometrie (šířky, pozice šipky) se pak řeší per oddíl.</summary>
    private readonly record struct StopRow(ZastavkaData Z, bool IsCurrent, bool IsPassed);

    /// <summary>Zastávky, které se reálně vykreslí: bez projetých (pokud se netisknou) a bez těch,
    /// kde neplatí podmínka viditelnosti řádku.</summary>
    private List<StopRow> CollectVisibleRows(List<ZastavkaData> zastavky, bool showPassed)
    {
        var rows = new List<StopRow>(zastavky.Count);
        bool foundCurrent = false;
        foreach (var z in zastavky)
        {
            if (!showPassed && !foundCurrent && !z.JeAktualni) continue;
            if (z.JeAktualni) foundCurrent = true;

            bool isCurrent = z.JeAktualni;
            bool isPassed = !foundCurrent && !z.JeAktualni;

            // Podmíněné skrytí řádku (po určení current/passed, ať se nerozbije sledování current).
            if (!RowConditions.IsVisible(RowVisibilityExpression, z.SourceRow)) continue;

            rows.Add(new StopRow(z, isCurrent, isPassed));
        }
        return rows;
    }

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var zastavky = BuildZastavkyProRender(ctx.Data);
        bool hasData = zastavky.Count > 0;

        // Sloupce se musí znát PŘED škálou - vodorovná část škály se počítá z nich (a varianty
        // časů jejich počet mění).
        var columns = GetVisibleColumns();

        // Multi-varianta časů tras: položka „Varianty tras" se rozvine na N variantních sloupců
        // (dynamicky dle dat, hlavičky = písmena z VARIANTY_TRAS). < 2 varianty → položka zmizí,
        // protože jediná varianta je totéž co sloupec „Čas".
        // Vypnutá písmena vyprázdní záhlaví, ale počet hlaviček musí zůstat - drží se na něm
        // rozvinutí sloupce, takže zahozením by zmizely i sloupce.
        columns = ExpandTravelTimeColumns(columns, VariantHeadersForRender(ctx.Data));

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit font. Vodorovně totéž -
        // součet pevných šířek sloupců nad šířku elementu se dřív oříznul (svislé varování
        // to nezachytí). Zmenšuje se tou horší z obou stran.
        // Šířky sloupců i vodorovná škála se počítají ze šířky ODDÍLU, ne prvku - jinak by
        // sloupce z oddílu vylezly (rozdělení na pokračování, KOVED #13097).
        int parts = EffectivePartCount;
        float partWidth = PartWidthMm();
        if (partWidth <= 0) return;

        // Šipka vlevo si nárokuje pruh nalevo od sloupce zastávek. Musí být součástí rozdělení
        // šířek, ne kreslením přes souseda - jinak si po zúžení prvku lehne na jízdní doby
        // (#12843 bod 1). Vpravo se kreslí uvnitř sloupce a rezervu drží nameShrinkReserveMm.
        bool showArrowEarly = GetBoolParam(ctx.Data, "TISK_SIPKY", DefaultShowArrow);
        float arrowLaneMm = showArrowEarly && DirectionArrowPosition == DirectionArrowSide.Left
            ? ArrowOutsideOffsetMm + (MathF.Max(DirectionArrowHeadSize, DirectionArrowWidth) / 2f) + 0.2f
            : 0f;
        float columnsWidthMm = MathF.Max(1f, partWidth - arrowLaneMm);

        float heightScale = hasData ? ComputeFontScale(CalculateHeightFromData(ctx.Data)) : 1f;
        float scale = MathF.Min(heightScale, ComputeWidthScale(columns, columnsWidthMm));
        float baseFontSize = DataStyle.FontSize ?? 8f;
        // Výška záhlaví/řádku musí odpovídat NEJVĚTŠÍMU reálně vykreslenému (per-column resolved)
        // fontu, ne jen element-level fontu. Jinak vzniká asymetrie: per-column font glyf zvětší,
        // ale výška se řídí element fontem (nebo naopak) → výška ≠ text.
        float baseHeaderFontSize = MaxResolvedHeaderFontPt();
        float effectiveFontSize = baseFontSize * scale;
        float effectiveHeaderFontSize = baseHeaderFontSize * scale;

        // TR4: fixed-then-shrink - pevná DataRowHeight se taky zmenší při přetečení (sdílený helper, jako DepartureGrid).
        float rowH = ScaledDataRowHeight(MaxResolvedDataFontPt(), scale);
        float headerH = ComputeHeaderH(effectiveHeaderFontSize) * MaxHeaderLines();

        using var paints = CreateStandardPaints(ctx);

        // Parametry z tiskového dialogu přepíší element properties
        bool showPassed = GetBoolParam(ctx.Data, "TISK_PROJETYCH_ZAST", DefaultShowPassedStops);
        bool showArrow = showArrowEarly;
        bool arrowLeft = arrowLaneMm > 0f;

        // Šipka se vykresluje UVNITŘ sloupce zastávek (Key = NAZEV_ZASTAVKY) - vertikální čára je
        // kousek od vnitřního okraje sloupce, bracket horizontálně směřuje do sloupce.
        // Nepřičítá se nic k šířce layoutu → element a sloupce zachovávají Width.
        // Pro běžně krátké názvy zastávek se text se šipkou nepřekrývá.
        // Fallback (žádný StopName sloupec): poslední sloupec.
        int stopNameIdx = columns.FindIndex(c => c.Key == StopFields.StopName);
        if (stopNameIdx < 0) stopNameIdx = columns.Count - 1;
        var colW = ResolveColumnWidths(columns, columnsWidthMm);
        // Odsazení sloupce zastávek od levé hrany oddílu (per oddíl se přičte jeho X).
        float stopNameOffsetMm = 0f;
        for (int i = 0; i < stopNameIdx && i < columns.Count; i++) stopNameOffsetMm += colW[columns[i]];
        stopNameOffsetMm += arrowLaneMm;   // pruh šipky leží před sloupcem zastávek
        float stopNameWidthMm = stopNameIdx >= 0 && stopNameIdx < columns.Count ? colW[columns[stopNameIdx]] : 0f;

        // Symbolový font pro piktogramové značky (MHD „na znamení" = drátový kód „D" → piktogram).
        var symbolFace = GetTypeface(FontResolver.SymbolFontFamily(ctx.Data?.Culture));

        // Šířka sub-cell pro značky (BeforeName) - auto-fit podle nejširšího marker textu v datech.
        float markersSubCellWidth = MarkersPosition == StopMarkersSide.BeforeName
            ? ComputeMarkersSubCellWidth(zastavky, effectiveFontSize, ctx.MmToPx, symbolFace)
            : 0f;

        // Rezerva „Přizpůsobit buňce" pro sloupec názvů: při šipce vpravo nesmí zmenšený text
        // podlézt její vodorovnou část (svislice + bracket míří do sloupce) - cíl končí před ní.
        float nameShrinkReserveMm = (showArrow && !arrowLeft)
            ? ArrowInsetFromEdgeMm + ArrowBracketLengthMm + 0.5f
            : CellRenderer.DefaultShrinkReserveMm;

        // Clip na hranice elementu - chrání před přesahem, pokud součet šířek sloupců
        // přesahuje Width (nekonzistentní konfigurace). Konzistentní s DepartureGridElement.
        var clipRect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(clipRect);
        try
        {
            var visibleRows = hasData ? CollectVisibleRows(zastavky, showPassed) : [];
            MarkSubMinuteCells(visibleRows, columns);
            int rowsPerPart = RowsPerPart(visibleRows.Count, parts);
            bool currentInPreviousPart = false;

            for (int part = 0; part < parts; part++)
            {
                float partX = X + part * (partWidth + PartGap);
                float stopNameLeftMm = partX + stopNameOffsetMm;

                // Asymetrická pozice šipky:
                //   Vpravo: šipka UVNITŘ sloupce u pravé hrany - text je left-aligned, k pravé hraně obvykle nezasahuje.
                //   Vlevo:  šipka MIMO sloupec (vlevo od jeho levé hrany) - left-aligned text by jinak kolidoval.
                //           Bracket pak vede zpět až k levé hraně sloupce (do prázdné padding-right oblasti předchozího sloupce).
                float arrowLineXMm = arrowLeft
                    ? stopNameLeftMm - ArrowOutsideOffsetMm
                    : stopNameLeftMm + stopNameWidthMm - ArrowInsetFromEdgeMm;
                float bracketColumnsSideMm = arrowLeft
                    ? stopNameLeftMm                         // bracket od šipky doprava až k levé hraně sloupce zastávek
                    : arrowLineXMm - ArrowBracketLengthMm;   // bracket od šipky doleva do sloupce

                // Záhlaví sloupců se v každém oddílu zopakuje - oddíl je celá tabulka znovu.
                float currentY = ShowHeader
                    ? RenderColumnHeaders(ctx, columns, stopNameIdx, markersSubCellWidth, colW, headerH, paints, scale, partX, partWidth, arrowLaneMm)
                    : Y;

                if (visibleRows.Count > 0)
                {
                    int from = part * rowsPerPart;
                    if (from >= visibleRows.Count) break;   // méně řádků než oddílů - zbytek se nekreslí
                    int count = Math.Min(rowsPerPart, visibleRows.Count - from);

                    // Šipka pokračuje: v oddílu s aktuální zastávkou začíná u ní, v dalších od horní hrany.
                    float arrowStartY = currentInPreviousPart ? currentY : -1f;
                    float arrowEndY = -1f;

                    float rowsStartY = currentY;
                    var plan = new List<RowSpec>(count);

                    for (int i = from; i < from + count; i++)
                    {
                        var row = visibleRows[i];
                        if (row.IsCurrent)
                        {
                            arrowStartY = currentY;
                            currentInPreviousPart = true;
                        }
                        arrowEndY = currentY;
                        currentY += rowH;

                        plan.Add(BuildRowSpec(ctx, row, columns, stopNameIdx, colW, markersSubCellWidth,
                            stopNameLeftMm, rowH, scale, symbolFace, nameShrinkReserveMm, paints, arrowLaneMm));
                    }

                    RowPlanRenderer.DrawRows(ctx, partX, rowsStartY, partWidth, plan, scale);

                    // Šipka směru jízdy v rezervovaném gutteru - ┌─▼ (vlevo) nebo ─┐▼ (vpravo)
                    if (showArrow && arrowStartY >= 0 && arrowEndY > arrowStartY)
                        RenderDirectionArrow(ctx, arrowLineXMm, bracketColumnsSideMm, arrowStartY, arrowEndY, rowH);
                }
                else if (ctx.Data is null)
                {
                    // Placeholder se ukazuje jen v designeru. Print s prázdnými daty nesmí
                    // vykreslovat fake zastávky.
                    float placeholderFontPx = effectiveFontSize * PtToMm * ctx.MmToPx;
                    using var placeholderFont = new SKFont(GetTypeface(ResolveColumnDataParent()), placeholderFontPx);
                    RenderPlaceholderRows(ctx, 4, "Zastávka",
                        partX, currentY, partWidth, rowH, placeholderFont, paints.Text, paints.Grid);
                }
            }

            // Outer border je řízen TemplateElement.BorderColor/BorderWidth (druhý průchod).
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    /// <summary>Sestaví jeden datový řádek plánu - podbarvení, čáry a dokreslení značek.</summary>
    private RowSpec BuildRowSpec(
        RenderContext ctx, StopRow row, List<StopColumn> columns, int stopNameIdx,
        Dictionary<StopColumn, float> colW, float markersSubCellWidth, float stopNameLeftMm,
        float rowH, float scale, SKTypeface symbolFace, float nameShrinkReserveMm, SchedulePaints paints,
        float arrowLaneMm)
    {
        var z = row.Z;

        // Podbarvení řádku přes celou šířku oddílu (za sloupci i v gap za posledním).
        // Priorita: podmíněné zvýraznění (uživatelský záměr) > podbarvení aktuální zastávky.
        // Per-column Style.Data.BackgroundColor (pokud je neprůhledná) toto lokálně přepíše.
        string? rowBg =
            RowConditions.HighlightColor(HighlightRules, z.SourceRow)
            ?? (row.IsCurrent ? CurrentStopBackgroundColor : null);

        // „Přizpůsobit buňce" + značky za názvem: zmenšení názvu počítá i s šířkou
        // značek (škálují se spolu), aby se za zmenšený název vešly a nepodlézaly šipku.
        float nameTrailingMm = 0f;
        if (stopNameIdx >= 0 && MarkersPosition == StopMarkersSide.AfterName)
        {
            var nameStyleRow = ResolveRowStyle(columns[stopNameIdx], row.IsCurrent, row.IsPassed);
            nameTrailingMm = MeasureMarkersTrailingMm(ctx, BuildStopMarkerRuns(z), nameStyleRow, symbolFace, scale);
        }

        var specs = BuildDataRowSpecs(columns, z, row.IsCurrent, row.IsPassed, stopNameIdx,
            markersSubCellWidth, colW, nameShrinkReserveMm, nameTrailingMm);
        specs = InsertArrowLane(specs, stopNameIdx, arrowLaneMm);

        return new RowSpec(RowKind.Data, rowH, specs)
        {
            Background = ColorHex.ParseOrNull(rowBg),
            VerticalLinePaint = Grid.ShowColumnLines ? paints.Grid : null,
            LineBelowPaint = Grid.ShowRowLines ? paints.Grid : null,
            // Značky (na znamení / atributy / poznámky) po úsecích: piktogramy symbolovým
            // Timetable fontem (MHD „na znamení" = kód „D"), text a poznámky normálním.
            // V CellRenderu nejde míchat font v jedné buňce (#7161 MHD parita).
            Decorate = rowY => DrawStopMarkers(ctx, z, rowY, rowH, columns[stopNameIdx],
                stopNameLeftMm, colW[columns[stopNameIdx]], markersSubCellWidth,
                row.IsCurrent, row.IsPassed, symbolFace, scale, nameShrinkReserveMm),
        };
    }

    /// <summary>Vykreslí záhlaví sloupců jednoho oddílu přes ColumnRenderer.</summary>
    private float RenderColumnHeaders(
        RenderContext ctx, List<StopColumn> columns, int stopNameIdx, float markersSubCellWidth,
        Dictionary<StopColumn, float> widths, float headerH, SchedulePaints paints, float scale,
        float xMm, float widthMm, float arrowLaneMm)
    {
        // Výchozí pozadí záhlaví (přes celou šířku oddílu).
        // Per-column header bg z kaskády to lokálně přepíše.
        var headerRect = ctx.ToCanvasRect(xMm, Y, widthMm, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        var specs = InsertArrowLane(
            BuildHeaderRowSpecs(columns, stopNameIdx, markersSubCellWidth, widths), stopNameIdx, arrowLaneMm);
        ColumnRenderer.DrawRow(ctx, xMm, Y, headerH, specs,
            Grid.ShowColumnLines ? paints.Grid : null, fontScale: scale);

        // `B150`: s vypnutými písmeny nese blok variantních sloupců JEDEN společný popisek, ne
        // tentýž text v každém sloupci. Kreslí se až po DrawRow přes celý blok - kdyby se slily
        // samotné buňky záhlaví, rozešly by se indexy specs s datovými řádky (pruh šipky).
        DrawVariantGroupHeader(ctx, columns, widths, headerH, xMm + arrowLaneMm, scale);

        // Čára pod záhlavím
        var lineStart = ctx.ToCanvas(xMm, Y + headerH);
        var lineEnd = ctx.ToCanvas(xMm + widthMm, Y + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, paints.Grid);

        return Y + headerH;
    }

    // ─── Style resolution (kaskáda column → element → CellDefaults) ─────────

    /// <summary>Parent kaskády pro per-column header override - co sloupec dědí, když vlastní styl nemá.</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() => HeaderStyle.Resolve(ScheduleHeaderDefaults);

    /// <summary>Parent kaskády pro per-column data override - co sloupec dědí, když vlastní styl nemá.</summary>
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
                TextColor = CurrentStopTextColor is { Length: > 0 } barva ? barva : resolved.TextColor,
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
                    GetCellText(z, col.Key, isPassed),
                    ResolveCellStyle(col, z, isCurrent, isPassed),
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
                // Text značek prázdný - kreslí je DrawStopMarkers zvlášť (per-run font). Sub-cell drží jen šířku a pozadí.
                result[idx++] = new ColumnRenderer.CellSpec(markersSubCellWidth, "", markerStyle);
                result[idx++] = new ColumnRenderer.CellSpec(widths[col] - markersSubCellWidth, GetCellText(z, col.Key, isPassed), nameStyle, nameShrinkReserveMm, nameTrailingMm,
                    SuppressLeadingLine: true);
            }
            else
            {
                result[idx++] = new ColumnRenderer.CellSpec(
                    widths[col],
                    GetCellText(z, col.Key, isPassed),
                    ResolveCellStyle(col, z, isCurrent, isPassed));
            }
        }
        return result;
    }

    /// <summary>Vloží před sloupec zastávek prázdný pruh, ve kterém se kreslí šipka směru jízdy.
    /// Bez něj si šipka půjčuje pravý okraj sousedního sloupce a po zúžení prvku mu leží přes text
    /// (#12843 bod 1). Svislice zůstává na začátku pruhu (tam, kde soused končí), buňka za pruhem
    /// si ji proto potlačí - jinak by vedle sebe byly dvě.</summary>
    private static ColumnRenderer.CellSpec[] InsertArrowLane(ColumnRenderer.CellSpec[] specs, int at, float laneMm)
    {
        if (laneMm <= 0f || at < 0 || at >= specs.Length) return specs;
        var result = new ColumnRenderer.CellSpec[specs.Length + 1];
        Array.Copy(specs, 0, result, 0, at);
        result[at] = new ColumnRenderer.CellSpec(laneMm, "", specs[at].Style);
        result[at + 1] = specs[at] with { SuppressLeadingLine = true };
        Array.Copy(specs, at + 1, result, at + 2, specs.Length - at - 1);
        return result;
    }

    /// <summary>`B150`: společný popisek bloku variantních sloupců, vodorovně na střed přes celý
    /// blok. Kreslí se jen když blok existuje a popisek je neprázdný - tedy s vypnutými písmeny
    /// variant, protože jinak v záhlaví stojí ta písmena.</summary>
    private void DrawVariantGroupHeader(
        RenderContext ctx, List<StopColumn> columns, Dictionary<StopColumn, float> widths,
        float headerH, float xMm, float scale)
    {
        string label = VariantGroupLabel(ctx.Data);
        if (label.Length == 0) return;

        var (blockStartMm, blockWidthMm, first) = VariantBlockSpan(columns, widths);
        if (first is null || blockWidthMm <= 0) return;

        var style = ResolveHeaderStyle(first) with { HAlign = TextHorizontalAlignment.Center };
        CellRenderer.DrawCell(ctx,
            new SKRect(xMm + blockStartMm, Y, xMm + blockStartMm + blockWidthMm, Y + headerH),
            label, style, scale);
    }

    /// <summary>Text společného popisku, nebo prázdno když se kreslit nemá. Se zapnutými písmeny
    /// variant nese záhlaví je, takže by je popisek překryl.</summary>
    internal string VariantGroupLabel(DataContext? data)
    {
        if (GetBoolParam(data, NoteVisibilityFilter.VariantLettersParamName, true)) return "";
        return Columns.FirstOrDefault(c => c.Key == StopFields.TravelTimeVariants)?.Header ?? "";
    }

    /// <summary>Odsazení a šířka bloku variantních sloupců v už rozvinutém seznamu, plus první
    /// z nich (nese styl záhlaví). Blok je souvislý, protože vzniká rozvinutím jedné položky.</summary>
    internal static (float StartMm, float WidthMm, StopColumn? First) VariantBlockSpan(
        List<StopColumn> columns, Dictionary<StopColumn, float> widths)
    {
        float offsetMm = 0f, startMm = 0f, widthMm = 0f;
        StopColumn? first = null;
        foreach (var col in columns)
        {
            if (col.Key.StartsWith(StopFields.TravelTimeVariantPrefix, StringComparison.Ordinal))
            {
                if (first is null) { first = col; startMm = offsetMm; }
                widthMm += widths[col];
            }
            offsetMm += widths[col];
        }
        return (startMm, widthMm, first);
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
                result[idx++] = new ColumnRenderer.CellSpec(widths[col] - markersSubCellWidth, Rendering.ColumnHeader.Resolve(col.Header, ""), headerStyle,
                    SuppressLeadingLine: true);
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
    /// <remarks>
    /// Samotná kresba je ve <see cref="DirectionArrowRenderer"/> - sdílí ji linková tabulka.
    /// Tady zůstává jen vývěsková geometrie: šipka začíná <b>uprostřed písmen</b> aktuální zastávky
    /// a končí kousek nad spodní hranou posledního řádku.
    /// </remarks>
    private void RenderDirectionArrow(
        RenderContext ctx, float lineXMm, float bracketColumnsSideMm,
        float startY, float endY, float rowH) =>
        DirectionArrowRenderer.Draw(ctx, ArrowVzhled(),
            lineXMm, bracketColumnsSideMm,
            startY + rowH * 0.5f,    // uprostřed písmen aktuální zastávky
            endY + rowH * 0.8f);     // kousek nad spodní hranou řádku poslední zastávky

    /// <summary>Vzhled šipky z vlastností prvku.</summary>
    private DirectionArrowRenderer.Vzhled ArrowVzhled() => new(
        DirectionArrowColor, DirectionArrowWidth, DirectionArrowHeadSize,
        DirectionArrowHead, DirectionArrowLineStyle);

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public override float CalculateHeightFromData(DataContext? data)
    {
        var zastavky = BuildZastavkyProRender(data);
        if (zastavky.Count == 0) return DesignerPlaceholderHeight;

        bool showPassed = GetBoolParam(data, "TISK_PROJETYCH_ZAST", DefaultShowPassedStops);
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
            // Aktivní podmínka viditelnosti - spočítat řádky přesně jako v Render (parita výška↔render).
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

        // Stejný zdroj fontu jako v Render() - max resolved per-column, ať scale/overflow sedí.
        float headerH = ShowHeader ? ComputeHeaderH(MaxResolvedHeaderFontPt()) * MaxHeaderLines() : 0f;
        float rowH = DataRowHeight ?? ComputeRowH(MaxResolvedDataFontPt());
        // Rozdělení na pokračování: výška je dána nejdelším oddílem, ne všemi řádky.
        return headerH + RowsPerPart(visibleStops, EffectivePartCount) * rowH;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<StopColumn> GetVisibleColumns() => Columns.Where(c => c.Visible).ToList();

    /// <summary>Vyřeší efektivní šířky sloupců přes sdílené jádro (<see cref="TableLayout.ResolveWidths"/>):
    /// pevné (Width != null) zůstanou, auto (null) si rovnoměrně rozdělí zbytek. StopList je mm-only.
    /// <paramref name="availableWidthMm"/> = šířka oddílu, ne prvku (rozdělení na pokračování).</summary>
    private Dictionary<StopColumn, float> ResolveColumnWidths(List<StopColumn> visible, float availableWidthMm)
    {
        var widths = TableLayout.ResolveWidths(WidthProjection(visible), availableWidthMm);
        var result = new Dictionary<StopColumn, float>(visible.Count);
        for (int i = 0; i < visible.Count; i++) result[visible[i]] = widths[i];
        return result;
    }

    /// <summary>Škála fontů, aby se sloupce vešly do šířky oddílu (1 = vejdou se).</summary>
    private float ComputeWidthScale(List<StopColumn> visible, float availableWidthMm) =>
        TableLayout.ComputeWidthScale(WidthProjection(visible), availableWidthMm);

    private static List<TableLayout.ColWidth> WidthProjection(List<StopColumn> visible) =>
        visible.Select(c => new TableLayout.ColWidth(c.Width)).ToList();

    /// <summary>Největší resolved font záhlaví napříč viditelnými sloupci (pt) - vč. per-column overridů.
    /// Výška záhlaví se počítá z této hodnoty, aby odpovídala reálně vykreslenému textu.</summary>
    private float MaxResolvedHeaderFontPt()
    {
        var cols = GetVisibleColumns();
        return cols.Count > 0 ? cols.Max(c => ResolveHeaderStyle(c).FontSize) : (HeaderStyle.FontSize ?? 9f);
    }

    /// <summary>Nejvyšší počet řádků záhlaví napříč viditelnými sloupci - dvouřádkové záhlaví přes \n
    /// (např. „Zastávka" / „Stop"). Výška záhlaví se násobí tímto počtem, ať se text vejde.</summary>
    private int MaxHeaderLines()
    {
        var cols = GetVisibleColumns();
        int max = 1;
        foreach (var c in cols)
            max = Math.Max(max, Rendering.CellLayout.LineCount(Rendering.ColumnHeader.Resolve(c.Header, "")));
        return max;
    }

    /// <summary>Největší resolved font datové buňky napříč viditelnými sloupci (pt) - vč. per-column overridů.</summary>
    private float MaxResolvedDataFontPt()
    {
        var cols = GetVisibleColumns();
        return cols.Count > 0 ? cols.Max(c => ResolveDataStyle(c).FontSize) : (DataStyle.FontSize ?? 8f);
    }

    /// <summary>Řídí sloupek režimy „více hodnot", řádkové duplikování a dělení stránek místo označníku?
    /// **Odvozené z dat** (parametr sestavy „Číslo stanoviště podle" → `ZDROJ_STANOVISTE`), ne uživatelské
    /// nastavení prvku - proto se neserializuje. Dvě místa, kde se totéž nastavuje, už jsme jednou
    /// odklízeli (`B41`).</summary>
    [JsonIgnore] internal bool SloupekJakoStanoviste { get; set; }

    /// <summary>Zastávky pro render/měření: rozvinuté na stanoviště a se zdrojem stanoviště přečteným
    /// z dat. Jediná cesta, jak si prvek zastávky bere - aby zdroj nemohl chybět v jedné z nich.</summary>
    private List<ZastavkaData> BuildZastavkyProRender(DataContext? data)
    {
        SloupekJakoStanoviste = data?.GetValue("ZDROJ_STANOVISTE")?.ToString() == "Sloupek";
        return ExpandPlatformRows(BuildZastavky(data));
    }

    internal string GetCellText(ZastavkaData z, string columnKey, bool isPassed = false)
    {
        // Variantní časový sub-sloupec „CAS_JIZDY#i" → z.TrasaMin[i] (číslo / „<" / "" jak přišlo z Caché)
        if (columnKey.StartsWith(StopFields.TravelTimeVariantPrefix, System.StringComparison.Ordinal))
        {
            string variant = int.TryParse(columnKey.AsSpan(StopFields.TravelTimeVariantPrefix.Length), out int vi)
                   && vi >= 0 && vi < z.TrasaMin.Count ? z.TrasaMin[vi] : "";
            return FilterPassedTime(SubMinuteOr(FilterCurrentStopTime(variant, z), z, columnKey), isPassed);
        }
        return columnKey switch
        {
            StopFields.StopName     => BuildStopDisplayText(z),
            // Základní i variantní sloupec jdou TOUŽ cestou - je to tentýž údaj a rozdílné zacházení
            // se hned pozná: nula se v „Čase" potlačovala, ve variantách ne, takže zastávka s jízdou
            // pod minutu vypadala v jednom sloupci jako neobsluhovaná a hned vedle měla „0".
            StopFields.TravelTime   => FilterPassedTime(SubMinuteOr(FilterCurrentStopTime(z.CasJizdy, z), z, columnKey), isPassed),
            // Víc zón u jedné zastávky (hraniční) odděluje čárka - shodně se starým modulem.
            StopFields.Zone         => string.Join(",", z.Zony),
            // Režimy „více hodnot" platí pro ten údaj, který je zvolený jako číslo stanoviště;
            // druhý sloupec ukazuje prostou hodnotu z trasy, aby se čísla nemíchala.
            StopFields.Nastupiste   => SloupekJakoStanoviste
                ? z.Nastupiste
                : StanovisteDleRezimu(z.Nastupiste, z.NastupisteSpoju),
            StopFields.Sloupek      => SloupekJakoStanoviste
                ? StanovisteDleRezimu(z.Sloupek, z.SloupekSpoju)
                : z.Sloupek,
            StopFields.SloupekPopis => z.SloupekPopis,
            StopFields.Poradi       => z.Poradi > 0 ? z.Poradi.ToString() : "",
            StopFields.JeAktualni   => z.JeAktualni ? "1" : "",
            _                       => ""
        };
    }

    /// <summary>Hodnota stanoviště podle <see cref="MultiPlatformMode"/>: z trasy / nejčetnější dle spojů /
    /// výčet všech. Bez per-spoj dat vždy fallback na hodnotu z trasy.</summary>
    private string StanovisteDleRezimu(string zTrasy, string dleSpoju) => MultiPlatformMode switch
    {
        MultiPlatformDisplay.MostFrequent => dleSpoju.Length > 0 ? dleSpoju.Split(',')[0] : zTrasy,
        MultiPlatformDisplay.All          => dleSpoju.Length > 0 ? dleSpoju.Replace(",", ", ") : zTrasy,
        _                                 => zTrasy,
    };

    private const string SubMinuteText = "<1";

    /// <summary>Buňky, kde nula ve sloupci časů znamená **skutečnou jízdu pod minutu**, ne chybějící
    /// údaj. Klíč = (řádek, sloupec); prázdné/null = featura vypnutá nebo taková buňka není.</summary>
    [JsonIgnore] private HashSet<(ZastavkaData, string)>? _subMinuteCells;

    /// <summary>Najde buňky s jízdou pod minutu - jednou per render/měření, protože to jde poznat
    /// jen z pořadí řádků. Časy jsou kumulativní od vývěskové zastávky, takže nula pro *jízdu* může
    /// být jen v souvislém běhu hned za ní; **první nenulový čas ten běh ukončí**. Zastávka mimo trasu
    /// dnes posílá prázdno (třístavová hodnota), takže se sem nedostane - běh přesto hlídáme, protože
    /// nula za prvním reálným časem by „&lt;1" ukázala tam, kde má být prázdno.</summary>
    private void MarkSubMinuteCells(List<StopRow> rows, List<StopColumn> columns)
    {
        _subMinuteCells = null;
        if (!ShowSubMinuteTravel) return;

        var cells = new HashSet<(ZastavkaData, string)>();
        foreach (var col in columns)
        {
            if (col.Key != StopFields.TravelTime
                && !col.Key.StartsWith(StopFields.TravelTimeVariantPrefix, System.StringComparison.Ordinal)) continue;

            bool afterCurrent = false;
            foreach (var row in rows)
            {
                if (row.IsCurrent) { afterCurrent = true; continue; }
                if (!afterCurrent || row.IsPassed) continue;
                // „<" / prázdno = varianta tu nestaví; není to čas, takže běh neukončuje ani nezakládá
                if (!int.TryParse(RawTravelTime(row.Z, col.Key), out int minuty)) continue;
                if (minuty != 0) break;
                cells.Add((row.Z, col.Key));
            }
        }
        _subMinuteCells = cells.Count > 0 ? cells : null;
        _subMinuteColumns = cells.Count > 0 ? [.. cells.Select(c => c.Item2)] : null;
    }

    /// <summary>Sloupce, ve kterých v tomhle průchodu nějaké „&lt;1" je - jen v nich se odsazují čísla.</summary>
    [JsonIgnore] private HashSet<string>? _subMinuteColumns;

    /// <summary>Styl buňky datového řádku + **zavěšení „&lt;"**: v časovém sloupci, kde se „&lt;1"
    /// objevilo, se ostatním číslům přidá vlevo odsazení o šířku znaku „&lt;", takže číslice zůstanou
    /// ve svislici a „&lt;" z ní vystupuje doleva. Dělá se to jen u zarovnání vlevo - vpravo se
    /// číslice srovnají samy a odsazení by je rozhodilo.</summary>
    internal ResolvedTextStyle ResolveCellStyle(StopColumn col, ZastavkaData z, bool isCurrent, bool isPassed)
    {
        var style = ResolveRowStyle(col, isCurrent, isPassed);
        if (_subMinuteColumns?.Contains(col.Key) != true) return style;
        if (style.HAlign != TextHorizontalAlignment.Left) return style;
        if (_subMinuteCells?.Contains((z, col.Key)) == true) return style;   // sama „<1" se neodsazuje
        return style with { PaddingLeft = style.PaddingLeft + HangWidthMm(style) };
    }

    /// <summary>Šířka znaku „&lt;" v mm pro daný řez a velikost písma.</summary>
    private static float HangWidthMm(ResolvedTextStyle style)
    {
        using var typeface = GetTypeface(style);
        using var font = new SKFont(typeface, style.FontSize);   // velikost v pt → míra v pt
        return font.MeasureText(SubMinuteText.AsSpan(0, 1)) * PtToMm;
    }

    /// <summary>Totéž, co dělá <see cref="Render"/> před kreslením řádků - sesbírá viditelné řádky
    /// a označí buňky s jízdou pod minutu. Existuje, aby to šlo ověřit bez kreslení na canvas.</summary>
    internal void PrepareSubMinuteCells(List<ZastavkaData> zastavky, bool showPassed, List<StopColumn> columns)
        => MarkSubMinuteCells(CollectVisibleRows(zastavky, showPassed), columns);

    /// <summary>Hodnota časového sloupce, jak přišla z dat - bez náhrady za „&lt;1".</summary>
    private static string RawTravelTime(ZastavkaData z, string columnKey)
    {
        if (!columnKey.StartsWith(StopFields.TravelTimeVariantPrefix, System.StringComparison.Ordinal))
            return z.CasJizdy;
        return int.TryParse(columnKey.AsSpan(StopFields.TravelTimeVariantPrefix.Length), out int vi)
               && vi >= 0 && vi < z.TrasaMin.Count ? z.TrasaMin[vi] : "";
    }

    private string SubMinuteOr(string value, ZastavkaData z, string columnKey)
        => _subMinuteCells?.Contains((z, columnKey)) == true ? SubMinuteText : value;

    /// <summary>Čas na řádku vývěskové zastávky se netiskne - nula tam není údaj, ale definice
    /// („stojíš tady") a vyjde stejně v každém sloupci. Platí pro obě rodiny sloupců, jinak by se
    /// rozešly. Ne-číselnou hodnotu („&lt;") nechává, není to čas.
    ///
    /// <para>⚠️ Mimo tento řádek se nula **tiskne**: prázdno v seznamu znamená „tudy tenhle průběh
    /// nejede", takže potlačená nula dělala ze zastávky s jízdou pod minutu neobsluhovanou
    /// (nález Jan 13. 8., linka 9 Nitra, zastávka Bizetova). Čitelnější tvar dá volba
    /// <see cref="ShowSubMinuteTravel"/>.</para></summary>
    private static string FilterCurrentStopTime(string value, ZastavkaData z) =>
        z.JeAktualni && int.TryParse(value, out _) ? "" : value;

    /// <summary>Čas jízdy u projeté zastávky podle volby <see cref="PassedStopsTime"/>. Ne-číselnou
    /// hodnotu („&lt;" = projíždí) nechává - není to čas, jen značka.</summary>
    private string FilterPassedTime(string value, bool isPassed)
    {
        if (!isPassed || value.Length == 0 || PassedStopsTime == PassedStopsTimeDisplay.Show) return value;
        return int.TryParse(value, out _) ? "" : value;
    }

    private const float VariantColumnWidthMm = 6f;

    /// <summary>Hlavičky variantních časových sloupců z page values (VARIANTY_TRAS). Prázdné = jedna varianta.</summary>
    private static List<string> GetVariantHeaders(DataContext? data)
    {
        if (data?.GetValue("VARIANTY_TRAS") is System.Text.Json.Nodes.JsonArray arr)
            return arr.Select(n => n?.ToString() ?? "").ToList();
        return [];
    }

    /// <summary>Hlavičky pro render: parametr sestavy „Písmena variant tras" je umí vyprázdnit,
    /// ale jejich POČET zůstává - na něm visí, kolik variantních sloupců vznikne.</summary>
    internal static List<string> VariantHeadersForRender(DataContext? data)
    {
        var headers = GetVariantHeaders(data);
        if (GetBoolParam(data, NoteVisibilityFilter.VariantLettersParamName, true)) return headers;
        return headers.Select(_ => "").ToList();
    }

    /// <summary>Rozvine položku „Varianty tras" na N sloupců (Key „CAS_JIZDY#i", Header = písmeno
    /// varianty). Šířka, zarovnání i styl položky platí pro **každý** z nich - je to N zobrazení
    /// téhož údaje, takže se nastavují jednou. Ostatní sloupce beze změny.
    ///
    /// <para>Méně než dvě varianty = položka se zahodí: jediná varianta je totéž co sloupec „Čas"
    /// a prázdný sloupec s písmenem by na vývěsce vypadal jako chyba dat.</para></summary>
    internal static List<StopColumn> ExpandTravelTimeColumns(List<StopColumn> cols, List<string> headers)
    {
        var result = new List<StopColumn>(cols.Count + headers.Count);
        foreach (var col in cols)
        {
            if (col.Key != StopFields.TravelTimeVariants) { result.Add(col); continue; }

            if (headers.Count < 2) continue;

            for (int i = 0; i < headers.Count; i++)
                result.Add(new StopColumn
                {
                    Key = $"{StopFields.TravelTimeVariantPrefix}{i}", Header = headers[i],
                    Width = col.Width ?? VariantColumnWidthMm, Visible = true,
                    Alignment = col.Alignment, Style = col.Style,
                });
        }
        return result;
    }

    /// <summary>Cached excluded set - vytvořen jednou per render pass.</summary>
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
    /// Timetable fontem - bez toho MHD „na znamení" (kód „D") vykreslí surové písmeno „D".
    /// Textové značky a poznámky jdou normálním fontem (AD „x" zůstává „x"). Úseky navzájem
    /// oddělené mezerou (shodně s dřívějším spojeným textem).
    /// </summary>
    private List<MarkerRun> BuildStopMarkerRuns(ZastavkaData z)
    {
        var runs = new List<MarkerRun>();
        void Separator() { if (runs.Count > 0) runs.Add(new MarkerRun(" ", false)); }

        // „Na znamení" přes dedikované pole (mock data) - zobrazovací symbol, normální font.
        if (ShowSignalStopIndicator && z.NaZnameni)
            runs.Add(new MarkerRun(SignalStopSymbol, false));

        // Atributy - každý vlastní úsek (piktogram, pokud jednoznakový a ne textové „x/y/z").
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
                {
                    bool piktogram = JePiktogram(a.Znak);
                    // Přestupová značka se kreslí ze symbolového fontu, kde glyfy nesedí na písmena
                    // číselníku (autobus „A" → „z", vlak „L" → „x"). Ostatní atributy se NEpřekládají -
                    // u MHD je znak v datech už pozice ve fontu, u AD ostatní znaky na svém písmenu sedí.
                    bool prelozit = piktogram && IsTransferAttr(a);
                    runs.Add(new MarkerRun(prelozit ? SymbolGlyph(a.Znak) : a.Znak, piktogram));
                }
            }
        }

        // Poznámky zastávky - textové značky (legenda je má jako „text"), normální font.
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
            // AfterName: za názvem - odměř šířku názvu ve stejném fontu a začni značky za ní.
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

    /// <summary>Šířka bloku značek za názvem (oddělovací mezera + úseky) v mm při dané škále -
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
    /// NaZnameni), symbol se liší podle číselníku firmy (AD „x", MHD např. „D") - rozpoznáváme ho
    /// proto podle popisu (CZ „na znamení" / SK „na znamenie"), aby ho řídil přepínač „Na znamení".</summary>
    private static bool IsNaZnameniAttr(AtributData a) =>
        a.Popis.StartsWith("na znamen", StringComparison.OrdinalIgnoreCase);

    /// <summary>Je atribut zastávky přestup na jiný druh dopravy? V1: vlak „L" / autobus „A"
    /// (číselník 16). Řídí ho přepínač „Přestupy" - nad rámec legacy StopListu.</summary>
    private static bool IsTransferAttr(AtributData a) =>
        a.Znak is "A" or "L";

    /// <summary>Režim „Řádek pro každý označník" (KOVED #13048 bod 4): zastávku obsluhovanou
    /// z více stanovišť rozvine na N řádků na jejím místě v trase (pořadí = první výskyt stanoviště
    /// napříč variantami, netřídí se). Každý řádek nese své stanoviště a časy jen variant, které z něj
    /// jedou. Vývěsková (aktuální) zastávka i zastávka s ≤1 stanovištěm zůstávají jedním řádkem.
    /// Ostatní režimy = beze změny (vrací původní seznam).</summary>
    public List<ZastavkaData> ExpandPlatformRows(List<ZastavkaData> stops)
    {
        if (MultiPlatformMode != MultiPlatformDisplay.RowPerPlatform) return stops;
        bool sl = SloupekJakoStanoviste;
        // Rozpad řádků má smysl jen s variantními časovými sloupci - bez nich by duplikáty nesly
        // identický jediný čas (CasJizdy základní trasy) a vypadaly jako chyba.
        if (!ShowsRouteVariants) return stops;

        var result = new List<ZastavkaData>(stops.Count);
        foreach (var z in stops)
        {
            // Odlišná stanoviště variant, které zde staví - v pořadí prvního výskytu (bez třídění).
            // Jen indexy v rozsahu TrasaMin (obojí server drží stejně dlouhé; guard proti fantom řádku).
            var varianty = sl ? z.SloupekVariant : z.NastupisteVariant;
            var platforms = new List<string>();
            int pocetSloupcu = Math.Min(varianty.Count, z.TrasaMin.Count);
            for (int i = 0; i < pocetSloupcu; i++)
            {
                var p = varianty[i];
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
                result.Add(CloneStopForPlatform(z, plat, keepMarkers: first, sloupek: sl));
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
    private static ZastavkaData CloneStopForPlatform(ZastavkaData z, string platform, bool keepMarkers, bool sloupek)
    {
        var varianty = sloupek ? z.SloupekVariant : z.NastupisteVariant;
        var trasa = new List<string>(z.TrasaMin.Count);
        for (int i = 0; i < z.TrasaMin.Count; i++)
        {
            string nv = i < varianty.Count ? varianty[i] : "";
            string val = z.TrasaMin[i];
            if (nv == platform)
                trasa.Add(val);                           // čas varianty jedoucí z tohoto stanoviště
            else if (keepMarkers && val == "<")
                trasa.Add(val);                           // „projíždí" (mimo stanoviště) - 1× na prvním řádku
            else
                trasa.Add("");
        }
        return new ZastavkaData
        {
            Id               = z.Id,
            Popis            = z.Popis,
            // Základní čas není vázaný na stanoviště (je to doba linky), takže zůstane na prvním
            // řádku - stejně jako značka „projíždí" a značky zastávky. Zopakovaný na každém řádku
            // vypadá jako chyba dat.
            CasJizdy         = keepMarkers ? z.CasJizdy : "",
            Poradi           = z.Poradi,
            JeAktualni       = false,
            Odsazeni         = z.Odsazeni,
            NaZnameni        = keepMarkers && z.NaZnameni,
            Nastupiste       = sloupek ? z.Nastupiste : platform,
            NastupisteSpoju  = z.NastupisteSpoju,
            Sloupek          = sloupek ? platform : z.Sloupek,
            SloupekSpoju     = z.SloupekSpoju,
            SloupekPopis     = z.SloupekPopis,
            SloupekVariant   = z.SloupekVariant,
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
        // (per-run font) - v obou režimech (Before/AfterName). Buňka názvu nese jen název.
        return name;
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetScheduleBaseDescriptors(),
        .. GetCellZoneBaseColorDescriptors(),

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Zastávky", ButtonLabel = "Upravit sloupce…", ActionId = ButtonPropertyDescriptor.ActionOpenStopColumnsEditor },
        new FloatPropertyDescriptor
        {
            FromImage = true, Label = "Rozdělit na více oddílů", Group = "Zastávky",
            Get = () => PartCount, Set = v => PartCount = (int)MathF.Round(v),
            Min = 1, Max = MaxPartCount, Format = "0"
        },
        new FloatPropertyDescriptor
        {
            FromImage = true, Label = "Mezera mezi oddíly", Group = "Zastávky",
            Get = () => PartGap, Set = v => PartGap = v,
            Min = 0, Max = 50, Unit = "mm", Format = "0.##"
        },
        GetDataRowHeightDescriptor(),

        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor   { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new StringPropertyDescriptor { FromImage = true, Label = "Symbol na znamení", Group = "Zastávky", Get = () => SignalStopSymbol,      Set = v => SignalStopSymbol = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Přestupy (vlak/autobus)",Group = "Zastávky", Get = () => ShowTransferAttributes, Set = v => ShowTransferAttributes = v },
        new EnumPropertyDescriptor
        {
            Label = "Více stanovišť", Group = "Zastávky",
            Get = () => MultiPlatformMode,
            Set = v => MultiPlatformMode = (MultiPlatformDisplay)v,
            Options = [
                ("Z trasy linky", (object)MultiPlatformDisplay.FromRoute),
                ("Nejčetnější dle spojů", (object)MultiPlatformDisplay.MostFrequent),
                ("Výčet všech", (object)MultiPlatformDisplay.All),
                ("Řádek pro každé stanoviště", (object)MultiPlatformDisplay.RowPerPlatform)
            ]
        },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },
        new BoolPropertyDescriptor  { Label = "Jízda pod minutu jako <1", Group = "Zastávky", Get = () => ShowSubMinuteTravel, Set = v => ShowSubMinuteTravel = v },
        new EnumPropertyDescriptor
        {
            FromImage = true, Label = "Pozice značek", Group = "Zastávky",
            Get = () => MarkersPosition,
            Set = v => MarkersPosition = (StopMarkersSide)v,
            Options =
            [
                ("Za zastávkou",  (object)StopMarkersSide.AfterName),
                ("Před zastávkou", (object)StopMarkersSide.BeforeName),
            ]
        },

        // „Šipka směru" (zapnutí) tu záměrně není - řídí ji parametr sestavy TISK_SIPKY, který
        // vlastnost prvku přebíjí. Vzhled šipky (barva, tloušťka, hrot, styl) do šablony patří.
        new EnumPropertyDescriptor
        {
            FromImage = true, Label = "Pozice šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowPosition,
            Set = v => DirectionArrowPosition = (DirectionArrowSide)v,
            // „u okraje elementu" v labelu záměrně - šipka se kreslí na hraně elementu, ne u sloupců;
            // u širokého elementu je tedy daleko vpravo a uživateli se zdálo, že se „nic nezměnilo".
            Options =
            [
                ("Vpravo (u pravého okraje elementu)", (object)DirectionArrowSide.Right),
                ("Vlevo (u levého okraje elementu)",   (object)DirectionArrowSide.Left),
            ]
        },
        new ColorPropertyDescriptor { FromImage = true, Label = "Barva šipky",     Group = "Směr jízdy", Get = () => DirectionArrowColor,  Set = v => DirectionArrowColor = v },
        new FloatPropertyDescriptor
        {
            FromImage = true, Label = "Tloušťka šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowWidth, Set = v => DirectionArrowWidth = v,
            Min = 0.05f, Max = 5, Unit = "mm", Format = "0.##"
        },
        new FloatPropertyDescriptor
        {
            FromImage = true, Label = "Velikost hrotu", Group = "Směr jízdy",
            Get = () => DirectionArrowHeadSize, Set = v => DirectionArrowHeadSize = v,
            Min = 0.5f, Max = 20, Unit = "mm", Format = "0.##"
        },
        new EnumPropertyDescriptor
        {
            FromImage = true, Label = "Hrot šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowHead,
            Set = v => DirectionArrowHead = (ArrowHeadShape)v,
            Options =
            [
                ("Plný", (object)ArrowHeadShape.Filled),
                ("Otevřený", (object)ArrowHeadShape.Open),
                ("Bez hrotu", (object)ArrowHeadShape.None),
            ]
        },
        new EnumPropertyDescriptor
        {
            FromImage = true, Label = "Styl čáry šipky", Group = "Směr jízdy",
            Get = () => DirectionArrowLineStyle,
            Set = v => DirectionArrowLineStyle = (LineStyle)v,
            Options =
            [
                ("Plná", (object)LineStyle.Solid),
                ("Čárkovaná", (object)LineStyle.Dashed),
                ("Tečkovaná", (object)LineStyle.Dotted),
            ]
        },

        new BoolPropertyDescriptor  { FromImage = true, Label = "Záhlaví sloupců",      Group = "Záhlaví",    Get = () => ShowHeader,            Set = v => ShowHeader = v },

        new BoolPropertyDescriptor  { FromImage = true, Label = "Podtržení aktuální",  Group = "Zvýraznění", Get = () => CurrentStopUnderline, Set = v => CurrentStopUnderline = v },
        new BoolPropertyDescriptor  { FromImage = true, Label = "Tučně aktuální",      Group = "Zvýraznění", Get = () => CurrentStopBold,      Set = v => CurrentStopBold = v },
        new ColorPropertyDescriptor { FromImage = true, Label = "Barva textu aktuální", Group = "Zvýraznění", Get = () => CurrentStopTextColor ?? "", Set = v => CurrentStopTextColor = string.IsNullOrWhiteSpace(v) ? null : v },
        new FloatPropertyDescriptor { Label = "Velikost aktuální",  Group = "Zvýraznění", Get = () => CurrentStopFontSize ?? (DataStyle.FontSize ?? 8f), Set = v => CurrentStopFontSize = Math.Abs(v - (DataStyle.FontSize ?? 8f)) < 0.01f ? null : v, Min = 4, Max = 200, Unit = "pt" },
        new ColorPropertyDescriptor { FromImage = true, Label = "Pozadí aktuální",     Group = "Zvýraznění", Get = () => CurrentStopBackgroundColor ?? "", Set = v => CurrentStopBackgroundColor = string.IsNullOrEmpty(v) ? null : v },
        // „Projeté zastávky" (zapnutí) tu záměrně není - řídí ji parametr sestavy
        // TISK_PROJETYCH_ZAST. Barva a čas u projetých jsou vzhled, ty do šablony patří.
        new ColorPropertyDescriptor { FromImage = true, Label = "Barva projetých",     Group = "Zvýraznění", Get = () => PassedStopsForeColor, Set = v => PassedStopsForeColor = v },
        new EnumPropertyDescriptor
        {
            Label = "Čas u projetých", Group = "Zvýraznění",
            Get = () => PassedStopsTime,
            Set = v => PassedStopsTime = (PassedStopsTimeDisplay)v,
            Options =
            [
                ("Nezobrazovat", (object)PassedStopsTimeDisplay.Hide),
                ("Zobrazit", (object)PassedStopsTimeDisplay.Show),
            ]
        },

        RowConditionDescriptors.RowVisibility(() => RowVisibilityExpression, v => RowVisibilityExpression = v),

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);   // DataRowHeight + Header/Data styl (font+padding) škáluje base
        if (CurrentStopFontSize is not null) CurrentStopFontSize *= scale;
        DirectionArrowWidth *= scale;
        DirectionArrowHeadSize *= scale;
        PartGap *= scale;
        foreach (var col in Columns)
        {
            if (col.Width is float w) col.Width = w * scale;   // auto (null) se neškáluje
            col.Style?.ApplyScale(scale);                       // per-sloupcové font/padding overridy
        }
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new StopListElement
        {
            PartCount               = PartCount,
            PartGap                 = PartGap,
            Columns                 = Columns.Select(c => c.Clone()).ToList(),
            DirectionArrowColor     = DirectionArrowColor,
            DirectionArrowPosition  = DirectionArrowPosition,
            DirectionArrowWidth     = DirectionArrowWidth,
            DirectionArrowHeadSize  = DirectionArrowHeadSize,
            DirectionArrowHead      = DirectionArrowHead,
            DirectionArrowLineStyle = DirectionArrowLineStyle,
            ShowStopIndentation     = ShowStopIndentation,
            ShowSignalStopIndicator = ShowSignalStopIndicator,
            SignalStopSymbol        = SignalStopSymbol,
            ShowStopAttributes      = ShowStopAttributes,
            ShowTransferAttributes  = ShowTransferAttributes,
            ShowStopNotes           = ShowStopNotes,
            ShowSubMinuteTravel     = ShowSubMinuteTravel,
            MarkersPosition         = MarkersPosition,
            MultiPlatformMode       = MultiPlatformMode,
            CurrentStopUnderline    = CurrentStopUnderline,
            PassedStopsForeColor    = PassedStopsForeColor,
            PassedStopsTime         = PassedStopsTime,
            ExcludedAttributeIds    = [.. ExcludedAttributeIds],
            ShowHeader              = ShowHeader,
            CurrentStopBold         = CurrentStopBold,
            CurrentStopFontSize     = CurrentStopFontSize,
            CurrentStopBackgroundColor = CurrentStopBackgroundColor,
            CurrentStopTextColor = CurrentStopTextColor,
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

/// <summary>Zdroj sloupce „Označník" při více označnících u zastávky (KOVED #13048 bod 4).</summary>
public enum MultiPlatformDisplay
{
    /// <summary>Z trasy linky (J-věta) - dnešní chování, default.</summary>
    FromRoute,
    /// <summary>Označník, odkud jede nejvíc spojů (per-spoj hodnota má přednost před trasou).</summary>
    MostFrequent,
    /// <summary>Výčet všech označníků dle spojů („2, 3"), seřazený dle četnosti.</summary>
    All,
    /// <summary>Řádek pro každý označník: zastávka obsluhovaná z více označníků se vypíše jako N řádků
    /// na svém místě v trase; časy jen variant daného označníku (KOVED #13048 bod 4).</summary>
    RowPerPlatform,
}

/// <summary>Tvar hrotu šipky směru jízdy.</summary>
public enum ArrowHeadShape
{
    /// <summary>Plný trojúhelník (výchozí).</summary>
    Filled,
    /// <summary>Otevřené „V" ze dvou čar.</summary>
    Open,
    /// <summary>Bez hrotu - jen čára.</summary>
    None,
}

/// <summary>Co se u projeté zastávky ukáže ve sloupci času jízdy.</summary>
/// <remarks>Samostatná volba „skrýt jen záporné" tu byla a padla: časy jsou kumulativní od
/// vývěskové zastávky, takže u projeté zastávky jiný než záporný čas nevznikne a chovala se
/// shodně s <see cref="Hide"/>.</remarks>
public enum PassedStopsTimeDisplay
{
    /// <summary>U projetých zastávek nezobrazí žádný čas (výchozí) - značka „projíždí" zůstává.</summary>
    Hide,
    /// <summary>Zobrazí čas tak, jak přišel z dat (chování do 7/2026, tedy i záporný).</summary>
    Show,
}

/// <summary>Strana značek (na znamení, atributy, poznámky) vůči názvu zastávky.</summary>
public enum StopMarkersSide
{
    /// <summary>Značky za názvem zastávky (default, CBDP layout).</summary>
    AfterName,
    /// <summary>Značky před názvem zastávky (DPKV layout, např. „× Globus").</summary>
    BeforeName
}
