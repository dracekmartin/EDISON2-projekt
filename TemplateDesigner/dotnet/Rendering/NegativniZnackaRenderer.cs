using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Kreslení <b>negativní značky</b> v buňce - tmavý box se světlým znakem uvnitř.
/// </summary>
/// <remarks>
/// <para>Značka odkazuje do legendy na datumové omezení („nejede 24. 12."). Inverzní tisk není
/// ozdoba: odlišuje ji od značek platnosti nad ní, které říkají opak (kdy spoj <b>jede</b>), a je
/// to tvar, který má i starý modul - ten dává tomuhle řádku vlastní typ obsahu
/// (`JRapiLinPrtWin.mac:1370-1376`, typ 3 vedle 2 pro značky platnosti a 1 pro text).</para>
/// <para><b>Inverze se bere z kaskády, ne z pevných barev:</b> box dostane barvu textu buňky
/// a znak její barvu pozadí (průhledné pozadí = bílá). Obarvená tabulka tak zůstane čitelná,
/// místo aby uprostřed držela černý obdélník.</para>
/// <para><b>Proč to nesdílí kreslení s prvkem odjezdů a legendou poznámek:</b> ty dvě značku
/// nekreslí do buňky, ale inline za minutou, resp. jako odrážku řádku legendy, a mají kvůli tomu
/// jinak spočítaný obal (výška z metrik písma × z výšky řádku legendy). Sjednotit geometrii by
/// změnilo jejich hotový výstup, a to je rozhodnutí o vzhledu, ne refaktoring. Tenhle soubor je
/// pro to sjednocení připravené místo, až se o něm rozhodne.</para>
/// </remarks>
public static class NegativniZnackaRenderer
{
    /// <summary>Vodorovná mezera kolem znaku jako podíl velikosti písma (shodně s prvkem odjezdů).</summary>
    private const float PomerVnitrniMezery = 0.12f;

    /// <summary>Nakreslí značku do obdélníku (v mm) podle zarovnání stylu. Prázdná = nic.</summary>
    public static void DrawCell(RenderContext ctx, SKRect contentMm, string znak,
        ResolvedTextStyle style, float scale)
    {
        if (string.IsNullOrEmpty(znak)) return;

        using var font = style.CreateFont(scale, ctx.MmToPx);
        var m = font.Metrics;

        float mezeraPx = font.Size * PomerVnitrniMezery;
        float znakPx = font.MeasureText(znak);
        float boxPx = znakPx + mezeraPx * 2f;

        var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);
        var (xPx, baselineY) = CellLayout.TextAnchorPx(
            tl, br, boxPx, -m.Ascent, m.Descent, style.HAlign, style.VAlign);

        using var boxPaint = new SKPaint { Color = style.ParsedTextColor, IsAntialias = true };
        ctx.Canvas.DrawRect(
            new SKRect(xPx, baselineY + m.Ascent, xPx + boxPx, baselineY + m.Descent), boxPaint);

        using var znakPaint = new SKPaint { Color = BarvaZnaku(style), IsAntialias = true };
        ctx.Canvas.DrawText(znak, xPx + mezeraPx, baselineY, font, znakPaint);
    }

    /// <summary>Barva znaku = pozadí buňky; průhledné pozadí (výchozí stav) znamená bílou.</summary>
    private static SKColor BarvaZnaku(ResolvedTextStyle style)
    {
        var pozadi = ColorHex.ParseOrTransparent(style.BackgroundColor);
        return pozadi.Alpha == 0 ? SKColors.White : pozadi;
    }
}
