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
/// (kolik sloupec má a kolik si žádá obsah), aby bylo vidět, o kolik povolit - týž vzor jako
/// <see cref="ContentShrinkWarnings"/> a <see cref="DepartureColumnWarnings"/>.
/// </remarks>
public static class TripColumnWidthWarnings
{
    /// <summary>Menší rozdíl než tohle (mm) se nehlásí - obsah se o desetinu milimetru dotkne čáry
    /// a nikdo to nepozná.</summary>
    private const float NoticeableMm = 0.1f;

    /// <summary>Text upozornění, nebo null když se všude vejde.</summary>
    public static string? Summarize(Template? template, IReadOnlyList<DataContext>? pages)
    {
        if (template is null || pages is null || pages.Count == 0) return null;

        var casti = new List<string>();
        foreach (var el in template.Sections.SelectMany(s => s.Elements).OfType<LineTableElement>())
        {
            float nejhorsiSloupec = 0f, potreba = 0f;
            foreach (var page in pages)
            {
                if (el.SirkaSloupceProtiObsahu(page) is not { } mira) continue;
                if (mira.Potreba - mira.Sloupec <= NoticeableMm) continue;
                if (potreba > 0f && mira.Sloupec >= nejhorsiSloupec) continue;
                nejhorsiSloupec = mira.Sloupec;
                potreba = mira.Potreba;
            }
            if (potreba <= 0f) continue;

            casti.Add($"{ElementNames.ForUser(el)}: sloupec spoje má {nejhorsiSloupec:0.0} mm, "
                + $"obsah potřebuje {potreba:0.0} mm - časy a značky přetečou do sousedního sloupce");
        }

        return casti.Count == 0 ? null : string.Join("; ", casti);
    }
}
