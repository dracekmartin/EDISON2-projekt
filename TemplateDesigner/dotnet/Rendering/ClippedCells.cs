using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Buňky, jejichž obsah se nevešel ani po zmenšení na mez (<see cref="CellRenderer.MinScale"/>),
/// takže se ořízl na hraně buňky. Sbírá se **při kreslení**, ne dopředným výpočtem - jedině tak
/// hláška nemůže tvrdit něco jiného, než co je vidět na papíře.
/// </summary>
/// <remarks>
/// Šířka sloupce vzniká až v rozvržení prvku (auto sloupce si dělí zbytek, a když se tabulka
/// nevejde, zmenší se všechny), takže dopředu by se musela celá ta matematika spočítat podruhé.
/// Vzor je tentýž jako u přetečení stránky: sesbírat při kreslení a poslat jako text do stavového
/// řádku (<c>PreviewCanvas.OverflowChanged</c>).
/// <para>Hláška jmenuje i <b>text buňky a stranu</b>: samotné jméno prvku nestačilo poznat, která
/// z tisíců buněk tabulky to je, a tedy ani to, jestli je hláška oprávněná (nález Jan 15. 10.).</para>
/// </remarks>
public sealed class ClippedCells
{
    /// <summary>Kolik textů buněk hláška vyjmenuje; zbytek shrne „a další".</summary>
    private const int UkazekTextu = 3;

    /// <summary>Delší text buňky se v hlášce zkrátí, aby se vešla do řádku.</summary>
    private const int DelkaUkazky = 40;

    /// <summary>Prvek, který se právě kreslí - jeho jméno jde do hlášky. Nastavuje kreslicí smyčka.</summary>
    public string? CurrentElement { get; set; }

    /// <summary>Id prvku, který se právě kreslí - klik na hlášku ho vybere. Nastavuje kreslicí smyčka.</summary>
    public Guid? CurrentElementId { get; set; }

    /// <summary>Strana náhledu, která se právě kreslí (od 1); null mimo náhled. Nastavuje kreslicí smyčka.</summary>
    public int? CurrentPage { get; set; }

    private readonly Dictionary<string, Guid?> _idPrvku = [];

    private readonly Dictionary<string, Nalez> _bunky = [];

    private readonly Dictionary<string, Nalez> _texty = [];

    private sealed class Nalez
    {
        public int Pocet;
        public readonly SortedSet<int> Strany = [];
        public readonly List<string> Ukazky = [];
    }

    /// <summary>Ohlásí jednu oříznutou buňku s jejím textem. Volá <see cref="CellRenderer"/>.</summary>
    public void Report(string text) => Zapis(_bunky, CurrentElement ?? "Prvek", text);

    /// <summary>
    /// Ohlásí textový prvek, který se nevešel ani po zmenšení písma. Volá <c>TextElement</c>.
    /// </summary>
    /// <remarks>
    /// Kreslí se s daty té stránky sazby, takže hláška sedí i na pole, které sazba plní až po
    /// rozvržení (dopravci za spoje na stránce), a jde se změnou výšky prvku, ne až s dalším
    /// načtením dat.
    /// </remarks>
    public void ReportText() => Zapis(_texty, CurrentElement ?? "Text", null);

    private void Zapis(Dictionary<string, Nalez> kam, string nazev, string? text)
    {
        if (!kam.TryGetValue(nazev, out var nalez)) kam[nazev] = nalez = new Nalez();
        nalez.Pocet++;
        if (CurrentPage is int strana) nalez.Strany.Add(strana);
        if (Ukazka(text) is { } ukazka && nalez.Ukazky.Count <= UkazekTextu && !nalez.Ukazky.Contains(ukazka))
            nalez.Ukazky.Add(ukazka);
        _idPrvku.TryAdd(nazev, CurrentElementId);
    }

    /// <summary>Text buňky do hlášky: víc řádků jedním řádkem, dlouhý zkrácený.</summary>
    private static string? Ukazka(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string jedenRadek = string.Join(" / ", CellLayout.SplitLines(text).Select(r => r.Trim()));
        return jedenRadek.Length <= DelkaUkazky ? jedenRadek : jedenRadek[..(DelkaUkazky - 1)].TrimEnd() + "…";
    }

    /// <summary>Hlášky po prvcích - oříznutý obsah na papíře chybí, proto chyba.</summary>
    public List<Hlaseni> Seznam()
    {
        var bunky = _bunky.OrderBy(p => p.Key).Select(p => new Hlaseni(ZavaznostHlaseni.Chyba,
            SeStranami(p.Key, p.Value), CoSeStaloSBunkami(p.Value),
            "rozšířit sloupec", _idPrvku.GetValueOrDefault(p.Key)));
        var texty = _texty.OrderBy(p => p.Key).Select(p => new Hlaseni(ZavaznostHlaseni.Chyba,
            SeStranami(p.Key, p.Value), "text se nevejde ani po zmenšení písma, ořízl se",
            "zkrátit text, nebo zvětšit prvek", _idPrvku.GetValueOrDefault(p.Key)));
        return [.. bunky, .. texty];
    }

    /// <summary>Prvek a strany ve stejném tvaru jako hláška o přetečení stránky.</summary>
    private static string SeStranami(string prvek, Nalez nalez) => nalez.Strany.Count == 0
        ? prvek
        : $"{prvek} · {OverflowWarnings.PopisStranek([.. nalez.Strany], 0)}";

    private static string CoSeStaloSBunkami(Nalez nalez)
    {
        var ukazky = nalez.Ukazky.Take(UkazekTextu).Select(u => $"„{u}“").ToList();
        if (nalez.Pocet == 1)
            return ukazky.Count == 1
                ? $"obsah buňky {ukazky[0]} se nevejde ani po zmenšení písma, ořízl se"
                : "obsah jedné buňky se nevejde ani po zmenšení písma, ořízl se";

        string vycet = ukazky.Count == 0 ? ""
            : $" ({string.Join(", ", ukazky)}{(nalez.Ukazky.Count > UkazekTextu ? " a další" : "")})";
        return $"obsah {nalez.Pocet} buněk se nevejde ani po zmenšení písma, ořízl se{vycet}";
    }

    /// <summary>Text do stavového řádku, nebo null když se všechno vešlo.</summary>
    public string? Summarize() =>
        Seznam() is { Count: > 0 } s ? string.Join("; ", s.Select(h => h.Text)) : null;
}
