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

    private readonly Dictionary<string, int> _pocty = [];

    /// <summary>Ohlásí jednu oříznutou buňku. Volá <see cref="CellRenderer"/>.</summary>
    public void Report()
    {
        string nazev = CurrentElement ?? "Prvek";
        _pocty[nazev] = _pocty.TryGetValue(nazev, out int n) ? n + 1 : 1;
    }

    /// <summary>Text do stavového řádku, nebo null když se všechno vešlo.</summary>
    public string? Summarize()
    {
        if (_pocty.Count == 0) return null;
        // Počet buněk, ne seznam textů: v tabulce se ořízne celý sloupec naráz a výčet čtyřiceti
        // názvů zastávek by hlášku roztáhl přes celý řádek.
        var casti = _pocty.OrderBy(p => p.Key).Select(p => p.Value == 1
            ? $"{p.Key}: obsah jedné buňky je na zmenšení moc dlouhý, ořízl se"
            : $"{p.Key}: obsah {p.Value} buněk je na zmenšení moc dlouhý, ořízl se");
        return string.Join("; ", casti);
    }
}
