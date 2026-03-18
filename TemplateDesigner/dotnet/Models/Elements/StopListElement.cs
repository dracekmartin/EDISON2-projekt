using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Tabulka zastávek (levý sloupec zastávkového JŘ).
/// Zobrazuje: TČ, název zastávky, čas jízdy, zóna, průběhy, atributy, poznámky.
/// Height je vždy null (dynamický — výšku počítá engine z počtu zastávek).
/// Uživatel umístí na canvas vedle DepartureGridElement(ů).
/// </summary>
public class StopListElement : ScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Sloupce
    // ═══════════════════════════════════════════
    public bool ShowStopNameColumn { get; set; } = true;
    public float StopNameColumnWidth { get; set; } = 50f;
    public bool ShowStopIndentation { get; set; } = true;
    public bool ShowTravelTimeColumn { get; set; } = true;
    public float TravelTimeColumnWidth { get; set; } = 8f;
    public bool ShowTcColumn { get; set; }
    public float TcColumnWidth { get; set; } = 8f;
    public bool ShowZoneColumn { get; set; }
    public float ZoneColumnWidth { get; set; } = 12f;
    public bool ShowRouteLettersColumn { get; set; }
    public float RouteLettersColumnWidth { get; set; } = 8f;
    public bool ShowBaseRouteColumn { get; set; }
    public float BaseRouteColumnWidth { get; set; } = 6f;
    public bool ShowSignalStopIndicator { get; set; } = true;

    // ═══════════════════════════════════════════
    // Atributy a poznámky u zastávek
    // ═══════════════════════════════════════════
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

        float rowH = ComputeRowH(FontSize);
        float headerH = ComputeHeaderH(HeaderFontSize);

        using var paints = CreateStandardPaints();

        float fontPx = FontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = HeaderFontSize * PtToMm * ctx.MmToPx;

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

                foreach (var (label, width) in columns)
                {
                    string cellText = GetCellText(z, label);
                    var pt = ctx.ToCanvas(colX + 1f, currentY + rowH * TextBaselineFactor);
                    ctx.Canvas.DrawText(cellText, pt.X, pt.Y, rowFont, paints.Text);

                    if (isCurrent && CurrentStopUnderline && cellText.Length > 0)
                    {
                        float textW = rowFont.MeasureText(cellText);
                        using var ulPaint = new SKPaint { Color = rowColor, IsStroke = true, StrokeWidth = 1f };
                        ctx.Canvas.DrawLine(pt.X, pt.Y + 1, pt.X + textW, pt.Y + 1, ulPaint);
                    }

                    colX += width;
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
        RenderContext ctx, List<(string Label, float Width)> columns,
        float headerH, SKFont headerFont, SchedulePaints paints)
    {
        var headerRect = ctx.ToCanvasRect(X, Y, Width, headerH);
        ctx.Canvas.DrawRect(headerRect, paints.HeaderBg);

        float colX = X;
        foreach (var (label, width) in columns)
        {
            var pt = ctx.ToCanvas(colX + 1f, Y + headerH * HeaderBaselineFactor);
            ctx.Canvas.DrawText(label, pt.X, pt.Y, headerFont, paints.HeaderText);
            colX += width;
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

    private List<(string Label, float Width)> GetVisibleColumns()
    {
        var cols = new List<(string, float)>();
        if (ShowTcColumn)             cols.Add(("TČ", TcColumnWidth));
        if (ShowStopNameColumn)       cols.Add(("Zastávka", StopNameColumnWidth));
        if (ShowTravelTimeColumn)     cols.Add(("Čas", TravelTimeColumnWidth));
        if (ShowZoneColumn)           cols.Add(("Zóna", ZoneColumnWidth));
        if (ShowRouteLettersColumn)   cols.Add(("Trasa", RouteLettersColumnWidth));
        if (ShowBaseRouteColumn)      cols.Add(("ZT", BaseRouteColumnWidth));
        return cols;
    }

    private string GetCellText(ZastavkaData z, string columnLabel) => columnLabel switch
    {
        "TČ"       => z.TarifniCislo,
        "Zastávka" => BuildStopDisplayText(z),
        "Čas"      => z.CasJizdy > 0 ? z.CasJizdy.ToString() : "",
        "Zóna"     => string.Join("/", z.Zony),
        "Trasa"    => string.Join("", z.Prubehy),
        "ZT"       => z.ZakladniTrasa ? "1" : "",
        _          => ""
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
            text += " ×";

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
        new BoolPropertyDescriptor  { Label = "Název zastávky",    Group = "Zastávky", Get = () => ShowStopNameColumn,     Set = v => ShowStopNameColumn = v },
        new FloatPropertyDescriptor { Label = "Šířka zastávky",    Group = "Zastávky", Get = () => StopNameColumnWidth,    Set = v => StopNameColumnWidth = v,    Min = 10, Max = 200, Unit = "mm" },
        new BoolPropertyDescriptor  { Label = "Odsazení",          Group = "Zastávky", Get = () => ShowStopIndentation,    Set = v => ShowStopIndentation = v },
        new BoolPropertyDescriptor  { Label = "Čas jízdy",         Group = "Zastávky", Get = () => ShowTravelTimeColumn,   Set = v => ShowTravelTimeColumn = v },
        new BoolPropertyDescriptor  { Label = "Tarifní číslo",     Group = "Zastávky", Get = () => ShowTcColumn,           Set = v => ShowTcColumn = v },
        new BoolPropertyDescriptor  { Label = "Zóna",              Group = "Zastávky", Get = () => ShowZoneColumn,         Set = v => ShowZoneColumn = v },
        new BoolPropertyDescriptor  { Label = "Průběhy",           Group = "Zastávky", Get = () => ShowRouteLettersColumn, Set = v => ShowRouteLettersColumn = v },
        new BoolPropertyDescriptor  { Label = "Na znamení",        Group = "Zastávky", Get = () => ShowSignalStopIndicator,Set = v => ShowSignalStopIndicator = v },
        new BoolPropertyDescriptor  { Label = "Atributy",          Group = "Zastávky", Get = () => ShowStopAttributes,     Set = v => ShowStopAttributes = v },
        new BoolPropertyDescriptor  { Label = "Poznámky zastávky", Group = "Zastávky", Get = () => ShowStopNotes,          Set = v => ShowStopNotes = v },

        new BoolPropertyDescriptor  { Label = "Zvýraznit aktuální",  Group = "Zvýraznění", Get = () => HighlightCurrentStop, Set = v => HighlightCurrentStop = v },
        new BoolPropertyDescriptor  { Label = "Podtržení aktuální",  Group = "Zvýraznění", Get = () => CurrentStopUnderline, Set = v => CurrentStopUnderline = v },
        new BoolPropertyDescriptor  { Label = "Projeté zastávky",    Group = "Zvýraznění", Get = () => ShowPassedStops,      Set = v => ShowPassedStops = v },
        new ColorPropertyDescriptor { Label = "Barva projetých",     Group = "Zvýraznění", Get = () => PassedStopsForeColor, Set = v => PassedStopsForeColor = v },

        .. GetScheduleBaseDescriptors(),
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

            ShowStopNameColumn      = ShowStopNameColumn,
            StopNameColumnWidth     = StopNameColumnWidth,
            ShowStopIndentation     = ShowStopIndentation,
            ShowTravelTimeColumn    = ShowTravelTimeColumn,
            TravelTimeColumnWidth   = TravelTimeColumnWidth,
            ShowTcColumn            = ShowTcColumn,
            TcColumnWidth           = TcColumnWidth,
            ShowZoneColumn          = ShowZoneColumn,
            ZoneColumnWidth         = ZoneColumnWidth,
            ShowRouteLettersColumn  = ShowRouteLettersColumn,
            RouteLettersColumnWidth = RouteLettersColumnWidth,
            ShowBaseRouteColumn     = ShowBaseRouteColumn,
            BaseRouteColumnWidth    = BaseRouteColumnWidth,
            ShowSignalStopIndicator = ShowSignalStopIndicator,
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
