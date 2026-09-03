using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Co se má s obsahem stát při změně formátu stránky - o kolik ho zvětšit či zmenšit a o kolik
/// posunout, aby seděl na střed.
/// </summary>
/// <param name="Factor">Čím přenásobit dnešní velikosti (1 = nechat být).</param>
/// <param name="PrevOffsetX">Posun, který na obsahu leží teď - před škálováním se odečte.</param>
/// <param name="NewOffsetX">Posun, který na něm má ležet potom.</param>
/// <param name="Origin">Nový vztah k výchozí ploše, který si šablona odloží.</param>
public readonly record struct PageFormatPlan(
    float Factor, float PrevOffsetX, float NewOffsetX, PageFormatOrigin Origin);

/// <summary>
/// Výpočet přepočtu obsahu při změně formátu stránky.
/// </summary>
/// <remarks>
/// <para><b>Počítá se z tiskové plochy, ne z papíru.</b> Prvky leží uvnitř okrajů, takže kdyby se
/// poměr bral z rozměru stránky, promítla by se souběžná změna okrajů do výsledku dvakrát.</para>
/// <para><b>Bere se menší z obou os.</b> Při stejném poměru stran (A5 → A4) jsou obě skoro stejné
/// a vyjde z toho zvětšenina téhož. Při změně orientace se liší a jedno číslo neexistuje - menší
/// z nich znamená „vejde se", takže po přepočtu nic nepřeteče ven ze stránky.</para>
/// <para><b>Vždy od výchozí plochy, nikdy od aktuálního stavu.</b> To je jediné, čím je otočení
/// vratné: poměr „aby se to vešlo" vyjde při otočení na výšku i na šířku stejně, takže dvě otočení
/// se nevyruší, ale vynásobí (u A4 na 47 %, písmo z 12 pt na 5,6 pt - nález Jan 31. 8.). Když se
/// počítá od výchozí plochy, návrat na původní formát dá poměr 1 a obsah sedne přesně tam, kde byl.</para>
/// <para><b>Středí se jen vodorovně.</b> Sekce jdou odshora dolů jako dokument, takže obsah zůstává
/// nahoře; hlavička plovoucí uprostřed prázdné stránky by vypadala rozbitě (rozhodnutí Jan 31. 8.).
/// Při stejném poměru stran vyjde posun skoro nulový sám od sebe.</para>
/// </remarks>
public static class PageFormatChange
{
    /// <summary>Přepočet na cílovou stránku. Nesahá na šablonu, jen počítá.</summary>
    public static PageFormatPlan Plan(
        Template template, float newWidth, float newHeight,
        float marginLeft, float marginRight, float marginTop, float marginBottom)
    {
        var origin = template.FormatOrigin ?? PageFormatOrigin.From(template.PageSize);

        float newW = newWidth - marginLeft - marginRight;
        float newH = newHeight - marginTop - marginBottom;

        if (origin.PrintableWidth <= 0f || origin.PrintableHeight <= 0f
            || newW <= 0f || newH <= 0f || origin.Scale <= 0f)
            return new(1f, origin.OffsetX, origin.OffsetX, origin);

        float target = MathF.Min(newW / origin.PrintableWidth, newH / origin.PrintableHeight);
        float offsetX = MmMath.Round((newW - origin.PrintableWidth * target) / 2f);

        return new(
            Factor: target / origin.Scale,
            PrevOffsetX: origin.OffsetX,
            NewOffsetX: offsetX,
            Origin: origin with { Scale = target, OffsetX = offsetX });
    }

    /// <summary>Plán pro „změnit jen stránku" - obsahu se nesahá.</summary>
    public static PageFormatPlan KeepSizes(Template template)
    {
        var origin = template.FormatOrigin ?? PageFormatOrigin.From(template.PageSize);
        return new(1f, origin.OffsetX, origin.OffsetX, origin);
    }
}
