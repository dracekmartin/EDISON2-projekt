using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// `B177`: nastavení sekce se nesmí tiše přepsat - co uživatel zadá a nápověda slibuje, musí sazba
/// udělat, nebo to musí jít poznat.
/// </summary>
public class SekceNastaveniTests
{
    private static Section Obsah(string nazev, float vyska, string? podminka = null) => new()
    {
        Name = nazev, PrintBehavior = SectionPrintBehavior.Once,
        OverflowBehavior = SectionOverflowBehavior.KeepTogether,
        DesignerHeight = vyska, VisibilityExpression = podminka,
    };

    private static LayoutResult Vysad(params Section[] sekce) =>
        new PageLayoutEngine(new SkiaHeightCalculator()).Layout(new Template
        {
            PageSize = new PageSize { Width = 210, Height = 100, MarginTop = 0, MarginBottom = 0 },
            Sections = [.. sekce],
        }, new DataContext(new Dictionary<string, object?>()));

    private static List<int> StrankySekce(LayoutResult vysledek, string nazev) =>
        [.. vysledek.Pages.Where(p => p.Sections.Any(s => s.Source.Name == nazev)).Select(p => p.PageNumber)];

    /// <summary>Příklad z nápovědy formuláře - do 24. 9. se taková sekce nevytiskla nikdy.</summary>
    [Fact]
    public void PodminkaSeStrankou_UObsahoveSekce_Plati()
    {
        var vysledek = Vysad(
            Obsah("Úvod", 60),
            Obsah("Jen na první", 20, "{STRANA} = 1"),
            Obsah("Jen na druhé", 5, "{STRANA} = 2"),
            Obsah("Tabulka", 60),
            Obsah("Od druhé", 20, "{STRANA} > 1"),
            Obsah("Až od třetí", 5, "{STRANA} >= 3"));

        Assert.Equal([1], StrankySekce(vysledek, "Jen na první"));
        Assert.Empty(StrankySekce(vysledek, "Jen na druhé"));   // padne na první → skrytá
        Assert.Equal([2], StrankySekce(vysledek, "Od druhé"));
        Assert.Empty(StrankySekce(vysledek, "Až od třetí"));
    }

    /// <summary>Sekce, která se nevejde a odejde na další list, se vyhodnotí s číslem toho listu.</summary>
    [Fact]
    public void PodminkaSeStrankou_BereStrankuKamSekceOdejde()
    {
        var vysledek = Vysad(Obsah("Úvod", 90), Obsah("Od druhé", 30, "{STRANA} > 1"));

        Assert.Equal([2], StrankySekce(vysledek, "Od druhé"));
    }

    /// <summary>Když se vejde, sedí hned pod obsahem poslední stránky - jako dřív.</summary>
    [Fact]
    public void PodObsahemJenPosledni_KdyzSeVejde_ZustanePodObsahem()
    {
        var vysledek = Vysad(
            Obsah("A", 80), Obsah("B", 40),
            new Section
            {
                Name = "Poznámky", PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
                Role = SectionRole.BelowContent, DesignerHeight = 30,
            });

        Assert.Equal([2], StrankySekce(vysledek, "Poznámky"));
        var strana2 = vysledek.Pages[1].Sections;
        Assert.Equal(strana2.Single(s => s.Source.Name == "B").Y + 40, strana2.Single(s => s.Source.Name == "Poznámky").Y, 2);
    }

    [Fact]
    public void PodminkaSPoctemStran_ZnaPoslediStranku()
    {
        var vysledek = Vysad(
            Obsah("A", 80), Obsah("B", 80),
            Obsah("Na poslední", 10, "{STRANA} = {POCET_STRAN}"),
            Obsah("Ne na poslední", 5, "{STRANA} != {POCET_STRAN}"));

        Assert.Equal([2], StrankySekce(vysledek, "Na poslední"));
        Assert.Empty(StrankySekce(vysledek, "Ne na poslední"));
    }

    /// <summary>
    /// „Pod obsahem" + „Jen poslední" se na poslední stránku nevejde - odejde na nový list, který
    /// se tím stane posledním. Do 24. 9. se přidala navíc a přetekla přes spodek stránky.
    /// </summary>
    [Fact]
    public void PodObsahemJenPosledni_NepreteceStranku()
    {
        var vysledek = Vysad(
            Obsah("Tabulka", 90),
            new Section
            {
                Name = "Poznámky", PrintBehavior = SectionPrintBehavior.OnLastPageOnly,
                Role = SectionRole.BelowContent, DesignerHeight = 30,
            });

        Assert.Equal([2], StrankySekce(vysledek, "Poznámky"));
        Assert.Equal(2, vysledek.Pages.Count);
        Assert.All(vysledek.Pages.SelectMany(p => p.Sections), s => Assert.True(s.Y + s.Height <= 100.01f,
            $"sekce {s.Source.Name} končí na {s.Y + s.Height} mm, stránka má 100 mm"));
    }
}
