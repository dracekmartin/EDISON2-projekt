using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Popisky uzlů stromu šablony - stejná jména, jaká uživatel vidí v paletě, ve formuláři sekce
/// a ve stavovém řádku. Do 26. 10. strom ukazoval názvy tříd a interní hodnoty („DepartureGrid",
/// „[OnEveryPage]", id skupiny), `B194` E1.
/// </summary>
public static class TemplateTreeNames
{
    public static string Sablona(Template sablona) => $"Šablona: {sablona.Name}";

    public static string Stranka(PageSize ps) =>
        $"Stránka: {ps.Width:0.#} × {ps.Height:0.#} mm, okraje vlevo {ps.MarginLeft:0.#}, nahoře {ps.MarginTop:0.#}, "
        + $"vpravo {ps.MarginRight:0.#}, dole {ps.MarginBottom:0.#} mm";

    /// <summary>Název sekce, typ z formuláře sekce a výška. Sekce pojmenovaná jako její typ ho neopakuje.</summary>
    public static string Sekce(Section sekce)
    {
        string typ = NazevTypuSekce(sekce.Typ);
        string vyska = sekce.DesignerHeight is { } h ? $"{h:0.#} mm" : "výška podle obsahu";
        return string.Equals(sekce.Name, typ, StringComparison.CurrentCultureIgnoreCase)
            ? $"{typ}, {vyska}"
            : $"{sekce.Name} - {typ}, {vyska}";
    }

    public static string Skupina(int pocetPrvku) => $"Skupina ({pocetPrvku} {Prvku(pocetPrvku)})";

    public static string Prvek(TemplateElement prvek) => ElementNames.SPopisem(prvek);

    /// <summary>Typ sekce tak, jak ho nabízí formulář sekce (<c>SectionEdit.cls</c>).</summary>
    public static string NazevTypuSekce(TypSekce typ) => typ switch
    {
        TypSekce.HlavickaStranky   => "Hlavička stránky",
        TypSekce.PodObsahemStranky => "Pod obsahem stránky",
        TypSekce.PatickaStranky    => "Patička stránky",
        _                          => "Obsah",
    };

    /// <summary>„prvek" / „prvky" / „prvků" podle počtu.</summary>
    public static string Prvku(int pocet) => pocet == 1 ? "prvek" : pocet is >= 2 and <= 4 ? "prvky" : "prvků";
}
