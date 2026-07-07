using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Čistá geometrie pozicování textu buňky — bez SkiaSharp canvasu, deterministická a testovatelná.
/// Sdílí ji <see cref="CellRenderer"/> (jediné místo vykreslení buňky). Sem patří přesně ta logika,
/// kde historicky vznikají tiché regrese: padding shrink, H/V zarovnání a clamp proti ořezu.
/// </summary>
public static class CellLayout
{
    /// <summary>
    /// Zmenší obdélník buňky o padding (v mm). Pokud padding sežral celou šířku/výšku,
    /// příslušnou dimenzi rozbalí zpět na plný rozměr (raději bez paddingu než záporný box).
    /// </summary>
    public static SKRect ApplyPadding(SKRect rectMm, ResolvedTextStyle style)
    {
        float left   = rectMm.Left   + style.PaddingLeft;
        float top     = rectMm.Top    + style.PaddingTop;
        float right   = rectMm.Right  - style.PaddingRight;
        float bottom  = rectMm.Bottom - style.PaddingBottom;
        if (right  <= left) { left = rectMm.Left; right  = rectMm.Right;  }
        if (bottom <= top)  { top  = rectMm.Top;  bottom = rectMm.Bottom; }
        return new SKRect(left, top, right, bottom);
    }

    /// <summary>
    /// Spočítá kotvu textu (levý okraj X + baseline Y) v px z content boxu (px) a rozměrů textu.
    /// <paramref name="ascentPx"/> je KLADNÁ výška nad baseline (tj. <c>-metrics.Ascent</c>),
    /// <paramref name="descentPx"/> je <c>metrics.Descent</c>. X je vlevo clampnutý na levý okraj
    /// (chrání dlouhý text před ořezem zprava).
    /// </summary>
    public static (float X, float YBaseline) TextAnchorPx(
        SKPoint contentTopLeftPx, SKPoint contentBottomRightPx,
        float textWidthPx, float ascentPx, float descentPx,
        TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign)
    {
        var tl = contentTopLeftPx;
        var br = contentBottomRightPx;

        float x = hAlign switch
        {
            TextHorizontalAlignment.Center => tl.X + (br.X - tl.X - textWidthPx) / 2f,
            TextHorizontalAlignment.Right  => br.X - textWidthPx,
            _                              => tl.X,   // Left
        };
        if (x < tl.X) x = tl.X;   // chrání před ořezem u dlouhého textu

        float yBaseline = vAlign switch
        {
            TextVerticalAlignment.Middle => tl.Y + (br.Y - tl.Y + ascentPx - descentPx) / 2f,
            TextVerticalAlignment.Bottom => br.Y - descentPx,
            _                            => tl.Y + ascentPx,   // Top
        };

        return (x, yBaseline);
    }
}
