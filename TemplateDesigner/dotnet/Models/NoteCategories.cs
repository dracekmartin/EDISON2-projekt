namespace TemplateDesigner.Models;

/// <summary>
/// Kategorie, kterými Caché taguje značky a řádky legendy - **jediný zdroj pravdy** pro jejich jména
/// na .NET straně.
///
/// ⚠️ Proč to je tady a ne rozeseté po prvcích: na jménu kategorie visí dvě nezávislé věci -
/// (1) co se v šabloně vypíná (<see cref="NoteVisibilityFilter"/>) a (2) jestli se značka **přeloží
/// na glyf** symbolového fontu (<see cref="Elements.StopScheduleElementBase.GlyfDleKategorie"/>).
/// Když se kategorie 4. 8. přejmenovala z „odjezd-atribut" na dvě zvlášť, druhé místo zůstalo na
/// starém jménu → kolo se vykreslilo jako cizí glyf, a testy to neodhalily, protože si to staré
/// jméno držely samy. Konstanta tuhle třídu chyb odstraňuje.
///
/// Jména musí souhlasit s tím, co posílá agenda (`ZastavkoveJrAd`); Caché strana je autorita.
/// </summary>
public static class NoteCategories
{
    /// <summary>Přestup na jiný druh dopravy (značka „A" autobus / „L" vlak) - sémantická, překládá se.</summary>
    public const string Prestup = "prestup";

    /// <summary>Přeprava jízdních kol u odjezdu - sémantická (marker „K" → glyf kola).</summary>
    public const string OdjezdKola = "odjezd-kola";

    /// <summary>Místenka u odjezdu („R", kreslí se textem) - kategorie kvůli vypínání v šabloně.</summary>
    public const string OdjezdMistenka = "odjezd-mistenka";

    /// <summary>Pokračování spoje na jinou linku - značka `u1`, `u2`… (#13093 bod 3). Značky
    /// přiděluje server per stránka; víceznakové, takže se kreslí textem, ne symbolovým fontem.</summary>
    public const string Pokracovani = "pokracovani";

    /// <summary>Varianta trasy u odjezdu - písmeno, které říká, kterým variantním sloupcem časů se
    /// spoj řídí. Vysvětlením je záhlaví toho sloupce, ne řádek legendy; proto se kategorie tiskne
    /// jen tehdy, když seznam zastávek sloupec Varianty tras skutečně má.</summary>
    public const string VariantaTrasy = "varianta-trasy";

    /// <summary>Atribut zastávky (na znamení a spol.).</summary>
    public const string AtributZastavky = "atribut-zastavky";

    /// <summary>Týdenní platnost spoje (X, 6, 7, +) - zastávková AD; linková ji posílá jako druh 1.</summary>
    public const string TydenniPlatnost = "tydenni-platnost";

    /// <summary>Souhrn „všechny spoje mají bezbariérově přístupné vozidlo".</summary>
    public const string BezbarierAgregat = "bezbarier-agregat";

    /// <summary>Výluková poznámka linky (podmnožina všeobecných, poznaná podle prefixu textu).</summary>
    public const string Vyluka = "vyluka";

    /// <summary>Poznámky zadané u linky - dělení dle druhu ve vlastnostech linky.</summary>
    public const string PoznamkaLinkyTarifni = "poznamka-linky-tarifni";

    /// <inheritdoc cref="PoznamkaLinkyTarifni"/>
    public const string PoznamkaLinkyVseobecne = "poznamka-linky-vseobecne";

    /// <inheritdoc cref="PoznamkaLinkyTarifni"/>
    public const string PoznamkaLinkyRadiobus = "poznamka-linky-radiobus";

    /// <summary>
    /// Kategorie, jejichž značka nese **význam** a musí se přeložit na glyf symbolového fontu.
    /// Ostatní značky jsou v datech **už jako pozice ve fontu** (agenda MHD posílá <c>$c(kód)</c>)
    /// a přeložit se nesmí - jinak vyjede cizí piktogram.
    /// </summary>
    public static readonly IReadOnlySet<string> Semanticke =
        new HashSet<string> { Prestup, OdjezdKola, OdjezdMistenka };
}
