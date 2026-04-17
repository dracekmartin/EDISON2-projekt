using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Legenda poznámek pod mřížkou odjezdů.
/// Zobrazuje: negativní značky (invertované barvy), speciální znaky, textové poznámky.
/// Height = null → dynamická výška; Height = pevná → auto-scale fontu při přetečení.
/// Uživatel umístí na canvas pod StopListElement/DepartureGridElement.
/// </summary>
public class NotesLegendElement : ScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Vizuální vlastnosti
    // ═══════════════════════════════════════════
    public string HeaderText { get; set; } = "Poznámky";
    public string NoteSeparator { get; set; } = " — ";
    public float NoteFontSize { get; set; } = 7f;
    public float NoteLineHeight { get; set; } = 4f;

    // ═══════════════════════════════════════════
    // Negativní značky
    // ═══════════════════════════════════════════
    public string NegativniZnackaBackColor { get; set; } = "#FF000000";
    public string NegativniZnackaForeColor { get; set; } = "#FFFFFFFF";

    // ═══════════════════════════════════════════
    // Filtrování
    // ═══════════════════════════════════════════
    public bool ShowNegativniZnacky { get; set; } = true;
    public bool ShowSpecialniZnaky { get; set; } = true;
    public bool ShowTextovePoznamky { get; set; } = true;

    // ─── Designer ───────────────────────────────────────────────────────────

    protected override float DesignerPlaceholderHeight => 25f;

    // ─── Render ─────────────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        var poznamky = BuildPoznamky(ctx.Data);
        var filtered = FilterPoznamky(poznamky);
        bool hasData = filtered.Count > 0;

        // Auto-scale: pokud Height je pevné a obsah přetéká, zmenšit font
        float scale = hasData ? ComputeFontScale(CalculateHeightFromData(ctx.Data)) : 1f;

        float effHeaderFontSize = HeaderStyle.FontSize ?? 9f;
        string effFontFamily = DataStyle.FontFamily ?? "Arial";

        float noteFontPx = NoteFontSize * scale * PtToMm * ctx.MmToPx;
        float headerFontPx = effHeaderFontSize * scale * PtToMm * ctx.MmToPx;
        float headerH = ComputeHeaderH(effHeaderFontSize * scale);
        float scaledNoteLineH = NoteLineHeight * scale;

        using var paints = CreateStandardPaints();

        using var noteFont = new SKFont(GetTypeface(effFontFamily), noteFontPx);
        using var headerFont = new SKFont(GetTypeface(effFontFamily, SKFontStyle.Bold), headerFontPx);
        using var boldFont = new SKFont(GetTypeface(effFontFamily, SKFontStyle.Bold), noteFontPx);

        // Záhlaví
        float currentY = RenderSectionHeader(ctx, HeaderText,
            X, Y, Width, headerH, headerFont, paints.HeaderBg, paints.HeaderText, paints.Grid);

        if (!hasData)
        {
            // Placeholder
            paints.Text.Color = new SKColor(0xFF999999);
            for (int i = 0; i < 3; i++)
            {
                var pt = ctx.ToCanvas(X + 1f, currentY + NoteLineHeight * 0.75f);
                ctx.Canvas.DrawText($"Poznámka {i + 1}", pt.X, pt.Y, noteFont, paints.Text);
                currentY += NoteLineHeight;
            }
        }
        else
        {
            foreach (var pozn in filtered)
            {
                float lineX = X + 1f;

                switch (pozn.Typ)
                {
                    case TypPoznamky.NegativniZnacka:
                        RenderNegativniZnacka(ctx, pozn, ref lineX, currentY, scaledNoteLineH, noteFont, boldFont, paints.Text);
                        break;

                    case TypPoznamky.SpecialniZnak:
                        var znakPt = ctx.ToCanvas(lineX, currentY + scaledNoteLineH * 0.75f);
                        ctx.Canvas.DrawText(pozn.Znak, znakPt.X, znakPt.Y, boldFont, paints.Text);
                        lineX += boldFont.MeasureText(pozn.Znak) / ctx.MmToPx + 1.5f;
                        var textPt = ctx.ToCanvas(lineX, currentY + scaledNoteLineH * 0.75f);
                        ctx.Canvas.DrawText(pozn.Text, textPt.X, textPt.Y, noteFont, paints.Text);
                        break;

                    default:
                        var pt = ctx.ToCanvas(lineX, currentY + scaledNoteLineH * 0.75f);
                        string displayText = pozn.Znak.Length > 0 ? $"{pozn.Znak}{NoteSeparator}{pozn.Text}" : pozn.Text;
                        ctx.Canvas.DrawText(displayText, pt.X, pt.Y, noteFont, paints.Text);
                        break;
                }

                currentY += scaledNoteLineH;
            }
        }

        RenderOuterBorder(ctx, X, Y, Width, currentY - Y, paints.Grid);
    }

    private void RenderNegativniZnacka(RenderContext ctx, PoznamkaData pozn,
        ref float lineX, float currentY, float lineH, SKFont noteFont, SKFont boldFont, SKPaint textPaint)
    {
        var nzBgColor = SKColor.Parse(NegativniZnackaBackColor);
        var nzFgColor = SKColor.Parse(NegativniZnackaForeColor);

        // Invertovaný obdélník se znakem
        float znakW = boldFont.MeasureText(pozn.Znak) / ctx.MmToPx + 1.5f;
        float boxH = lineH * 0.85f;
        float boxY = currentY + (lineH - boxH) / 2f;

        using var nzBgPaint = new SKPaint { Color = nzBgColor };
        var boxRect = ctx.ToCanvasRect(lineX, boxY, znakW, boxH);
        ctx.Canvas.DrawRect(boxRect, nzBgPaint);

        using var nzFgPaint = new SKPaint { Color = nzFgColor, IsAntialias = true };
        var znakPt = ctx.ToCanvas(lineX + 0.75f, currentY + lineH * 0.75f);
        ctx.Canvas.DrawText(pozn.Znak, znakPt.X, znakPt.Y, boldFont, nzFgPaint);

        lineX += znakW + 1f;

        // Text za značkou
        var textPt = ctx.ToCanvas(lineX, currentY + lineH * 0.75f);
        ctx.Canvas.DrawText(pozn.Text, textPt.X, textPt.Y, noteFont, textPaint);
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public float CalculateHeightFromData(DataContext? data)
    {
        var poznamky = BuildPoznamky(data);
        var filtered = FilterPoznamky(poznamky);
        if (filtered.Count == 0) return DesignerPlaceholderHeight;

        return ComputeHeaderH(HeaderStyle.FontSize ?? 9f) + filtered.Count * NoteLineHeight;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private List<PoznamkaData> FilterPoznamky(List<PoznamkaData> poznamky)
    {
        return poznamky.Where(p =>
            (p.Typ == TypPoznamky.NegativniZnacka && ShowNegativniZnacky) ||
            (p.Typ == TypPoznamky.SpecialniZnak && ShowSpecialniZnaky) ||
            (p.Typ == TypPoznamky.Text && ShowTextovePoznamky)
        ).ToList();
    }

    // ─── PropertyDescriptors ────────────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        .. GetScheduleBaseDescriptors(),

        new StringPropertyDescriptor { Label = "Nadpis",               Group = "Poznámky", Get = () => HeaderText,       Set = v => HeaderText = v },
        new StringPropertyDescriptor { Label = "Oddělovač",           Group = "Poznámky", Get = () => NoteSeparator,    Set = v => NoteSeparator = v },
        new FloatPropertyDescriptor  { Label = "Velikost písma pozn.", Group = "Poznámky", Get = () => NoteFontSize,     Set = v => NoteFontSize = v,     Min = 4, Max = 20, Unit = "pt" },
        new FloatPropertyDescriptor  { Label = "Výška řádku",         Group = "Poznámky", Get = () => NoteLineHeight,   Set = v => NoteLineHeight = v,   Min = 2, Max = 15, Unit = "mm" },

        new BoolPropertyDescriptor  { Label = "Negativní značky",    Group = "Poznámky", Get = () => ShowNegativniZnacky, Set = v => ShowNegativniZnacky = v },
        new BoolPropertyDescriptor  { Label = "Speciální znaky",     Group = "Poznámky", Get = () => ShowSpecialniZnaky,  Set = v => ShowSpecialniZnaky = v },
        new BoolPropertyDescriptor  { Label = "Textové poznámky",    Group = "Poznámky", Get = () => ShowTextovePoznamky, Set = v => ShowTextovePoznamky = v },

        new ColorPropertyDescriptor { Label = "NZ pozadí",  Group = "Negativní značky", Get = () => NegativniZnackaBackColor, Set = v => NegativniZnackaBackColor = v },
        new ColorPropertyDescriptor { Label = "NZ barva",   Group = "Negativní značky", Get = () => NegativniZnackaForeColor, Set = v => NegativniZnackaForeColor = v },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ──────────────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        NoteFontSize *= scale;
        NoteLineHeight *= scale;
    }

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new NotesLegendElement
        {
            HeaderText               = HeaderText,
            NoteSeparator            = NoteSeparator,
            NoteFontSize             = NoteFontSize,
            NoteLineHeight           = NoteLineHeight,
            NegativniZnackaBackColor = NegativniZnackaBackColor,
            NegativniZnackaForeColor = NegativniZnackaForeColor,
            ShowNegativniZnacky      = ShowNegativniZnacky,
            ShowSpecialniZnaky       = ShowSpecialniZnaky,
            ShowTextovePoznamky      = ShowTextovePoznamky,
        };
        CopyBaseTo(clone);
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
