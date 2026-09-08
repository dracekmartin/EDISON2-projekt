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
    /// <summary>Nejmenší výška prázdné sekce - pod tím se do ní nedá nic položit.</summary>
    public const float MinimalniVyskaMm = 5f;

    /// <summary>
    /// O kolik smí součet výšek sekcí přesáhnout tiskovou plochu, než to engine bere jako
    /// přetečení na další stránku.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Není to volnost, je to smetí z desetinných výšek.** Přidání sekce krátí ostatní
    /// poměrem, takže z kulatých výšek udělá 21,8797; tažení hrany se pak zarazí přesně na
    /// zbývajícím místě (106,1203) a součet vyjde o dvě setiny milimetru nad papír. Bez téhle mez
    /// odešla poslední sekce na druhou stránku a uživatel viděl zdvojený výstup, který si ničím
    /// nevysvětlil (nález Jan 7. 9., `templates/bug_ukazka.json`).
    ///
    /// <para>Platí na **obou stranách**: co pustí dialog výšky sekce, to musí engine unést,
    /// jinak má tatáž šablona dvě chování podle toho, kudy k výšce uživatel přišel.</para>
    /// </remarks>
    public const float TolerancePrekryvuMm = 0.05f;

    /// <summary>
    /// Nejmenší výška, na kterou smí sekce spadnout: nikdy pod svůj **obsah**.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bez tohohle dna se prvek u dolního okraje po zmenšení sekce **ořízne v návrháři, ale
    /// vytiskne se celý** - engine dává sekci `max(nastavená výška, obsah)`, takže ji při tisku
    /// nafoukne zpátky. Návrhář a tisk se tím rozejdou a šablona vypadá rozbitě, přestože není
    /// (nález Jan 4. 9.).
    ///
    /// <para>Prvky se záměrně **neposouvají**: uživatel o změnu jejich pozic nežádal a při zvětšení
    /// sekce zpět by se nevrátily, takže by se rozvržení tiše rozpadlo. Mez je předvídatelná -
    /// kdo chce sekci zmenšit, přesune nebo smaže prvek, který ji drží.</para>
    /// </remarks>
    public static float MinimalniVyskaProSekci(Section section)
    {
        float dno = MinimalniVyskaMm;
        foreach (var el in section.Elements)
        {
            float spodniHrana = el.Y + (el.Height ?? el.EffectiveDesignerHeight);
            if (spodniHrana > dno) dno = spodniHrana;
        }
        return dno;
    }

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

        // Dno je obsah sekce, ne konstanta: na plné stránce se nesmí nabídnout výška, při které
        // by se prvek u dolního okraje oříznul.
        return MathF.Max(MinimalniVyskaProSekci(section), template.PageSize.PrintableHeight - obsazeno);
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
