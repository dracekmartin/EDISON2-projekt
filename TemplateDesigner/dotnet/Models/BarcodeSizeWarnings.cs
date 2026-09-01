using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Kódy (QR, čárové), které jsou na papíře **menší, než aby se daly spolehlivě sejmout**.
/// Ukazuje se v návrháři (stavový řádek) i při tisku.
/// </summary>
/// <remarks>
/// Zákazník se ptal přímo: „jak uživatelsky poznám, co je dostatečná velikost QR?" (#13158).
/// Z aplikace to poznat nešlo - kód se vykreslil vždy, jen nebyl čitelný, a na papíře to vypadalo
/// jako naše chyba. Hláška proto říká **konkrétní číslo v milimetrech**, ne „kód je malý".
///
/// <para>Počet modulů závisí na délce obsahu: krátká URL zabere 25 modulů, dlouhá s parametry 33
/// a víc. Doporučení se proto počítá z **reálného obsahu právě tištěné stránky**, ne z tabulky.</para>
/// </remarks>
public static class BarcodeSizeWarnings
{
    /// <summary>Text upozornění, nebo null když jsou všechny kódy dost velké.</summary>
    public static string? Summarize(Template? template, IReadOnlyList<DataContext>? pages)
    {
        if (template is null) return null;

        var kody = template.Sections.SelectMany(s => s.Elements).OfType<BarcodeElement>().ToList();
        if (kody.Count == 0) return null;

        var casti = new List<string>();
        foreach (var kod in kody)
        {
            float nejvetsiPotreba = 0f;
            foreach (var obsah in Obsahy(kod, pages))
            {
                float potreba = kod.DoporucenaMinimalniSirkaMm(obsah);
                if (potreba > nejvetsiPotreba) nejvetsiPotreba = potreba;
            }

            // Rozhoduje kratší strana: QR na širokém a nízkém prvku je omezené výškou.
            float mistoMm = MathF.Min(kod.Width, kod.Height ?? kod.Width);
            if (nejvetsiPotreba <= 0f || mistoMm >= nejvetsiPotreba) continue;

            casti.Add($"{Nazev(kod)} je {mistoMm:0.#} mm, pro spolehlivé čtení potřebuje {nejvetsiPotreba:0.#} mm");
        }

        return casti.Count == 0 ? null : string.Join("; ", casti);
    }

    /// <summary>Obsahy kódu podle dat stránek; bez dat aspoň to, co je v šabloně napsané.</summary>
    private static IEnumerable<string> Obsahy(BarcodeElement kod, IReadOnlyList<DataContext>? pages)
    {
        if (pages is null || pages.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(kod.Content)) yield return kod.Content;
            yield break;
        }

        foreach (var page in pages)
        {
            var obsah = page.ResolvePlaceholders(kod.Content);
            if (!string.IsNullOrWhiteSpace(obsah)) yield return obsah;
        }
    }

    /// <summary>Jméno kódu pro uživatele - typ, ne název třídy.</summary>
    private static string Nazev(BarcodeElement kod) => kod.BarcodeType switch
    {
        BarcodeElementType.QR         => "QR kód",
        BarcodeElementType.DataMatrix => "Kód DataMatrix",
        BarcodeElementType.Pdf417     => "Kód PDF417",
        _                             => "Čárový kód",
    };
}
