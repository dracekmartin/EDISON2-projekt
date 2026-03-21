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
        new() { Key = "Tc",           Header = "TČ",       Width = 8f,  Visible = false },
        new() { Key = "StopName",     Header = "Zastávka",  Width = 50f, Visible = true },
        new() { Key = "TravelTime",   Header = "Čas",       Width = 8f,  Visible = true },
        new() { Key = "Zone",         Header = "Zóna",      Width = 12f, Visible = false },
        new() { Key = "RouteLetters", Header = "Trasa",     Width = 8f,  Visible = false },
        new() { Key = "BaseRoute",    Header = "ZT",        Width = 6f,  Visible = false },
    ];

    // ═══════════════════════════════════════════
    // Zobrazení zastávek (nesouvisí se sloupci)
    // ═══════════════════════════════════════════
    public bool ShowStopIndentation { get; set; } = true;
    public bool ShowSignalStopIndicator { get; set; } = true;
    public string SignalStopSymbol { get; set; } = "×";
    public bool ShowStopAttributes { get; set; } = true;
    public bool ShowStopNotes { get; set; } = true;

    // ═══════════════════════════════════════════
    // Zvýraznění aktuální zastávky
    // ═══════════════════════════════════════════
    public bool HighlightCurrentStop { get; set; } = true;
    public bool CurrentStopUnderline { get; set; } = true;

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
        float effectiveFontSize = FontSize * scale;
        float effectiveHeaderFontSize = HeaderFontSize * scale;

        float rowH = ComputeRowH(effectiveFontSize);
        float headerH = ComputeHeaderH(effectiveHeaderFontSize);

        using var paints = CreateStandardPaints();

        float fontPx = effectiveFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = effectiveHeaderFontSize * PtToMm * ctx.MmToPx;

        using var font = new SKFont(SKTypeface.FromFamilyName(FontFamily), fontPx);
        using var headerFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerFontPx);
        using var boldFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), fontPx);

        // Záhlaví sloupců
        var columns = GetVisibleColumns();
        float currentY = RenderColumnHeaders(ctx, columns, headerH, headerFont, paints);

        // Řádky zastávek — parametr TISK_PROJETYCH_ZAST přepíše ShowPassedStops
        bool showPassed = GetBoolParam(ctx.Data, "TISK_PROJETYCH_ZAST", ShowPassedStops);

        if (hasData)
        {
            bool foundCurrent = false;
            foreach (var z in zastavky)
            {
                if (!showPassed && !foundCurrent && !z.JeAktualni)
                    continue;
                if (z.JeAktualni) foundCurrent = true;

                float colX = X;
                bool isCurrent = z.JeAktualni && HighlightCurrentStop;
                bool isPassed = !foundCurrent && !z.JeAktualni;

                var rowFont = isCurrent ? boldFont : font;
                var rowColor = isPassed
                    ? SKColor.Parse(PassedStopsForeColor)
                    : new SKColor(0xFF222222);
                paints.Text.Color = rowColor;

                foreach (var col in columns)
                {
                    string cellText = GetCellText(z, col.Key);
                    float textX = AlignTextX(col, colX, cellText, rowFont, ctx.MmToPx);
                    var pt = ctx.ToCanvas(textX, currentY + rowH * TextBaselineFactor);
                    ctx.Canvas.DrawText(cellText, pt.X, pt.Y, rowFont, paints.Text);

                    if (isCurrent && CurrentStopUnderline && cellText.Length > 0)
                    {
                        float textW = rowFont.MeasureText(cellText);
                        using var ulPaint = new SKPaint { Color = rowColor, IsStroke = true, StrokeWidth = 1f };
                        ctx.Canvas.DrawLine(pt.X, pt.Y + 1, pt.X + textW, pt.Y + 1, ulPaint);
                    }

                    colX += col.Width;
                }

                var rowLineStart = ctx.ToCanvas(X, currentY + rowH);
                var rowLineEnd = ctx.ToCanvas(X + Width, currentY + rowH);
                ctx.Canvas.DrawLine(rowLineStart, rowLineEnd, paints.Grid);
                currentY += rowH;
            }
        }
        else
        {
            currentY = RenderPlaceholderRows(ctx, 4, "Zastávka",
                X, currentY, Width, rowH, font, paints.Text, paints.Grid);
        }

        RenderOuterBorder(ctx, X, Y, Width, currentY - Y, paints.Grid);
    }

    /// <summary>Vykreslí záhlaví sloupců tabulky zastávek.</summary>
    private float RenderColumnHeaders(
        RenderContext ctx, List<StopColumn> columns,
        float headerH, SKFont headerFont, SchedulePaints paints)
    {
        var headerRect = ctx.ToCanvasRect(X, Y, Width, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        float colX = X;
        foreach (var col in columns)
        {
            float textX = AlignTextX(col, colX, col.Header, headerFont, ctx.MmToPx);
            var pt = ctx.ToCanvas(textX, Y + headerH * HeaderBaselineFactor);
            ctx.Canvas.DrawText(col.Header, pt.X, pt.Y, headerFont, paints.HeaderText);
            colX += col.Width;
        }

        var lineStart = ctx.ToCanvas(X, Y + headerH);
        var lineEnd = ctx.ToCanvas(X + Width, Y + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, paints.Grid);

        return Y + headerH;
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

        return ComputeHeaderH(HeaderFontSize) + visibleStops * ComputeRowH(FontSize);
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

        new ButtonPropertyDescriptor { Label = "Sloupce", Group = "Zastávky", ButtonLabel = "Upravit sloupce..." },

        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor   { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new StringPropertyDescriptor { Label = "Symbol na znamení", Group = "Zastávky", Get = () => SignalStopSymbol,      Set = v => SignalStopSymbol = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },

        new BoolPropertyDescriptor  { Label = "Zvýraznit aktuální",  Group = "Zvýraznění", Get = () => HighlightCurrentStop, Set = v => HighlightCurrentStop = v },
        new BoolPropertyDescriptor  { Label = "Podtržení aktuální",  Group = "Zvýraznění", Get = () => CurrentStopUnderline, Set = v => CurrentStopUnderline = v },
        new BoolPropertyDescriptor  { Label = "Projeté zastávky",    Group = "Zvýraznění", Get = () => ShowPassedStops,      Set = v => ShowPassedStops = v },
        new ColorPropertyDescriptor { Label = "Barva projetých",     Group = "Zvýraznění", Get = () => PassedStopsForeColor, Set = v => PassedStopsForeColor = v },

        .. GetPositionDescriptors()
    ];

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new StopListElement
        {
            Id = Guid.NewGuid(),
            X = X, Y = Y, Width = Width, Height = Height,
            ZIndex = ZIndex, IsLocked = IsLocked,
            VisibilityExpression = VisibilityExpression,
            CustomProperties = CustomProperties is null ? null : new(CustomProperties),
            GroupId = GroupId,
            BorderColor = BorderColor, BorderWidth = BorderWidth,

            Columns                 = Columns.Select(c => c.Clone()).ToList(),
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
        };
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
