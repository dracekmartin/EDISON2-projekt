namespace TemplateDesigner.Models;

/// <summary>
/// Kontrakt prvek ↔ agenda (<c>B194g</c> R3): položky dat agendy, které čte kód návrháře - prvky (Seznam
/// zastávek, Minuty odjezdů, Poznámky, Tabulka spojů), sazba a navigace stránek. Uživatel je v nabídce dat
/// nehledá, nesou strukturu, kterou prvek kreslí. Dokud kontrakt nebyl zapsaný, chybějící položka se
/// projevila jen prázdným prvkem a jiný tvar shodil náhled (atributy zastávky, 8. 10.).
/// </summary>
/// <remarks>
/// Hlídají ho dva testy: <c>KontraktDatTests</c> (kód návrháře čte z dat jen položky odsud a každou
/// odsud opravdu čte) a integrační <c>KontraktDatAgendTests</c> (agendy je na skutečném serveru posílají
/// ve správném tvaru). Soubor je bez závislostí, integrační testy ho přilinkují.
/// </remarks>
public static class KontraktDat
{
    /// <summary>Druh agendy podle prvků, které obsluhuje (<c>ElementKind</c> v registru agend).</summary>
    public const string Zastavkove = "STOP";
    public const string Linkove = "LINE";
    public const string Kazda = "*";

    /// <summary>Údaje stránky (<c>values</c>); jiná kolekce = řádky v <c>rows</c>.</summary>
    public const string Udaje = "udaje";

    public enum Tvar
    {
        /// <summary>Text, číslo nebo příznak - kód ho čte jako text.</summary>
        Hodnota,
        /// <summary>Pole hodnot.</summary>
        PoleHodnot,
        /// <summary>Pole objektů; kód z každého čte <see cref="Polozka.VnoreneKlice"/>.</summary>
        PoleObjektu,
    }

    /// <param name="Volitelna">Proč položka nechodí vždy (parametr, jen některé řádky, jen porovnání verzí).
    /// Null = agenda daného druhu ji posílá v každém řádku.</param>
    /// <param name="VnoreneKlice">U pole objektů klíče, které kód čte z každého objektu.</param>
    public sealed record Polozka(string Druh, string Kolekce, string Klic, Tvar Tvar,
        string? Volitelna = null, string[]? VnoreneKlice = null);

    private const string NavigaceStranek = "popisek stránky v navigaci návrháře a udržení pozice po obnovení dat; bez něj obecný popis";
    private const string PorovnaniVerzi = "jen porovnání verzí";

    public static readonly IReadOnlyList<Polozka> Polozky =
    [
        // ── Každá agenda: navigace stránek v okně návrháře (TemplateDesignerViewModel) ──
        new(Kazda, Udaje, "POPIS_ZASTAVKA", Tvar.Hodnota, NavigaceStranek),
        new(Kazda, Udaje, "CISLO_ZASTAVKA", Tvar.Hodnota, NavigaceStranek),
        new(Kazda, Udaje, "NASTUPISTE", Tvar.Hodnota, NavigaceStranek),
        new(Kazda, Udaje, "VYCHOZI_ZASTAVKA", Tvar.Hodnota, NavigaceStranek),
        new(Kazda, Udaje, "KONECNA_ZASTAVKA", Tvar.Hodnota, NavigaceStranek),
        new(Kazda, Udaje, "SMER", Tvar.Hodnota, NavigaceStranek + "; sazba podle něj rozlišuje obousměrnou stránku"),

        // ── Zastávkové JŘ: Seznam zastávek, Minuty odjezdů, Poznámky ──
        new(Zastavkove, Udaje, "VARIANTY_TRAS", Tvar.PoleHodnot),
        new(Zastavkove, Udaje, "ZDROJ_STANOVISTE", Tvar.Hodnota),
        new(Zastavkove, Udaje, "PRESETY_SLOUPCU", Tvar.Hodnota),

        new(Zastavkove, "ZASTAVKY", StopFields.Id, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.StopName, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.TravelTime, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.Poradi, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.JeAktualni, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", "ODSAZENI", Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", "NA_ZNAMENI", Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.Nastupiste, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.NastupisteSpoju, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.Sloupek, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.SloupekSpoju, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.SloupekPopis, Tvar.Hodnota),
        new(Zastavkove, "ZASTAVKY", StopFields.Zone, Tvar.PoleHodnot),
        new(Zastavkove, "ZASTAVKY", "ATRIBUTY", Tvar.PoleObjektu, VnoreneKlice: ["ZNAK", "POPIS"]),
        new(Zastavkove, "ZASTAVKY", "POZNAMKY_ZASTAVKY", Tvar.PoleObjektu, VnoreneKlice: ["ZNAK", "TYP", "TEXT"]),
        new(Zastavkove, "ZASTAVKY", "TRASA_MIN", Tvar.PoleHodnot),
        new(Zastavkove, "ZASTAVKY", "NASTUPISTE_VARIANT", Tvar.PoleHodnot),
        new(Zastavkove, "ZASTAVKY", "SLOUPEK_VARIANT", Tvar.PoleHodnot),

        new(Zastavkove, "SKUPINY_DNU", "ID", Tvar.Hodnota),
        new(Zastavkove, "SKUPINY_DNU", "NADPIS", Tvar.Hodnota),
        new(Zastavkove, "SKUPINY_DNU", "NADPIS2", Tvar.Hodnota),
        new(Zastavkove, "SKUPINY_DNU", "PORADI", Tvar.Hodnota),

        new(Zastavkove, "ODJEZDY", "ZASTAVKA_ID", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "SKUPINA_DNU_ID", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "HODINA", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "MINUTA", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "FORE_COLOR", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "BACK_COLOR", Tvar.Hodnota),
        new(Zastavkove, "ODJEZDY", "ANOTACE", Tvar.PoleObjektu,
            VnoreneKlice: ["TYP", "ZNAK", "KATEGORIE", "FORE_COLOR", "BACK_COLOR", "V_ROZSAHU_NEPOZNAMKOVAT"]),

        new(Zastavkove, "POZNAMKY", "TYP", Tvar.Hodnota),
        new(Zastavkove, "POZNAMKY", "ZNAK", Tvar.Hodnota),
        new(Zastavkove, "POZNAMKY", "TEXT", Tvar.Hodnota),
        new(Zastavkove, "POZNAMKY", "FORE_COLOR", Tvar.Hodnota),
        new(Zastavkove, "POZNAMKY", "BACK_COLOR", Tvar.Hodnota),
        new(Zastavkove, "POZNAMKY", "KATEGORIE", Tvar.Hodnota),

        // ── Linkové JŘ: Tabulka spojů, Poznámky ──
        // Sazba stránek z něj skládá dopravce za stránku (#9417) a dopravce po verzích (PageLayoutEngine).
        new(Linkove, Udaje, "POPIS_DOPRAVCE", Tvar.Hodnota),
        new(Linkove, Udaje, "VYLEP_ZASTAVKY", Tvar.Hodnota, "jen stránka výlepu pro zastávku"),

        new(Linkove, "ZASTAVKY", "PORADI", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "AKTUALNI_ZASTAVKA", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "TYP", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "NAZEV_ZASTAVKY", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "TC", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "ZONY", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "MHD", Tvar.Hodnota),
        new(Linkove, "ZASTAVKY", "NASTUPISTE", Tvar.Hodnota),
        // ⚠️ Jiný tvar než u zastávkových JŘ (tam pole objektů): Tabulka spojů čte jen znaky.
        new(Linkove, "ZASTAVKY", "ATRIBUTY", Tvar.PoleHodnot),
        new(Linkove, "ZASTAVKY", "KM", Tvar.PoleHodnot),
        new(Linkove, "ZASTAVKY", "VERZE", Tvar.Hodnota, PorovnaniVerzi),
        new(Linkove, "ZASTAVKY", "TC_PUVODNI", Tvar.Hodnota, PorovnaniVerzi),
        new(Linkove, "ZASTAVKY", "ZONY_PUVODNI", Tvar.Hodnota, PorovnaniVerzi),

        new(Linkove, "SPOJE", "CISLO_SPOJE", Tvar.Hodnota),
        new(Linkove, "SPOJE", "SKUPINA", Tvar.Hodnota),
        new(Linkove, "SPOJE", "SMER", Tvar.Hodnota),
        new(Linkove, "SPOJE", "NZ", Tvar.Hodnota),
        new(Linkove, "SPOJE", "FORE_COLOR", Tvar.Hodnota, "jen se zapnutým parametrem Barvy spojů"),
        new(Linkove, "SPOJE", "PLATNOST", Tvar.PoleHodnot),
        new(Linkove, "SPOJE", "TEXTOVE_ZNACKY", Tvar.PoleHodnot, "jen spoj, který textovou značku má"),
        new(Linkove, "SPOJE", "TURNUSY", Tvar.PoleHodnot, "jen se zapnutými turnusy ke spojům"),
        new(Linkove, "SPOJE", "CASY", Tvar.PoleHodnot),
        new(Linkove, "SPOJE", "PRIPOJE", Tvar.PoleObjektu,
            VnoreneKlice: ["TYP", "PORADI", "TEXT", "CAS", "LINKA", "SPOJ", "AVL", "RADEK"]),
        new(Linkove, "SPOJE", "DOPRAVCI", Tvar.PoleHodnot),
        new(Linkove, "SPOJE", "ZNACKY", Tvar.PoleHodnot, PorovnaniVerzi),
        new(Linkove, "SPOJE", "VERZE", Tvar.Hodnota, PorovnaniVerzi),
        new(Linkove, "SPOJE", "ROZDILY", Tvar.PoleHodnot, PorovnaniVerzi),
        new(Linkove, "SPOJE", "PLATNOST_VERZE", Tvar.PoleHodnot, PorovnaniVerzi + ", když se platnost liší"),
        new(Linkove, "SPOJE", "NZ_PUVODNI", Tvar.Hodnota, PorovnaniVerzi),
        new(Linkove, "SPOJE", "NZ_VERZE", Tvar.Hodnota, PorovnaniVerzi),
        new(Linkove, "SPOJE", "DOPRAVCI_PUVODNI", Tvar.PoleHodnot, PorovnaniVerzi + ", když se dopravce liší"),

        new(Linkove, "SKUPINY_DNU", "ID", Tvar.Hodnota),
        new(Linkove, "SKUPINY_DNU", "NADPIS", Tvar.Hodnota),

        new(Linkove, "POZNAMKY", "TYP", Tvar.Hodnota),
        new(Linkove, "POZNAMKY", "ZNAK", Tvar.Hodnota),
        new(Linkove, "POZNAMKY", "TEXT", Tvar.Hodnota),
        new(Linkove, "POZNAMKY", "DRUH", Tvar.Hodnota),
        new(Linkove, "POZNAMKY", "VERZE", Tvar.Hodnota, PorovnaniVerzi),
    ];
}
