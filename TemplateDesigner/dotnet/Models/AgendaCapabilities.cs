namespace TemplateDesigner.Models;

/// <summary>
/// Které prvky jízdních řádů nabízí druh agendy (<c>AgendaRegistryEntry.ElementKind</c>) - paleta,
/// nabídka vložení, asistent i kontrola vkládání. Caché deklaruje jen druh ("STOP"/"LINE"/""), které
/// prvky k němu patří, říká <see cref="ElementCatalog"/> (<see cref="ElementCatalogEntry.AgendaKinds"/>).
/// Nová agenda existujícího druhu = nula změn; nový prvek JŘ = jeho druhy v katalogu.
/// </summary>
public static class AgendaCapabilities
{
    private static readonly Dictionary<string, string[]> TypyPodleDruhu = new(StringComparer.OrdinalIgnoreCase)
    {
        [KontraktDat.Zastavkove] = TypyDruhu(KontraktDat.Zastavkove),
        [KontraktDat.Linkove] = TypyDruhu(KontraktDat.Linkove),
    };

    private static string[] TypyDruhu(string druh) =>
        [.. ElementCatalog.All.Where(e => e.AgendaKinds.Contains(druh)).Select(e => e.Type)];

    /// <summary>JŘ typy elementů podporované daným druhem agendy. Prázdný/neznámý druh
    /// (GENERAL / fakturace / bez JŘ kontextu) → žádné JŘ elementy - ty patří jen ke své agendě.</summary>
    public static IReadOnlyList<string> ScheduleElementTypes(string? elementKind) =>
        elementKind is not null && TypyPodleDruhu.TryGetValue(elementKind, out var typy) ? typy : [];

    /// <summary>Podporuje daný druh agendy tento typ elementu? (řízení menu/palety).</summary>
    public static bool Supports(string? elementKind, string elementType) =>
        ScheduleElementTypes(elementKind).Contains(elementType);

    /// <summary>
    /// Výchozí název PDF při tisku do víc souborů (tokeny <c>{POLE}</c> z dat) - jen z polí, která
    /// druh agendy posílá. Prázdný = agenda bez polí JŘ, soubor dostane jméno vybrané instance.
    /// </summary>
    /// <remarks>Číslo linky je i u MHD licenční, ne veřejné: je vždy vyplněné a jednoznačné
    /// (rozhodnutí Jan 24. 10.), dával ho tak i starý tisk MHD (`TiskMHD/.../Linka.cls:99`).</remarks>
    public static string VychoziNazevSouboru(string? elementKind) =>
        elementKind?.ToUpperInvariant() switch
        {
            "STOP" => "{CISLO_LINKY}_{POPIS_ZASTAVKA}_{KONECNA_ZASTAVKA}",
            "LINE" => "{CISLO_LINKY}_{VYCHOZI_ZASTAVKA}_{KONECNA_ZASTAVKA}",
            _      => "",
        };

    private static readonly HashSet<string> AllScheduleTypes = [.. ElementCatalog.All.Where(e => !e.IsGeneric).Select(e => e.Type)];

    /// <summary>
    /// Smí prvek do šablony agendy daného druhu? Obecné prvky (text, tabulka, kód…) všude, prvky
    /// jízdních řádů jen do agendy, která je podporuje. Neznámý druh (<c>null</c> - registr agend
    /// se nenačetl) nic neblokuje.
    /// </summary>
    /// <remarks>
    /// Platí pro každé vložení do hotové šablony, nejen pro paletu: Seznam zastávek vložený ze
    /// schránky do linkového JŘ kreslil náhled nad daty jiného tvaru a shodil ho (POVED 8. 10.).
    /// Typ prvku = jméno třídy, stejně jako diskriminátor v JSON šablony (<see cref="ElementTypes"/>).
    /// </remarks>
    public static bool Allows(string? elementKind, TemplateElement element)
    {
        if (elementKind is null) return true;
        var typ = element.GetType().Name;
        return !AllScheduleTypes.Contains(typ) || Supports(elementKind, typ);
    }
}
