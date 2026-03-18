using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Legenda poznámek pod mřížkou odjezdů.
/// Zobrazuje: negativní značky (invertované barvy), speciální znaky, textové poznámky.
/// Height je vždy null (dynamický — výšku počítá engine z počtu poznámek).
/// Uživatel umístí na canvas pod StopListElement/DepartureGridElement.
/// </summary>
public class NotesLegendElement : ScheduleElementBase
{
    // ═══════════════════════════════════════════
    // Vizuální vlastnosti
    // ═══════════════════════════════════════════
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

        float noteFontPx = NoteFontSize * PtToMm * ctx.MmToPx;
        float headerFontPx = HeaderFontSize * PtToMm * ctx.MmToPx;
        float headerH = ComputeHeaderH(HeaderFontSize);

        using var paints = CreateStandardPaints();

        using var noteFont = new SKFont(SKTypeface.FromFamilyName(FontFamily), noteFontPx);
        using var headerFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), headerFontPx);
        using var boldFont = new SKFont(
            SKTypeface.FromFamilyName(FontFamily, SKFontStyle.Bold), noteFontPx);

        // Záhlaví "Poznámky"
        float currentY = RenderSectionHeader(ctx, "Poznámky",
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
                        RenderNegativniZnacka(ctx, pozn, ref lineX, currentY, noteFont, boldFont, paints.Text);
                        break;

                    case TypPoznamky.SpecialniZnak:
                        var znakPt = ctx.ToCanvas(lineX, currentY + NoteLineHeight * 0.75f);
                        ctx.Canvas.DrawText(pozn.Znak, znakPt.X, znakPt.Y, boldFont, paints.Text);
                        lineX += boldFont.MeasureText(pozn.Znak) / ctx.MmToPx + 1.5f;
                        var textPt = ctx.ToCanvas(lineX, currentY + NoteLineHeight * 0.75f);
                        ctx.Canvas.DrawText(pozn.Text, textPt.X, textPt.Y, noteFont, paints.Text);
                        break;

                    default:
                        var pt = ctx.ToCanvas(lineX, currentY + NoteLineHeight * 0.75f);
                        string displayText = pozn.Znak.Length > 0 ? $"{pozn.Znak} — {pozn.Text}" : pozn.Text;
                        ctx.Canvas.DrawText(displayText, pt.X, pt.Y, noteFont, paints.Text);
                        break;
                }

                currentY += NoteLineHeight;
            }
        }

        RenderOuterBorder(ctx, X, Y, Width, currentY - Y, paints.Grid);
    }

    private void RenderNegativniZnacka(RenderContext ctx, PoznamkaData pozn,
        ref float lineX, float currentY, SKFont noteFont, SKFont boldFont, SKPaint textPaint)
    {
        var nzBgColor = SKColor.Parse(NegativniZnackaBackColor);
        var nzFgColor = SKColor.Parse(NegativniZnackaForeColor);

        // Invertovaný obdélník se znakem
        float znakW = boldFont.MeasureText(pozn.Znak) / ctx.MmToPx + 1.5f;
        float boxH = NoteLineHeight * 0.85f;
        float boxY = currentY + (NoteLineHeight - boxH) / 2f;

        using var nzBgPaint = new SKPaint { Color = nzBgColor };
        var boxRect = ctx.ToCanvasRect(lineX, boxY, znakW, boxH);
        ctx.Canvas.DrawRect(boxRect, nzBgPaint);

        using var nzFgPaint = new SKPaint { Color = nzFgColor, IsAntialias = true };
        var znakPt = ctx.ToCanvas(lineX + 0.75f, currentY + NoteLineHeight * 0.75f);
        ctx.Canvas.DrawText(pozn.Znak, znakPt.X, znakPt.Y, boldFont, nzFgPaint);

        lineX += znakW + 1f;

        // Text za značkou
        var textPt = ctx.ToCanvas(lineX, currentY + NoteLineHeight * 0.75f);
        ctx.Canvas.DrawText(pozn.Text, textPt.X, textPt.Y, noteFont, textPaint);
    }

    // ─── Výpočet výšky ──────────────────────────────────────────────────────

    public float CalculateHeightFromData(DataContext? data)
    {
        var poznamky = BuildPoznamky(data);
        var filtered = FilterPoznamky(poznamky);
        if (filtered.Count == 0) return DesignerPlaceholderHeight;

        return ComputeHeaderH(HeaderFontSize) + filtered.Count * NoteLineHeight;
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
        new FloatPropertyDescriptor { Label = "Velikost písma pozn.", Group = "Poznámky", Get = () => NoteFontSize,     Set = v => NoteFontSize = v,     Min = 4, Max = 20, Unit = "pt" },
        new FloatPropertyDescriptor { Label = "Výška řádku",         Group = "Poznámky", Get = () => NoteLineHeight,   Set = v => NoteLineHeight = v,   Min = 2, Max = 15, Unit = "mm" },

        new BoolPropertyDescriptor  { Label = "Negativní značky",    Group = "Poznámky", Get = () => ShowNegativniZnacky, Set = v => ShowNegativniZnacky = v },
        new BoolPropertyDescriptor  { Label = "Speciální znaky",     Group = "Poznámky", Get = () => ShowSpecialniZnaky,  Set = v => ShowSpecialniZnaky = v },
        new BoolPropertyDescriptor  { Label = "Textové poznámky",    Group = "Poznámky", Get = () => ShowTextovePoznamky, Set = v => ShowTextovePoznamky = v },

        new ColorPropertyDescriptor { Label = "NZ pozadí",  Group = "Negativní značky", Get = () => NegativniZnackaBackColor, Set = v => NegativniZnackaBackColor = v },
        new ColorPropertyDescriptor { Label = "NZ barva",   Group = "Negativní značky", Get = () => NegativniZnackaForeColor, Set = v => NegativniZnackaForeColor = v },

        .. GetScheduleBaseDescriptors(),
        .. GetPositionDescriptors()
    ];

    // ─── Clone ──────────────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new NotesLegendElement
        {
            Id = Guid.NewGuid(),
            X = X, Y = Y, Width = Width, Height = Height,
            ZIndex = ZIndex, IsLocked = IsLocked,
            VisibilityExpression = VisibilityExpression,
            CustomProperties = CustomProperties is null ? null : new(CustomProperties),
            GroupId = GroupId,

            NoteFontSize             = NoteFontSize,
            NoteLineHeight           = NoteLineHeight,
            NegativniZnackaBackColor = NegativniZnackaBackColor,
            NegativniZnackaForeColor = NegativniZnackaForeColor,
            ShowNegativniZnacky      = ShowNegativniZnacky,
            ShowSpecialniZnaky       = ShowSpecialniZnaky,
            ShowTextovePoznamky      = ShowTextovePoznamky,
        };
        CopyScheduleBaseProperties(clone);
        return clone;
    }
}
