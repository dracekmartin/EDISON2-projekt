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

        // Pojistka pro velmi nízký řádek: i těsný box se musí vejít, jinak přeteče do řádku pod
        // sebou. Zmenšuje se až tady, takže při běžné výšce řádku zůstane značka v plné velikosti.
        float vyskaBunkyPx = (contentMm.Bottom - contentMm.Top) * ctx.MmToPx;
        float vyskovaSkala = 1f;
        using (var mira = style.CreateFont(scale, ctx.MmToPx))
        {
            mira.MeasureText(znak, out var o);
            float potreba = o.Height + mira.Size * PomerVnitrniMezery * 2f;
            if (potreba > vyskaBunkyPx && potreba > 0f && vyskaBunkyPx > 0f)
                vyskovaSkala = vyskaBunkyPx / potreba;
        }

        using var font = style.CreateFont(scale * vyskovaSkala, ctx.MmToPx);

        // ⚠️ Box se staví z **obrysu znaku**, ne z výšky písma. Ascent a descent počítají s dotažnicemi
        // (á, j), které značka nikdy nemá - box z nich vyšel o třetinu vyšší, než kolik číslice
        // zabraly, a v hustě nastavené tabulce přetekl přes spodní linku do řádku pod sebou
        // (nález Jan 7. 9.). Obrys navíc drží box přilehlý ke znaku, což je i tvar starého tisku.
        float znakPx = font.MeasureText(znak, out var obrys);
        float mezeraPx = font.Size * PomerVnitrniMezery;
        float boxPx = znakPx + mezeraPx * 2f;
        // Svisle stejná mezera jako vodorovně, ať box nevypadá zmáčknutý.
        float nadPx = -obrys.Top + mezeraPx;
        float podPx = obrys.Bottom + mezeraPx;

        var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);
        var (xPx, baselineY) = CellLayout.TextAnchorPx(
            tl, br, boxPx, nadPx, podPx, style.HAlign, style.VAlign);

        using var boxPaint = new SKPaint { Color = style.ParsedTextColor, IsAntialias = true };
        ctx.Canvas.DrawRect(
            new SKRect(xPx, baselineY - nadPx, xPx + boxPx, baselineY + podPx), boxPaint);

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
