using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Zámek pozicování textu buňky (vrstva 4 — Renderer). Testuje čistou geometrii vytaženou
/// z <see cref="CellRenderer"/> do <see cref="CellLayout"/> — přesně tu logiku (padding shrink,
/// H/V zarovnání, clamp), kde podle dokumentace opakovaně vznikají tiché regrese. Samotné kreslení
/// na SKCanvas se netestuje (SkiaSharp), ale výpočet kotvy ano — deterministicky, bez fontů.
/// </summary>
public class CellLayoutTests
{
    // ── ApplyPadding ─────────────────────────────────────────────────────────
    [Fact]
    public void ApplyPadding_ZmensiBoxOKazdyOkraj()
    {
        var style = CellDefaults.Default with
        {
            PaddingLeft = 2f, PaddingTop = 1f, PaddingRight = 3f, PaddingBottom = 4f,
        };
        var content = CellLayout.ApplyPadding(new SKRect(10, 20, 110, 60), style);

        Assert.Equal(12f, content.Left);
        Assert.Equal(21f, content.Top);
        Assert.Equal(107f, content.Right);
        Assert.Equal(56f, content.Bottom);
    }

    [Fact]
    public void ApplyPadding_KdyzPaddingSezralSirku_RozbaliZpet()
    {
        var style = CellDefaults.Default with { PaddingLeft = 3f, PaddingRight = 3f };
        var content = CellLayout.ApplyPadding(new SKRect(0, 0, 5, 5), style);

        // Šířka rozbalena zpět (3+3 > 5), výška nedotčená (žádný V padding).
        Assert.Equal(0f, content.Left);
        Assert.Equal(5f, content.Right);
    }

    // ── TextAnchorPx — horizontální ────────────────────────────────────────────
    private static readonly SKPoint Tl = new(100, 200);
    private static readonly SKPoint Br = new(300, 260);   // box 200 × 60 px
    private const float TextW = 40f, Ascent = 15f, Descent = 5f;

    [Theory]
    [InlineData(TextHorizontalAlignment.Left,   100f)]
    [InlineData(TextHorizontalAlignment.Center, 180f)]   // 100 + (200-40)/2
    [InlineData(TextHorizontalAlignment.Right,  260f)]   // 300 - 40
    public void TextAnchorPx_HorizontalniZarovnani(TextHorizontalAlignment h, float expectedX)
    {
        var (x, _) = CellLayout.TextAnchorPx(Tl, Br, TextW, Ascent, Descent, h, TextVerticalAlignment.Top);
        Assert.Equal(expectedX, x, 3);
    }

    [Fact]
    public void TextAnchorPx_DlouhyText_ClampNaLevyOkraj()
    {
        // Text širší než box → center by dal X < levý okraj; clamp ho drží na tl.X.
        var (x, _) = CellLayout.TextAnchorPx(Tl, Br, textWidthPx: 250f, Ascent, Descent,
            TextHorizontalAlignment.Center, TextVerticalAlignment.Top);
        Assert.Equal(100f, x, 3);
    }

    // ── TextAnchorPx — vertikální ───────────────────────────────────────────────
    [Theory]
    [InlineData(TextVerticalAlignment.Top,    215f)]   // tl.Y + ascent
    [InlineData(TextVerticalAlignment.Middle, 235f)]   // 200 + (60 + 15 - 5)/2
    [InlineData(TextVerticalAlignment.Bottom, 255f)]   // br.Y - descent
    public void TextAnchorPx_VertikalniZarovnani(TextVerticalAlignment v, float expectedY)
    {
        var (_, y) = CellLayout.TextAnchorPx(Tl, Br, TextW, Ascent, Descent, TextHorizontalAlignment.Left, v);
        Assert.Equal(expectedY, y, 3);
    }

    // ── SplitLines / LineCount — víceřádkové záhlaví (\n, např. CZ/EN) ──────────
    [Theory]
    [InlineData("Zastávka", 1)]
    [InlineData("Zastávka\nStop", 2)]
    [InlineData("Zastávka\\nStop", 2)]       // literální \n token (autorská inline zkratka)
    [InlineData("Zastávka\r\nStop", 2)]     // WPF TextBox posílá \r\n
    [InlineData("Zastávka\rStop", 2)]        // samotné \r
    [InlineData("A\nB\nC", 3)]
    [InlineData("", 1)]                       // prázdný = 1 řádek (má výšku)
    public void LineCount_PocitaRadky(string text, int expected)
    {
        Assert.Equal(expected, CellLayout.LineCount(text));
    }

    [Fact]
    public void LineCount_Null_JeJeden()
    {
        Assert.Equal(1, CellLayout.LineCount(null));
    }

    [Fact]
    public void SplitLines_NormalizujeZlomyAZachovaPrazdneRadky()
    {
        var lines = CellLayout.SplitLines("Zastávka\r\nStop\n\nHalt");
        Assert.Equal(new[] { "Zastávka", "Stop", "", "Halt" }, lines);
    }
}
