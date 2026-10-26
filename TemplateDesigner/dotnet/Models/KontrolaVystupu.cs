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

    /// <summary>
    /// Linka bez jediné stránky (`B193g`): problém, protože se z ní nevytiskne nic. Rada = důvod od
    /// agendy (co v datech chybí a co s tím). Klik přepne na linku.
    /// </summary>
    /// <remarks>Do 23. 10. byla jen textem ve stavovém řádku, takže se nedala prokliknout a v tiskovém
    /// okně ji kontrola neukázala vůbec (rozhodnutí Jan: patří mezi hlášky, červeně).</remarks>
    public static Hlaseni LinkaBezDat(string linka, string? duvod) =>
        new(ZavaznostHlaseni.Chyba, linka, "nemá data, nevytiskne se",
            string.IsNullOrWhiteSpace(duvod) ? "zkontrolujte jízdní řád linky" : duvod) { Linka = linka };

    /// <summary>Linka, kterou zastavila chyba v jejích datech - agenda řekne, co opravit (`B194a` C11).</summary>
    public static Hlaseni LinkaChybaDat(string linka, string text) =>
        new(ZavaznostHlaseni.Chyba, linka, "nejde vytisknout, tisk se zastavil", text) { Linka = linka };

    /// <summary>Hlášky linek, které nedaly data - před hláškami ostatních linek.</summary>
    public static List<Hlaseni> SBezDat(IReadOnlyList<(string Linka, string? Duvod)>? bezDat, IEnumerable<Hlaseni> ostatni) =>
        [.. (bezDat ?? []).Select(v => LinkaBezDat(v.Linka, v.Duvod)), .. ostatni];

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
            .Select(g =>
            {
                var tyto = g.Select(n => n.Linka).Distinct().ToList();
                return g.Key with
                {
                    Kde = PredponaLinek(tyto, linky.Count) + g.Key.Kde,
                    Linka = tyto.FirstOrDefault(t => t.Length > 0),
                };
            })
            .ToList();
        if (FontWarnings.Hlaska(template) is { } font) vysledek.Add(font);
        return [.. vysledek.OrderByDescending(h => h.Zavaznost)];
    }

    /// <summary>
    /// Kontrola po linkách s průběhem (tiskové okno i návrhář). Sazba běží na vlákně volajícího -
    /// sdílí cache prvků s náhledem -, proto se mezi linkami pustí kreslení, jinak by průběh stál.
    /// </summary>
    public static async Task<List<Hlaseni>> ZkontrolujAsync(Template template, IReadOnlyList<DataContext> stranky,
        Func<IReadOnlyList<DataContext>, IReadOnlyList<Hlaseni>> sazba, Action<int, int, string>? prubeh = null)
    {
        var linky = PoLinkach(stranky);
        var vysledky = new List<(string Linka, List<Hlaseni> Hlaseni)>();
        for (int i = 0; i < linky.Count; i++)
        {
            prubeh?.Invoke(i, linky.Count, linky[i].Linka);
            await Task.Delay(1);
            vysledky.Add((linky[i].Linka, ZLinky(template, linky[i].Stranky, sazba)));
        }
        prubeh?.Invoke(linky.Count, linky.Count, "");
        return Sluc(template, vysledky);
    }

    /// <summary>„2 chyby, 1 upozornění, 3 informace".</summary>
    public static string SouhrnPoctu(IReadOnlyList<Hlaseni> hlaseni)
    {
        var casti = new List<string>();
        int ch = hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Chyba);
        int up = hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Upozorneni);
        int inf = hlaseni.Count(h => h.Zavaznost == ZavaznostHlaseni.Informace);
        // „Problém", ne „chyba" - jde o vadu v sestavě, ne v programu (rozhodnutí Jan 24. 9.).
        if (ch > 0) casti.Add($"{ch} {(ch == 1 ? "problém" : ch <= 4 ? "problémy" : "problémů")}");
        if (up > 0) casti.Add($"{up} upozornění");
        if (inf > 0) casti.Add($"{inf} {(inf <= 4 ? "informace" : "informací")}");
        return string.Join(", ", casti);
    }

    public static string Linek(int n) => $"{n} {(n == 1 ? "linka" : n <= 4 ? "linky" : "linek")}";

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
