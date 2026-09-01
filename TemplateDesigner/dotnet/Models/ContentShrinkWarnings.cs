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

    /// <summary>
    /// Názvy prvků jak je zná uživatel z palety. ⚠️ Musí souhlasit s tooltipy tlačítek palety
    /// v <c>MainWindow.xaml</c> - jiné jméno v hlášce než na tlačítku znamená, že uživatel neví,
    /// o který prvek jde.
    /// </summary>
    private static string? NazevProUzivatele(TemplateElement el) => el switch
    {
        NotesLegendElement   => "Legenda poznámek",
        StopListElement      => "Seznam zastávek",
        DepartureGridElement => "Mřížka odjezdů",
        LineTableElement     => "Linkový JŘ",
        _                    => null,
    };

    /// <summary>Text upozornění, nebo null když se nic nezmenšilo.</summary>
    public static string? Summarize(Template? template, IReadOnlyList<DataContext>? pages)
    {
        if (template is null || pages is null || pages.Count == 0) return null;

        var elements = template.Sections
            .SelectMany(s => s.Elements)
            .OfType<ScheduleElementBase>()
            .Where(el => el.Height.HasValue)
            .ToList();
        if (elements.Count == 0) return null;

        var casti = new List<string>();
        foreach (var el in elements)
        {
            if (NazevProUzivatele(el) is not { } nazev) continue;

            float nejvetsiZmenseni = 1f;
            int dotcenychStranek = 0;
            foreach (var page in pages)
            {
                float zmenseni = Zmenseni(el, page);
                if (zmenseni >= NoticeableShrink) continue;
                dotcenychStranek++;
                if (zmenseni < nejvetsiZmenseni) nejvetsiZmenseni = zmenseni;
            }
            if (dotcenychStranek == 0) continue;

            // Procenta, ne body: velikost písma drží každý prvek jinde (poznámky ve vlastní
            // vlastnosti, ostatní ve stylu buněk), takže by hláška musela znát pět výjimek.
            int procent = (int)MathF.Round(nejvetsiZmenseni * 100f);
            casti.Add(pages.Count > 1
                ? $"{nazev}: písmo zmenšeno až na {procent} % ({dotcenychStranek} z {pages.Count} stránek)"
                : $"{nazev}: písmo zmenšeno na {procent} %");
        }

        return casti.Count == 0 ? null : string.Join("; ", casti);
    }

    /// <summary>
    /// Kolikrát se písmo zmenšilo, aby se obsah vešel. 1 = vejde se, méně = zmenšeno.
    /// Musí počítat stejně jako render (<c>ComputeFontScale</c>), jinak hláška lže.
    /// </summary>
    private static float Zmenseni(ScheduleElementBase el, DataContext? page)
    {
        if (el.Height is not { } vyska) return 1f;
        float prirozena = el.CalculateHeightFromData(page);
        if (prirozena <= 0f) return 1f;
        float ratio = vyska / prirozena;
        return ratio >= 1f ? 1f : ratio;
    }
}
