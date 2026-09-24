namespace TemplateDesigner.Models;

/// <summary>
/// Hlášky za **všechny vybrané linky** najednou (`B178`) - tiskové okno z nich řekne „vadí jen
/// linka X" dřív, než uživatel pošle sto PDF dál.
/// </summary>
/// <remarks>
/// Zjednodušená kontrola bez kreslení: sazba (přetečení, Počet stran, zmenšení tabulky) a hlášky
/// z dat (zmenšené písmo, malý kód, úzký sloupec, odjezdy mimo sloupce). Oříznuté buňky se poznají
/// až při kreslení, proto tu nejsou - ukazuje je náhled v návrháři.
/// <para>Stejná hláška z víc linek se sloučí do jedné a řekne, kterých linek se týká; jinak by
/// malý QR kód na padesáti linkách dal padesát řádků o jedné věci.</para>
/// </remarks>
public static class KontrolaVystupu
{
    /// <summary>Hodnota, kterou tiskové okno připíše každé stránce dat: popis linky („číslo - název").</summary>
    public const string LinkaKlic = "__LINKA_PRO_HLASKY";

    /// <summary>Stránky dat po linkách, v pořadí výběru.</summary>
    public static List<(string Linka, IReadOnlyList<DataContext> Stranky)> PoLinkach(IReadOnlyList<DataContext> stranky) =>
        [.. stranky
            .GroupBy(s => s.GetValue(LinkaKlic)?.ToString() ?? "")
            .Select(g => (g.Key, (IReadOnlyList<DataContext>)g.ToList()))];

    /// <param name="sazba">Hlášky ze sazby (přetečení, Počet stran …) - počítá je náhled,
    /// protože zná výšky prvků.</param>
    public static List<Hlaseni> ZLinky(Template template, IReadOnlyList<DataContext> data,
        Func<IReadOnlyList<DataContext>, IReadOnlyList<Hlaseni>> sazba) =>
    [
        .. new IEnumerable<Hlaseni>[]
        {
            sazba(data),
            ContentShrinkWarnings.Seznam(template, data),
            BarcodeSizeWarnings.Seznam(template, data),
            TripColumnWidthWarnings.Seznam(template, data),
            DepartureColumnWarnings.Seznam(data),
        }.SelectMany(h => h).Distinct(),
    ];

    /// <summary>Sloučí hlášky linek: stejná hláška z víc linek = jeden řádek, který je jmenuje.</summary>
    public static List<Hlaseni> Sluc(Template template, IReadOnlyList<(string Linka, List<Hlaseni> Hlaseni)> linky)
    {
        var vysledek = linky
            .SelectMany(l => l.Hlaseni.Select(h => (l.Linka, H: h)))
            .GroupBy(n => n.H)
            .Select(g => g.Key with { Kde = PredponaLinek([.. g.Select(n => n.Linka).Distinct()], linky.Count) + g.Key.Kde })
            .ToList();
        if (FontWarnings.Hlaska(template) is { } font) vysledek.Add(font);
        return [.. vysledek.OrderByDescending(h => h.Zavaznost)];
    }

    /// <summary>Kontrola naráz - pro testy a místa, kde se průběh neukazuje.</summary>
    public static List<Hlaseni> Souhrn(Template template, IReadOnlyList<DataContext> stranky,
        Func<IReadOnlyList<DataContext>, IReadOnlyList<Hlaseni>> sazba) =>
        Sluc(template, [.. PoLinkach(stranky).Select(l => (l.Linka, ZLinky(template, l.Stranky, sazba)))]);

    /// <summary>Kterých linek se hláška týká; u jediné vybrané linky nic (je jasné, o kterou jde).</summary>
    private static string PredponaLinek(List<string> tyto, int celkem)
    {
        if (celkem <= 1) return "";
        if (tyto.Count == celkem) return $"Všechny linky ({celkem}) · ";
        var jmena = tyto.Where(t => t.Length > 0).Take(3).ToList();
        string dalsi = tyto.Count > jmena.Count ? $" a {tyto.Count - jmena.Count} dalších" : "";
        return $"{string.Join(", ", jmena)}{dalsi} · ";
    }
}
