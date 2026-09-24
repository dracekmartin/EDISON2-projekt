using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Prvky jízdních řádů, do kterých se obsah nevešel, takže se **písmo zmenšilo**. Ukazuje se
/// v návrháři (stavový řádek) i při tisku.
/// </summary>
/// <remarks>
/// Zmenšení je jinak neviditelné: v panelu i ve formuláři svítí velikost, kterou uživatel nastavil,
/// ale vytiskne se menší - a u vývěsek, kde je stránka na zastávku, vyjde každá zastávka jinak podle
/// toho, kolik má poznámek. Uživatel to pozná až z papíru (nález Pavel 1. 9., #13158).
///
/// <para>⚠️ Hláška **jmenuje konkrétní prvek a míru zmenšení**, ne obecné „nastavení mění vývěsku" -
/// právě kvůli tomu byl zrušený signál `B24` (5. 8.): z obecné hlášky nikdo nepoznal, co hlásí.
/// Týž vzor jako <see cref="DepartureColumnWarnings"/> a <see cref="FontWarnings"/>.</para>
/// </remarks>
public static class ContentShrinkWarnings
{
    /// <summary>Pod tímhle zmenšením se nehlásí nic - desetinu procenta nemá smysl komentovat.</summary>
    private const float NoticeableShrink = 0.99f;

    /// <summary>Názvy prvků drží <see cref="ElementNames"/> - jediné místo pro celou aplikaci.</summary>
    private static string? NazevProUzivatele(TemplateElement el) => el switch
    {
        NotesLegendElement or StopListElement or DepartureGridElement or LineTableElement
            or TextElement => ElementNames.SPopisem(el),
        _   => null,
    };

    /// <summary>Text upozornění, nebo null když se nic nezmenšilo.</summary>
    public static string? Summarize(Template? template, IReadOnlyList<DataContext>? pages) =>
        Seznam(template, pages) is { Count: > 0 } s ? string.Join("; ", s.Select(h => h.Text)) : null;

    /// <summary>
    /// Hlášky po prvcích. Zmenšení je informace - dělá se na přání zákazníků a je to v pořádku
    /// (rozhodnutí Jan 24. 9.); oříznutí pod mezí je chyba, protože text na papíře chybí.
    /// </summary>
    /// <param name="vcetneOrezuTextu">Hlásit i text oříznutý pod mezí zmenšení. Návrhář ho hlásí
    /// při kreslení náhledu (aktuálně, i s okrajem stránky), takže tady false - jinak by táž vada
    /// svítila dvakrát (nález Jan 24. 9.). Tiskové okno a kontrola linek nekreslí, proto true.</param>
    public static List<Hlaseni> Seznam(Template? template, IReadOnlyList<DataContext>? pages, bool vcetneOrezuTextu = true)
    {
        if (template is null || pages is null || pages.Count == 0) return [];

        var elements = template.Sections
            .SelectMany(s => s.Elements)
            .Where(el => el is ScheduleElementBase or TextElement)
            .Where(el => el.Height.HasValue)
            .ToList();
        if (elements.Count == 0) return [];

        var casti = new List<Hlaseni>();
        foreach (var el in elements)
        {
            if (NazevProUzivatele(el) is not { } nazev) continue;

            float nejvetsiZmenseni = 1f;
            int dotcenychStranek = 0;
            bool oriznuto = false;
            foreach (var page in pages)
            {
                float zmenseni = Zmenseni(el, page);
                if (el is TextElement text && text.ContentOverflows(page)) oriznuto = true;
                if (zmenseni >= NoticeableShrink) continue;
                dotcenychStranek++;
                if (zmenseni < nejvetsiZmenseni) nejvetsiZmenseni = zmenseni;
            }
            if (oriznuto && !vcetneOrezuTextu) continue;
            if (dotcenychStranek == 0 && !oriznuto) continue;

            // Procenta, ne body: velikost písma drží každý prvek jinde (poznámky ve vlastní
            // vlastnosti, ostatní ve stylu buněk), takže by hláška musela znát pět výjimek.
            int procent = (int)MathF.Round(nejvetsiZmenseni * 100f);
            // Text pod mezí zmenšování se ořízne - to je ztráta obsahu, takže se musí říct
            // i s cestou k nápravě. Do 3. 9. se ořez nehlásil vůbec a uživatel o něm nevěděl.
            string kde = pages.Count > 1 && dotcenychStranek > 0
                ? $"{nazev} · {dotcenychStranek} z {pages.Count} stránek" : nazev;
            casti.Add(oriznuto
                ? new Hlaseni(ZavaznostHlaseni.Chyba, nazev,
                    "text se nevejde ani po zmenšení písma, ořízl se", "zkrátit text, nebo zvětšit prvek", el.Id)
                : new Hlaseni(ZavaznostHlaseni.Informace, kde,
                    pages.Count > 1 ? $"písmo zmenšeno až na {procent} %" : $"písmo zmenšeno na {procent} %", null, el.Id));
        }

        return casti;
    }

    /// <summary>
    /// Kolikrát se písmo zmenšilo, aby se obsah vešel. 1 = vejde se, méně = zmenšeno.
    /// Musí počítat stejně jako render (<c>ComputeFontScale</c>), jinak hláška lže.
    /// </summary>
    private static float Zmenseni(TemplateElement el, DataContext? page) => el switch
    {
        // Textový prvek si škálu počítá sám (výška i šířka, s mezí) - hláška musí dostat totéž
        // číslo jako render, jinak tvrdí jinou velikost, než se vytiskne.
        TextElement text => text.ComputeShrinkScale(page?.ResolvePlaceholders(text.Text) ?? text.Text),
        ScheduleElementBase schedule => ZmenseniJr(schedule, page),
        _ => 1f,
    };

    private static float ZmenseniJr(ScheduleElementBase el, DataContext? page)
    {
        if (el.Height is not { } vyska) return 1f;
        float prirozena = el.CalculateHeightFromData(page);
        if (prirozena <= 0f) return 1f;
        float ratio = vyska / prirozena;
        return ratio >= 1f ? 1f : ratio;
    }
}
