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
    /// Nejmenší výška, se kterou má smysl **zakládat** sekci - i na plné stránce.
    /// </summary>
    /// <remarks>
    /// ⚠️ Vyšší než <see cref="MinimalniVyskaMm"/> záměrně (rozhodnutí Jan 10. 9.). Sekce založená
    /// na 5 mm je k ničemu - uživatel do ní nic nepoloží a stejně jí hned mění výšku ručně, takže
    /// jediné, co ta mez ušetřila, bylo pár milimetrů papíru. Když se sekce v téhle výšce na
    /// stránku nevejde, přetečení se **řekne** a uživatel rozhodne, co zkrátí.
    ///
    /// <para>Strop je požadovaná výška: sekci, která si řekla o méně (oddělovač 5 mm), tahle mez
    /// nenafoukne.</para>
    /// </remarks>
    public const float MinimalniVyskaNoveSekceMm = 20f;

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

    /// <summary>O kolik se má zkrátit jedna existující sekce, aby se vešla nově přidávaná.</summary>
    public readonly record struct ZkraceniSekce(Section Sekce, float NovaVyska);

    /// <summary>
    /// Rozdělení místa pro nově přidávanou sekci: jakou výšku dostane a které existující sekce
    /// se kvůli ní zkrátí.
    /// </summary>
    /// <param name="VyskaNoveSekce">Kolik mm nová sekce reálně dostane (nejméně <see cref="MinimalniVyskaMm"/>).</param>
    /// <param name="Zkraceni">Sekce, kterým se mění výška. Prázdné, když se nová vešla do volného místa.</param>
    public readonly record struct MistoProNovouSekci(
        float VyskaNoveSekce,
        IReadOnlyList<ZkraceniSekce> Zkraceni);

    /// <summary>
    /// Kolik místa dostane nově přidávaná sekce a co se kvůli ní zkrátí. Nejdřív se sáhne po
    /// volném místě stránky, teprve chybějící zbytek se bere ostatním sekcím.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Do 10. 9. se krátilo vždycky** a volné místo se nezjišťovalo: nová sekce si vzala svých
    /// 30 mm poměrem z existujících, takže na A5 se 37 mm obsahu spadly obě zbylé na dno, přestože
    /// zbytek stránky byl prázdný (nález Jan 7. 9., `B129`).
    ///
    /// <para>Krátí se **podle rezervy**, ne podle výšky - rezerva je to, co sekci zbývá nad jejím
    /// obsahem (<see cref="MinimalniVyskaProSekci"/>). Sekce s prvkem u dolní hrany tak nedá nic
    /// a nezmáčkne se pod svůj obsah; dřívější dno na konstantě 5 mm tuhle mez obcházelo, takže
    /// přidání sekce oříznulo prvky, které tažení hrany ani dialog výšky sáhnout nenechají.</para>
    ///
    /// <para>Když ani po zkrácení místo není, dostane nová sekce jen to, co zbylo - ale nikdy míň
    /// než <see cref="MinimalniVyskaNoveSekceMm"/>, i za cenu přetečení stránky.</para>
    /// </remarks>
    public static MistoProNovouSekci RozdelMistoProNovouSekci(Template template, float pozadovanaVyska)
    {
        float obsazeno = 0f;
        foreach (var s in template.Sections)
            obsazeno += s.EffectiveDesignerHeight;

        // Sekce, která si řekla o méně než dno (oddělovač 5 mm), dostane svou výšku - dno ji
        // nenafoukne.
        float dno   = MathF.Min(pozadovanaVyska, MinimalniVyskaNoveSekceMm);
        float volne = MathF.Max(0f, template.PageSize.PrintableHeight - obsazeno);
        float chybi = pozadovanaVyska - volne;
        if (chybi <= TolerancePrekryvuMm)
            return new MistoProNovouSekci(MathF.Max(dno, pozadovanaVyska), []);

        var rezervy = new List<(Section Sekce, float Vyska, float Rezerva)>();
        float celkovaRezerva = 0f;
        foreach (var s in template.Sections)
        {
            if (s.DesignerHeight is not { } vyska) continue;   // dynamickou výšku počítá engine z obsahu
            float rezerva = vyska - MinimalniVyskaProSekci(s);
            if (rezerva <= TolerancePrekryvuMm) continue;
            rezervy.Add((s, vyska, rezerva));
            celkovaRezerva += rezerva;
        }

        if (celkovaRezerva <= 0f)
            return new MistoProNovouSekci(MathF.Max(dno, volne), []);

        float kOdebrani = MathF.Min(chybi, celkovaRezerva);
        var zkraceni = new List<ZkraceniSekce>();
        foreach (var (sekce, vyska, rezerva) in rezervy)
        {
            float ubrat = rezerva / celkovaRezerva * kOdebrani;
            if (ubrat <= TolerancePrekryvuMm) continue;
            zkraceni.Add(new ZkraceniSekce(sekce, vyska - ubrat));
        }

        return new MistoProNovouSekci(MathF.Max(dno, volne + kOdebrani), zkraceni);
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
