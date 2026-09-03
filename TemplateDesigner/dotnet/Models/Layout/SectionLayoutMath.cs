namespace TemplateDesigner.Models.Layout;

/// <summary>
/// Výpočty výšek sekcí na stránce - kolik místa zbývá a co z něj smí sekce zabrat.
/// </summary>
/// <remarks>
/// ⚠️ **Počítá se podle papíru, ne podle plátna návrháře.** Do 3. 9. se od zbývajícího místa
/// odečítalo 5 mm za každou sekci jako prostor pro její modré záhlaví na plátně. To je otázka
/// zobrazení, ne rozměru sestavy - u tří sekcí by tím zůstalo 15 mm papíru nevyužitých a tlačítko
/// „vyplnit zbývající místo" by stránku nevyplnilo.
/// </remarks>
public static class SectionLayoutMath
{
    /// <summary>Nejmenší výška, na kterou smí sekce spadnout - pod tím se do ní nedá nic položit.</summary>
    public const float MinimalniVyskaMm = 5f;

    /// <summary>
    /// Kolik milimetrů tiskové plochy zbývá pro <paramref name="section"/>, když ostatní sekce
    /// stránky drží svou výšku. Nikdy méně než <see cref="MinimalniVyskaMm"/>.
    /// </summary>
    /// <remarks>
    /// Sekce pod ní se tím **posune a podrží svou výšku** - proto se nevytlačí ze stránky
    /// (rozhodnutí Jan 3. 9.). Příklad, kterým je to zadané: tři sekce zabírají 0-10 %, 10-20 %
    /// a 20-30 % stránky; po vyplnění prostřední vyjde 0-10 %, 10-90 % a 90-100 %.
    /// </remarks>
    public static float ZbyvajiciMistoMm(Template template, Section section)
    {
        float obsazeno = 0f;
        foreach (var s in template.Sections)
        {
            if (ReferenceEquals(s, section)) continue;
            obsazeno += s.EffectiveDesignerHeight;
        }

        return MathF.Max(MinimalniVyskaMm, template.PageSize.PrintableHeight - obsazeno);
    }

    /// <summary>
    /// Vyplní sekce už celé zbývající místo? Slouží k tomu, aby tlačítko nenabízelo akci,
    /// která nic neudělá.
    /// </summary>
    public static bool VyplnujeCeleMisto(Template template, Section section)
    {
        if (section.DesignerHeight is not { } vyska) return true;   // dynamická roste sama
        return MathF.Abs(vyska - ZbyvajiciMistoMm(template, section)) < 0.05f;
    }
}
