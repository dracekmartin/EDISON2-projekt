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
/// </remarks>
public sealed class ClippedCells
{
    /// <summary>Prvek, který se právě kreslí - jeho jméno jde do hlášky. Nastavuje kreslicí smyčka.</summary>
    public string? CurrentElement { get; set; }

    /// <summary>Id prvku, který se právě kreslí - klik na hlášku ho vybere. Nastavuje kreslicí smyčka.</summary>
    public Guid? CurrentElementId { get; set; }

    private readonly Dictionary<string, Guid?> _idPrvku = [];

    private readonly Dictionary<string, int> _pocty = [];

    /// <summary>Ohlásí jednu oříznutou buňku. Volá <see cref="CellRenderer"/>.</summary>
    public void Report()
    {
        string nazev = CurrentElement ?? "Prvek";
        _pocty[nazev] = _pocty.TryGetValue(nazev, out int n) ? n + 1 : 1;
        _idPrvku.TryAdd(nazev, CurrentElementId);
    }

    private readonly HashSet<string> _texty = [];

    /// <summary>
    /// Ohlásí textový prvek, který se nevešel ani po zmenšení písma. Volá <c>TextElement</c>.
    /// </summary>
    /// <remarks>
    /// Kreslí se s daty té stránky sazby, takže hláška sedí i na pole, které sazba plní až po
    /// rozvržení (dopravci za spoje na stránce), a jde se změnou výšky prvku, ne až s dalším
    /// načtením dat.
    /// </remarks>
    public void ReportText()
    {
        string nazev = CurrentElement ?? "Text";
        _texty.Add(nazev);
        _idPrvku.TryAdd(nazev, CurrentElementId);
    }

    /// <summary>Hlášky po prvcích - oříznutý obsah na papíře chybí, proto chyba.</summary>
    public List<Hlaseni> Seznam()
    {
        var bunky = _pocty.OrderBy(p => p.Key).Select(p => new Hlaseni(ZavaznostHlaseni.Chyba, p.Key,
            p.Value == 1
                ? "obsah jedné buňky se nevejde ani po zmenšení písma, ořízl se"
                : $"obsah {p.Value} buněk se nevejde ani po zmenšení písma, ořízl se",
            "rozšířit sloupec", _idPrvku.GetValueOrDefault(p.Key)));
        var texty = _texty.Order().Select(n => new Hlaseni(ZavaznostHlaseni.Chyba, n,
            "text se nevejde ani po zmenšení písma, ořízl se",
            "zvětšit prvek, nebo nechat výšku prázdnou", _idPrvku.GetValueOrDefault(n)));
        return [.. bunky, .. texty];
    }

    /// <summary>Text do stavového řádku, nebo null když se všechno vešlo.</summary>
    public string? Summarize()
    {
        var texty = _texty.Order().Select(n =>
            $"{n}: text se nevejde ani po zmenšení písma, ořízl se - zvětšete prvek nebo nechte výšku prázdnou");
        if (_pocty.Count == 0)
        {
            var jenTexty = texty.ToList();
            return jenTexty.Count == 0 ? null : string.Join("; ", jenTexty);
        }
        // Počet buněk, ne seznam textů: v tabulce se ořízne celý sloupec naráz a výčet čtyřiceti
        // názvů zastávek by hlášku roztáhl přes celý řádek.
        var casti = _pocty.OrderBy(p => p.Key).Select(p => p.Value == 1
            ? $"{p.Key}: obsah jedné buňky je na zmenšení moc dlouhý, ořízl se"
            : $"{p.Key}: obsah {p.Value} buněk je na zmenšení moc dlouhý, ořízl se");
        return string.Join("; ", casti.Concat(texty));
    }
}
