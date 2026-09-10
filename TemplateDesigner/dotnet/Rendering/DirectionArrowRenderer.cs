using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Kreslení <b>šipky směru jízdy</b> - svislá čára s hrotem, volitelně s držákem na začátku.
/// </summary>
/// <remarks>
/// Jedno místo pro <b>oba</b> druhy jízdních řádů: vývěska (<c>StopListElement</c>) ji kreslí od
/// aktuální zastávky dolů, linková tabulka přes celý seznam zastávek. Legacy linkový modul ji skládá
/// z <b>glyfů symbolového fontu</b> (značka začátku, pokračování, konce - <c>JRapiLinPrtWin.mac:3013</c>),
/// ale u nás se kreslí vektorově a nastavitelně - rozhodnutí Jan 28. 8.: co už umíme, se nemá
/// duplikovat jen proto, že to starý modul dělal jinak.
/// <para>⚠️ Geometrie je <b>doslova přenesená</b> z vývěsky (odsazení, poměry výšek, hrot), aby se
/// jí nezměnil ani pixel - hlídají to její zlaté odtisky.</para>
/// </remarks>
public static class DirectionArrowRenderer
{
    /// <summary>Vzhled šipky - to, co si uživatel nastaví na prvku.</summary>
    /// <param name="Color">Barva čáry i hrotu (`#AARRGGBB`).</param>
    /// <param name="WidthMm">Tloušťka čáry v mm.</param>
    /// <param name="HeadSizeMm">Velikost hrotu v mm.</param>
    /// <param name="Head">Tvar hrotu - plný, otevřené „V", nebo žádný.</param>
    /// <param name="LineStyle">Styl čáry; hrot zůstává plnou čarou i u čárkované.</param>
    public readonly record struct Vzhled(
        string Color, float WidthMm, float HeadSizeMm, ArrowHeadShape Head, LineStyle LineStyle);

    /// <summary>
    /// Nakreslí šipku. Souřadnice jsou v mm; <paramref name="drzakOdMm"/> = X, odkud vede vodorovný
    /// držák k čáře (rovno <paramref name="caraXMm"/> znamená bez držáku).
    /// </summary>
    /// <param name="hrotDolu">Hrot dole (směr tam), nebo nahoře (zrcadlený směr u obousměrného tisku).</param>
    /// <param name="kreslitHrot">false = jen díl čáry. Šipka se u rozdvojené zastávky přerušuje,
    /// aby nepřejela označení příjezdu a odjezdu, a mezikusy hrot nemají (`B136`).</param>
    public static void Draw(RenderContext ctx, Vzhled vzhled,
        float caraXMm, float drzakOdMm, float zacatekYMm, float konecYMm, bool hrotDolu = true,
        bool kreslitHrot = true)
    {
        float strokeW = MathF.Max(0.5f, ctx.Px(vzhled.WidthMm));
        using var paint = new SKPaint
        {
            Color = ColorHex.ParseOrBlack(vzhled.Color),
            IsStroke = true,
            StrokeWidth = strokeW,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        using var dash = Carkovani(ctx, vzhled.LineStyle);
        paint.PathEffect = dash;

        var caraZacatek = ctx.ToCanvas(caraXMm, zacatekYMm);
        var caraKonec = ctx.ToCanvas(caraXMm, konecYMm);

        // Držák (┌─ / ─┐) na začátku šipky - u vývěsky vede do sloupce zastávek, v samostatném
        // sloupci se nekreslí (drzakOd == caraX).
        if (MathF.Abs(drzakOdMm - caraXMm) > 0.001f)
            ctx.Canvas.DrawLine(ctx.ToCanvas(drzakOdMm, zacatekYMm), caraZacatek, paint);
        ctx.Canvas.DrawLine(caraZacatek, caraKonec, paint);

        if (vzhled.Head == ArrowHeadShape.None) return;

        // Hrot vždy plnou čarou (bez PathEffectu) - čárkovaný hrot se rozpadne na tečky
        // a přestane být šipkou.
        float hrot = ctx.Px(vzhled.HeadSizeMm);
        float smer = hrotDolu ? 1f : -1f;
        using var hrotPaint = new SKPaint
        {
            Color = paint.Color,
            IsStroke = vzhled.Head == ArrowHeadShape.Open,
            StrokeWidth = strokeW,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        using var path = new SKPath();
        path.MoveTo(caraKonec.X - hrot * 0.5f, caraKonec.Y - hrot * smer);
        path.LineTo(caraKonec.X, caraKonec.Y);
        path.LineTo(caraKonec.X + hrot * 0.5f, caraKonec.Y - hrot * smer);
        if (vzhled.Head == ArrowHeadShape.Filled) path.Close();
        ctx.Canvas.DrawPath(path, hrotPaint);
    }

    /// <summary>Vzor čárkování, nebo null u plné čáry. Volající vlastní (dispose).</summary>
    private static SKPathEffect? Carkovani(RenderContext ctx, LineStyle style)
    {
        if (style == LineStyle.Solid) return null;
        var (dashMm, gapMm) = LineElement.GetDefaultDashPattern(style);
        return SKPathEffect.CreateDash([ctx.Px(dashMm), ctx.Px(gapMm)], 0);
    }
}
