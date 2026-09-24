using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Tabulky spojů, ve kterých je sloupec spoje užší, než se obsah vejde - časy a značky platnosti
/// tečou do sousedního sloupce. Ukazuje se v návrháři (stavový řádek) i při tisku.
/// </summary>
/// <remarks>
/// Od 22. 9. smí zadaná největší (i nejmenší) šířka sloupce podlézt měřený obsah - je to výslovné
/// přání a bez toho nešlo sloupce zúžit vůbec. Cena je, že se obsah přeleje do souseda, a na malém
/// náhledu to vypadá jako vada sazby, ne jako důsledek nastavení. Hláška proto jmenuje **obě čísla**
/// (kolik sloupec má a kolik si žádá obsah) **a ten spoj, u kterého to poprvé nevyjde**, aby se
/// šlo podívat - týž vzor jako <see cref="ContentShrinkWarnings"/> a <see cref="DepartureColumnWarnings"/>.
/// <para>⚠️ Porovnává se holý obsah, ne dno sloupce: to zahrnuje vůli 1 mm kolem obsahu, takže
/// hláška svítila i u sloupce, kde se všechno vejde a chybí jen komfortní mezera.</para>
/// </remarks>
public static class TripColumnWidthWarnings
{
    /// <summary>
    /// Menší přesah než tohle (mm) se nehlásí.
    /// </summary>
    /// <remarks>Půl milimetru je hranice, kdy to na papíře pozná i oko: pod ní se značka opře
    /// o svislou čáru, ale do sousedního sloupce reálně nezasáhne. S mezí 0,1 mm hláška svítila
    /// i u sloupců, které vypadají dobře, a takovou hlášku si uživatel odnaučí číst
    /// (rozhodnutí Jan 22. 9.).</remarks>
    private const float NoticeableMm = 0.5f;

    /// <summary>Text upozornění, nebo null když se obsah všude vejde.</summary>
    public static string? Summarize(Template? template, IReadOnlyList<DataContext>? pages) =>
        Seznam(template, pages) is { Count: > 0 } s ? string.Join("; ", s.Select(h => h.Text)) : null;

    /// <summary>Hlášky po tabulkách - obsah přeteče do souseda, na papíře to vypadá jako vada sazby.</summary>
    public static List<Hlaseni> Seznam(Template? template, IReadOnlyList<DataContext>? pages)
    {
        if (template is null || pages is null || pages.Count == 0) return [];

        var casti = new List<Hlaseni>();
        foreach (var el in template.Sections.SelectMany(s => s.Elements).OfType<LineTableElement>())
        {
            float nejhorsiSloupec = 0f, zaklad = 0f, potreba = 0f;
            string kde = "";
            foreach (var page in pages)
            {
                if (el.SirkaSloupceProtiObsahu(page) is not { } mira) continue;
                if (mira.Potreba - mira.Sloupec <= NoticeableMm) continue;
                if (potreba > 0f && mira.Sloupec >= nejhorsiSloupec) continue;
                nejhorsiSloupec = mira.Sloupec;
                zaklad = mira.Zaklad;
                potreba = mira.Potreba;
                kde = mira.Kde;
            }
            if (potreba <= 0f) continue;

            // Obě čísla a k tomu konkrétní spoj: z toho jde poznat, o kolik povolit, a jde se na to
            // podívat (požadavek Jan 22. 9.). Bez spoje by uživatel prohlížel celou linku.
            // Co přetéká, jde do závorky - jinak by se hláška musela shodovat v čísle
            // („značky potřebují" × „značka potřebuje") podle toho, co zrovna vyhrálo.
            // ⚠️ Šířka sloupce není nastavená hodnota: sloupec spoje se značkou u času je o ni
            // širší. Bez rozpadu na základ a příplatek to vypadá, že nastavení neplatí
            // (nález Jan 22. 9.: „mám 3,5 mm, proč píše 5,4?").
            string sirka = nejhorsiSloupec - zaklad > 0.05f
                ? $"{nejhorsiSloupec:0.0} mm (základ {zaklad:0.0} mm + místo na značky)"
                : $"{nejhorsiSloupec:0.0} mm";
            casti.Add(new Hlaseni(ZavaznostHlaseni.Chyba,
                $"{ElementNames.ForUser(el)}: sloupec spoje má {sirka}, "
                + $"obsah potřebuje {potreba:0.0} mm ({kde}) - přeteče do sousedního sloupce", el.Id));
        }

        return casti;
    }
}
