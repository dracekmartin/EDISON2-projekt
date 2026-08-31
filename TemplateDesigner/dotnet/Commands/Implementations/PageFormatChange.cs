using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Výpočet přepočtu obsahu při změně formátu stránky - o kolik zvětšit či zmenšit a o kolik
/// posunout doprava, aby obsah seděl na střed.
/// </summary>
/// <remarks>
/// <para><b>Počítá se z tiskové plochy, ne z papíru.</b> Prvky leží uvnitř okrajů, takže kdyby se
/// poměr bral z rozměru stránky, promítla by se souběžná změna okrajů do výsledku dvakrát.</para>
/// <para><b>Bere se menší z obou os.</b> Při stejném poměru stran (A5 → A4) jsou obě stejné a vyjde
/// z toho zvětšenina téhož. Při změně orientace se liší a jedno číslo neexistuje - menší z nich
/// znamená „vejde se", takže po přepočtu nic nepřeteče ven ze stránky.</para>
/// <para><b>Středí se jen vodorovně.</b> Sekce jdou odshora dolů jako dokument, takže obsah zůstává
/// nahoře; hlavička plovoucí uprostřed prázdné stránky by vypadala rozbitě (rozhodnutí Jan 31. 8.).
/// Při stejném poměru stran vyjde posun sám od sebe nulový.</para>
/// </remarks>
public static class PageFormatChange
{
    /// <summary>Přepočet pro cílovou stránku. <paramref name="ps"/> = stav před změnou.</summary>
    public static (float Scale, float OffsetX) Compute(
        PageSize ps, float newWidth, float newHeight,
        float marginLeft, float marginRight, float marginTop, float marginBottom)
    {
        float oldW = ps.PrintableWidth, oldH = ps.PrintableHeight;
        float newW = newWidth - marginLeft - marginRight;
        float newH = newHeight - marginTop - marginBottom;

        if (oldW <= 0f || oldH <= 0f || newW <= 0f || newH <= 0f) return (1f, 0f);

        float scale = MathF.Min(newW / oldW, newH / oldH);
        float offsetX = MmMath.Round((newW - oldW * scale) / 2f);
        return (scale, offsetX);
    }
}
