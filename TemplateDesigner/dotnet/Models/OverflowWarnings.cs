namespace TemplateDesigner.Models;

/// <summary>Kdo sekci vytlačil za hranu stránky - liší se tím, co má uživatel zmenšit.</summary>
public enum DruhPreteceni
{
    /// <summary>Sekci nafoukla její **nastavená výška** (obsah je menší) - zmenšit sekci.</summary>
    NastavenaVyskaSekce,

    /// <summary>Sekci nafoukl **obsah prvku** (typicky dynamická výška z dat) - zmenšit ten prvek.</summary>
    ObsahPrvku,
}

/// <summary>
/// Jeden nález přetečení: sekce, která se nevejde na stránku, a kdo ji nafoukl.
/// <paramref name="PresahMm"/> = o kolik sekce přesahuje spodní hranu tiskové plochy.
/// </summary>
public record PreteceniNalez(
    int CisloStranky,
    DruhPreteceni Druh,
    string Sekce,
    string? Prvek,
    float PresahMm,
    Guid? PrvekId = null);

/// <summary>
/// Souhrn toho, co se nevejde a kde. Ukazuje se v návrháři (stavový řádek u červeného rámečku
/// stránky) i v souhrnu tisku.
/// </summary>
/// <remarks>
/// Do 3. 9. hlásil náhled jen „Obsah se nevejde na stránku" - uživatel věděl, že je problém, ale
/// ne na které stránce ani co ji přetéká, takže neměl kde začít (podnět kolega 3. 9.). Přitom
/// náhled si přetečení počítá pro každou stránku zvlášť a tu informaci zahazoval.
///
/// <para>⚠️ **Prvek nemůže přesáhnout svou sekci** - engine dává sekci
/// <c>max(nastavená výška, obsah)</c>, takže rostoucí prvek ji nafoukne s sebou. Reálné přetečení
/// je proto vždy „sekce se nevejde na stránku"; užitečná informace je, **kdo ji nafoukl** (odtud
/// <see cref="DruhPreteceni"/>). Do 3. 9. tady byla i podmínka „prvek přesahuje sekci", která
/// z tohoto důvodu nemohla nastat.</para>
///
/// <para>Týž vzor jako <see cref="ContentShrinkWarnings"/> a <see cref="DepartureColumnWarnings"/> -
/// pojmenovat konkrétní věc, ne obecně varovat (kvůli obecnosti byl zrušený signál `B24`).</para>
/// </remarks>
public static class OverflowWarnings
{
    /// <summary>Text upozornění, nebo null když se všechno vejde.</summary>
    public static string? Summarize(IReadOnlyList<PreteceniNalez> nalezy, int celkemStranek) =>
        Seznam(nalezy, celkemStranek) is { Count: > 0 } s ? string.Join("; ", s.Select(h => h.Text)) : null;

    /// <summary>Hlášky po vinících - přetečení stránky je chyba, obsah se neztratí jen náhodou.</summary>
    public static List<Hlaseni> Seznam(IReadOnlyList<PreteceniNalez> nalezy, int celkemStranek)
    {
        if (nalezy.Count == 0) return [];

        // Seskupit podle vinníka, ne podle stránky: u vývěsky se stránkou na zastávku je tentýž
        // prvek přes všechny stránky, takže výpis po stránkách by dal dvacet řádků o jedné chybě.
        var skupiny = nalezy
            .GroupBy(n => (n.Druh, n.Sekce, n.Prvek))
            .Select(g => new
            {
                g.Key.Druh,
                g.Key.Sekce,
                g.Key.Prvek,
                Stranky = g.Select(n => n.CisloStranky).Distinct().OrderBy(c => c).ToList(),
                NejvetsiPresah = g.Max(n => n.PresahMm),
                PrvekId = g.Select(n => n.PrvekId).FirstOrDefault(id => id is not null),
            })
            .OrderByDescending(s => s.NejvetsiPresah)
            .ToList();

        return [.. skupiny.Select(s =>
        {
            string kde = s.Druh == DruhPreteceni.ObsahPrvku
                ? $"{s.Prvek} v sekci „{s.Sekce}“ · {PopisStranek(s.Stranky, celkemStranek)}"
                : $"Sekce „{s.Sekce}“ · {PopisStranek(s.Stranky, celkemStranek)}";
            return new Hlaseni(ZavaznostHlaseni.Chyba, kde,
                $"nevejde se na stránku, přesahuje o {s.NejvetsiPresah:0.#} mm", Rada(s.Druh), s.PrvekId);
        })];
    }

    /// <summary>Co s tím má autor šablony udělat.</summary>
    /// <remarks>
    /// ⚠️ Hlásí se **jen** sekce, která má držet pohromadě (viz volající), takže rada není dohad:
    /// obsah, který narostl z dat, se vejde, jen se sekce musí smět rozdělit přes stránky - přesně to dělá
    /// i starý tisk, když se tabulka nevejde do šířky (řeže ji do bloků a pokračuje dál). Bez rady bylo
    /// varování slepá ulice: šablona linkového JŘ se rozvržením do dvou bloků zdvojnásobí a šlo z toho
    /// jen zmenšovat písmo, i když stačí přehodit jedno nastavení sekce (nález Jan 7. 9.).
    /// </remarks>
    private static string Rada(DruhPreteceni druh) => druh == DruhPreteceni.ObsahPrvku
        ? "sekci rozdělit přes stránky, nebo prvek zmenšit"
        : "zmenšit výšku sekce";

    /// <summary>
    /// Které stránky to potkalo. Čísla se vypisují do tří stránek; nad to je užitečnější počet
    /// a první výskyt, protože „strany 1, 2, 3, 4, 5, 6, …“ v jednom řádku stavu nikdo nepřečte.
    /// </summary>
    private static string PopisStranek(List<int> stranky, int celkemStranek)
    {
        if (stranky.Count == 1) return $"strana {stranky[0]}";
        if (stranky.Count <= 3) return "strany " + string.Join(", ", stranky);

        string zCelku = celkemStranek > 0 ? $" z {celkemStranek}" : "";
        return $"{stranky.Count}{zCelku} stránek, první je {stranky[0]}";
    }
}
