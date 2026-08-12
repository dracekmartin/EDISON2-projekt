using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Záhlaví skupin dnů u mřížky odjezdů: **ruční zlom** v přepisu nadpisu (#12843, TRANSDEV).
/// Dvouřádkové záhlaví dosud vzniklo jen z dat (druhý nadpis, vlastní styl); ruční zlom se zahodil,
/// protože se text jen zalamoval podle šířky - Enter ani token se nezpracovaly. Testy drží to, co se
/// u toho snadno rozbije: že ruční řádky mají **stejný** styl, že druhý nadpis z dat zůstává
/// sekundární, a že zlom nesmí roztáhnout minimální šířku skupiny.
/// </summary>
public class DepartureHeaderLinesTests
{
    /// <summary>Bez zalamování - test ověřuje skládání řádků, ne měření fontu.</summary>
    private static IEnumerable<string> BezWrapu(string radek) => [radek];

    [Fact]
    public void RucniZlom_DaDvaRadkyVeStejnemStylu()
    {
        var lines = DepartureGridElement.ComposeHeaderLines("Pracovní\ndny", "", BezWrapu);

        Assert.Equal(["Pracovní", "dny"], lines.Select(l => l.Text));
        Assert.All(lines, l => Assert.False(l.IsSecondary));
    }

    /// <summary>Token `\n` musí fungovat stejně jako Enter - v jednořádkovém poli je to jediná cesta.</summary>
    [Fact]
    public void TokenZpetneLomitkoN_SeChovaJakoEnter()
    {
        var enter = DepartureGridElement.ComposeHeaderLines("Pracovní\ndny", "", BezWrapu);
        var token = DepartureGridElement.ComposeHeaderLines(@"Pracovní\ndny", "", BezWrapu);

        Assert.Equal(enter.Select(l => l.Text), token.Select(l => l.Text));
    }

    /// <summary>Druhý nadpis z dat má vlastní styl, takže zůstává sekundární - to je dosavadní
    /// chování a nesmí se ztratit.</summary>
    [Fact]
    public void DruhyNadpisZDat_ZustavaSekundarni()
    {
        var lines = DepartureGridElement.ComposeHeaderLines("Pracovní", "den", BezWrapu);

        Assert.Equal(["Pracovní", "den"], lines.Select(l => l.Text));
        Assert.False(lines[0].IsSecondary);
        Assert.True(lines[1].IsSecondary);
    }

    [Fact]
    public void RucniZlom_SDruhymNadpisem_DaTriRadky()
    {
        var lines = DepartureGridElement.ComposeHeaderLines("Prac.\ndny", "mimo prázdniny", BezWrapu);

        Assert.Equal(["Prac.", "dny", "mimo prázdniny"], lines.Select(l => l.Text));
        Assert.Equal([false, false, true], lines.Select(l => l.IsSecondary));
    }

    /// <summary>Bez zlomu se pořád zalamuje podle šířky - dosavadní chování beze změny.</summary>
    [Fact]
    public void BezZlomu_ZalamujeDalPodleSirky()
    {
        var lines = DepartureGridElement.ComposeHeaderLines("Soboty neděle svátky", "",
            radek => radek.Split(' '));

        Assert.Equal(["Soboty", "neděle", "svátky"], lines.Select(l => l.Text));
        Assert.All(lines, l => Assert.False(l.IsSecondary));
    }

    // ── Minimální šířka skupiny ───────────────────────────────────────────────

    /// <summary>Past, kvůli které je to netriviální: minimální šířka skupiny se počítá z nejdelšího
    /// **slova**. Kdyby zlom slova nedělil, byl by dvouřádkový nadpis jedno dlouhé slovo a sloupec
    /// by se roztáhl na součet obou řádků.</summary>
    [Fact]
    public void RucniZlom_DeliSlovaStejneJakoMezera()
    {
        Assert.Equal(["Pracovní", "dny"], DepartureGridElement.HeaderWords("Pracovní\ndny"));
        Assert.Equal(["Pracovní", "dny"], DepartureGridElement.HeaderWords(@"Pracovní\ndny"));
    }

    [Fact]
    public void BezZlomu_SlovaJakoDrive()
    {
        Assert.Equal(["Neděle", "a", "svátky"], DepartureGridElement.HeaderWords("Neděle a svátky"));
        Assert.Equal(["Pracovní"], DepartureGridElement.HeaderWords("  Pracovní  "));
    }
}
