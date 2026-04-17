using SkiaSharp;

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
    // Řádky
    // ═══════════════════════════════════════════
    public float? DataRowHeight { get; set; }
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

    // ═══════════════════════════════════════════
    // Záhlaví sloupců
    // ═══════════════════════════════════════════
    public bool ShowHeader { get; set; } = true;

    // ═══════════════════════════════════════════
    // Zvýraznění aktuální zastávky
    // ═══════════════════════════════════════════
    public bool HighlightCurrentStop { get; set; } = true;
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

        using var paints = CreateStandardPaints();

        float fontPx = effectiveFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = effectiveHeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(GetTypeface(DataStyle.FontFamily), fontPx);
        using var headerFont = new SKFont(
            GetTypeface(DataStyle.FontFamily, SKFontStyle.Bold), headerFontPx);
        // Font pro aktuální zastávku — konfigurovatelná velikost + tučnost
        float currentFontSize = (CurrentStopFontSize ?? baseFontSize) * scale;
        float currentFontPx = currentFontSize * PtToMm * ctx.MmToPx;
        var currentStyle = CurrentStopBold ? SKFontStyle.Bold : SKFontStyle.Normal;
        using var currentStopFont = new SKFont(
            GetTypeface(DataStyle.FontFamily, currentStyle), currentFontPx);

        // Per-column resources — CellStyleResources drží font/textPaint/bgPaint pro header i data
        var columns = GetVisibleColumns();
        var headerCells = new Dictionary<string, CellStyleResources>();
        var dataCells = new Dictionary<string, CellStyleResources>();

        try
        {
        foreach (var col in columns)
        {
            dataCells[col.Key] = BuildCellStyle(col.Style?.Data, baseFontSize, false, scale, ctx.MmToPx);
            headerCells[col.Key] = BuildCellStyle(col.Style?.Header, baseHeaderFontSize, true, scale, ctx.MmToPx);
        }

        // Záhlaví sloupců
        float currentY = ShowHeader
            ? RenderColumnHeaders(ctx, columns, headerH, headerFont, paints, headerCells)
            : Y;

        // Parametry z tiskového dialogu přepíší element properties
        bool showPassed = GetBoolParam(ctx.Data, "TISK_PROJETYCH_ZAST", ShowPassedStops);
        bool showArrow = GetBoolParam(ctx.Data, "TISK_SIPKY", ShowDirectionArrow);

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

                float colX = X;
                bool isCurrent = z.JeAktualni && HighlightCurrentStop;
                bool isPassed = !foundCurrent && !z.JeAktualni;

                // Sledovat Y pro šipku směru
                if (isCurrent) arrowStartY = currentY;
                arrowEndY = currentY;

                // Podbarvení aktuálního řádku
                if (isCurrent && CurrentStopBackgroundColor is not null)
                {
                    var bgRect = ctx.ToCanvasRect(X, currentY, Width, rowH);
                    using var bgPaint = new SKPaint { Color = SKColor.Parse(CurrentStopBackgroundColor) };
                    ctx.Canvas.DrawRect(bgRect, bgPaint);
                }

                var defaultRowFont = isCurrent ? currentStopFont : font;
                var rowColor = isPassed
                    ? SKColor.Parse(PassedStopsForeColor)
                    : new SKColor(0xFF222222);
                paints.Text.Color = rowColor;

                foreach (var col in columns)
                {
                    var cell = dataCells[col.Key];

                    RenderCellBackground(ctx, cell.BgPaint, colX, currentY, col.Width, rowH);

                    // Per-column font (fallback na default) — neaplikovat na aktuální zastávku (ta má vlastní font)
                    var rowFont = !isCurrent && cell.Font is not null ? cell.Font : defaultRowFont;
                    // Per-column text color — neaplikovat na projeté zastávky (mají šedou)
                    var textPaint = !isPassed && cell.TextPaint is not null ? cell.TextPaint : paints.Text;

                    string cellText = GetCellText(z, col.Key);
                    float textX = AlignTextX(col, colX, cellText, rowFont, ctx.MmToPx);
                    var pt = ctx.ToCanvas(textX, currentY + rowH * TextBaselineFactor);
                    ctx.Canvas.DrawText(cellText, pt.X, pt.Y, rowFont, textPaint);

                    if (isCurrent && CurrentStopUnderline && cellText.Length > 0)
                    {
                        float textW = rowFont.MeasureText(cellText);
                        using var ulPaint = new SKPaint { Color = rowColor, IsStroke = true, StrokeWidth = 1f };
                        ctx.Canvas.DrawLine(pt.X, pt.Y + 1, pt.X + textW, pt.Y + 1, ulPaint);
                    }

                    colX += col.Width;
                }

                if (ShowRowSeparators)
                {
                    var rowLineStart = ctx.ToCanvas(X, currentY + rowH);
                    var rowLineEnd = ctx.ToCanvas(X + Width, currentY + rowH);
                    ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
                }
                currentY += rowH;
            }

            // Šipka směru jízdy ┌─▼
            if (showArrow && arrowStartY >= 0 && arrowEndY > arrowStartY)
                RenderDirectionArrow(ctx, columns, arrowStartY, arrowEndY, rowH);
        }
        else
        {
            currentY = RenderPlaceholderRows(ctx, 4, "Zastávka",
                X, currentY, Width, rowH, font, paints.Text, paints.Grid);
        }

        RenderOuterBorder(ctx, X, Y, Width, currentY - Y, paints.Grid);

        } // try
        finally
        {
            DisposeAll(headerCells);
            DisposeAll(dataCells);
        }
    }

    /// <summary>Vykreslí záhlaví sloupců tabulky zastávek.</summary>
    private float RenderColumnHeaders(
        RenderContext ctx, List<StopColumn> columns,
        float headerH, SKFont headerFont, SchedulePaints paints,
        Dictionary<string, CellStyleResources> headerCells)
    {
        // Výchozí pozadí záhlaví
        var headerRect = ctx.ToCanvasRect(X, Y, Width, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        float colX = X;
        foreach (var col in columns)
        {
            var cell = headerCells[col.Key];
            RenderCellBackground(ctx, cell.BgPaint, colX, Y, col.Width, headerH);

            var colFont = cell.Font ?? headerFont;
            var colPaint = cell.TextPaint ?? paints.HeaderText;

            float textX = AlignTextX(col, colX, col.Header, colFont, ctx.MmToPx);
            var pt = ctx.ToCanvas(textX, Y + headerH * HeaderBaselineFactor);
            ctx.Canvas.DrawText(col.Header, pt.X, pt.Y, colFont, colPaint);
            colX += col.Width;
        }

        var lineStart = ctx.ToCanvas(X, Y + headerH);
        var lineEnd = ctx.ToCanvas(X + Width, Y + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, paints.Grid);

        return Y + headerH;
    }

    // ─── Šipka směru jízdy ──────────────────────────────────────────────────

    /// <summary>
    /// Vykreslí šipku ┌─▼ mezi sloupci — od aktuální zastávky dolů k poslední.
    /// Pozice X: levý okraj sloupce StopName (nebo první viditelný sloupec).
    /// </summary>
    private void RenderDirectionArrow(
        RenderContext ctx, List<StopColumn> columns,
        float startY, float endY, float rowH)
    {
        // Najít X pozici sloupce StopName
        float arrowX = X;
        float colX = X;
        foreach (var col in columns)
        {
            if (col.Key == "StopName") { arrowX = colX; break; }
            colX += col.Width;
        }
        // Šipka je 1.5mm vlevo od sloupce StopName (mezera mezi zastávkami a šipkou)
        float lineXMm = arrowX - 1.5f;
        // Horizontální úsečka: od lineX doprava o 1.5mm (směrem k zastávkám = ke StopName)
        float bracketRightMm = lineXMm + 1.5f;

        float strokeW = DirectionArrowBold ? 1.5f : 0.8f;
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(DirectionArrowColor),
            IsStroke = true,
            StrokeWidth = strokeW,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Souřadnice v px
        float topY = startY + rowH * 0.2f;       // kousek pod horní hranou řádku
        float bottomY = endY + rowH * 0.8f;       // kousek nad spodní hranou řádku
        var lineTop = ctx.ToCanvas(lineXMm, topY);
        var lineBottom = ctx.ToCanvas(lineXMm, bottomY);
        var bracketRight = ctx.ToCanvas(bracketRightMm, topY);

        // ┐ — horizontální čára doprava (od zastávek) + vertikální dolů
        ctx.Canvas.DrawLine(bracketRight, lineTop, paint);
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

    /// <summary>Vypočítá X pozici textu v mm podle zarovnání sloupce.</summary>
    private static float AlignTextX(StopColumn col, float colX, string text, SKFont font, float mmToPx)
    {
        if (col.Alignment == TextHorizontalAlignment.Left)
            return colX + 1f;

        float textWMm = font.MeasureText(text) / mmToPx;
        float x = col.Alignment == TextHorizontalAlignment.Right
            ? colX + col.Width - textWMm - 0.5f
            : colX + (col.Width - textWMm) / 2f;
        return Math.Max(x, colX + 0.5f);
    }

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

        new ButtonPropertyDescriptor { Label = "Nastavení zastávek", Group = "Zastávky", ButtonLabel = "Upravit...", ActionId = ButtonPropertyDescriptor.ActionOpenStopColumnsEditor },

        new FloatPropertyDescriptor { Label = "Výška řádku (mm)",  Group = "Řádky",    Get = () => DataRowHeight ?? ComputeRowH(DataStyle.FontSize ?? 8f), Set = v => DataRowHeight = Math.Abs(v - ComputeRowH(DataStyle.FontSize ?? 8f)) < 0.01f ? null : v, Min = 1, Max = 50, Unit = "mm" },
        new BoolPropertyDescriptor  { Label = "Oddělovací čáry",   Group = "Řádky",    Get = () => ShowRowSeparators,     Set = v => ShowRowSeparators = v },

        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor   { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new StringPropertyDescriptor { Label = "Symbol na znamení", Group = "Zastávky", Get = () => SignalStopSymbol,      Set = v => SignalStopSymbol = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },

        new BoolPropertyDescriptor  { Label = "Šipka směru",      Group = "Směr jízdy", Get = () => ShowDirectionArrow,    Set = v => ShowDirectionArrow = v },
        new ColorPropertyDescriptor { Label = "Barva šipky",     Group = "Směr jízdy", Get = () => DirectionArrowColor,  Set = v => DirectionArrowColor = v },
        new BoolPropertyDescriptor  { Label = "Tučná šipka",     Group = "Směr jízdy", Get = () => DirectionArrowBold,   Set = v => DirectionArrowBold = v },

        new BoolPropertyDescriptor  { Label = "Záhlaví sloupců",      Group = "Záhlaví",    Get = () => ShowHeader,            Set = v => ShowHeader = v },

        new BoolPropertyDescriptor  { Label = "Zvýraznit aktuální",  Group = "Zvýraznění", Get = () => HighlightCurrentStop, Set = v => HighlightCurrentStop = v },
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
            ShowStopIndentation     = ShowStopIndentation,
            ShowSignalStopIndicator = ShowSignalStopIndicator,
            SignalStopSymbol        = SignalStopSymbol,
            ShowStopAttributes      = ShowStopAttributes,
            ShowStopNotes           = ShowStopNotes,
            HighlightCurrentStop    = HighlightCurrentStop,
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
