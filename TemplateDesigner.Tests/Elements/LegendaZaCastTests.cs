using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// `B127`: legenda poznámek tištěná na každé stránce má nést jen to, co na tu stránku patří.
/// Když se tabulka linky rozdělí do šířky na části, mají části různé spoje - a tedy různé značky.
/// </summary>
/// <remarks>
/// Doloženo starým tiskem `CB_GWBUS 251 / 0340100`: strana 1 měla v legendě jen „jede v pracovních
/// dnech", strana 2 navíc sobotu a neděli, přitom obě nesly tytéž zastávky.
/// <para>Bez nastavení: řídí to, jestli si uživatel nechal sekci poznámek opakovat (rozhodnutí
/// Jan 14. 9.). Když se legenda tiskne jednou za celou sestavu, klíč se do dat nedostane
/// a nefiltruje se nic.</para>
/// </remarks>
public class LegendaZaCastTests
{
    /// <summary>Linka, kde první polovina spojů jezdí v pracovní dny a druhá o víkendu.</summary>
    private static DataContext Linka(int pocetSpoju, int pocetZastavek)
    {
        var zastavky = Enumerable.Range(1, pocetZastavek)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "zastavka"),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())))
            .ToArray();

        var spoje = Enumerable.Range(1, pocetSpoju)
            .Select(i => RenderSnapshot.Row(
                ("CISLO_SPOJE", (i * 2).ToString()),
                ("SKUPINA", "1"),
                ("PLATNOST", new JsonArray(i <= pocetSpoju / 2 ? "X" : "6"))))
            .ToArray();

        return RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", spoje),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));
    }

    private static LineTableElement Tabulka()
    {
        var p = ElementRenderFixtures.LineTable();
        p.X = 0; p.Y = 0; p.Height = null; p.Width = 100;   // úzká → rozdělí se na části
        return p;
    }

    private static List<PoznamkaData> Poznamky() =>
    [
        new() { Typ = TypPoznamky.Text, Znak = "X", Text = "jede v pracovních dnech" },
        new() { Typ = TypPoznamky.Text, Znak = "6", Text = "jede v sobotu" },
        new() { Typ = TypPoznamky.Text, Znak = "",  Text = "Na lince platí tarifní podmínky" },
    ];

    /// <summary>Části mají různé spoje, takže i různé značky - to je celý základ featury.</summary>
    [Fact]
    public void CastiMajiRuzneZnacky()
    {
        var prvek = Tabulka();
        var data = Linka(40, pocetZastavek: 12);
        var zlomy = ((INedelitelneDily)prvek).ZlomyMm(data);
        var hraniceBloku = zlomy.Where(z => z.Uroven == ZlomUroven.Blok).Select(z => z.Mm).ToList();

        Assert.NotEmpty(hraniceBloku);   // pojistka: linka se opravdu dělí

        var prvniCast = ((INedelitelneDily)prvek).ZnackyVUseku(data, 0f, hraniceBloku[0]);
        var posledniCast = ((INedelitelneDily)prvek).ZnackyVUseku(data, hraniceBloku[^1], float.MaxValue);

        Assert.Contains("X", prvniCast);
        Assert.DoesNotContain("6", prvniCast);
        Assert.Contains("6", posledniCast);
        Assert.DoesNotContain("X", posledniCast);
    }

    /// <summary>Legenda vyhodí poznámku, jejíž značka na stránce není. Poznámka bez značky zůstává.</summary>
    [Fact]
    public void LegendaNechaJenZnackyStranky()
    {
        var legenda = new NotesLegendElement { X = 0, Y = 0, Width = 100 };
        var data = RenderSnapshot.Data().WithExtraValues(
            new Dictionary<string, object?> { [PageLayoutEngine.ZnackyStrankyKlic] = new HashSet<string> { "X" } });

        var vysledek = legenda.FiltrujNaZnackyStrankyProTest(Poznamky(), data);

        Assert.Contains(vysledek, p => p.Znak == "X");
        Assert.DoesNotContain(vysledek, p => p.Znak == "6");
        Assert.Contains(vysledek, p => p.Znak == "");   // obecný text patří celé lince
    }

    /// <summary>
    /// Bez klíče v datech se nefiltruje nic - legenda tištěná jednou za sestavu musí zůstat úplná.
    /// </summary>
    [Fact]
    public void BezKliceSeNefiltruje()
    {
        var legenda = new NotesLegendElement { X = 0, Y = 0, Width = 100 };

        var vysledek = legenda.FiltrujNaZnackyStrankyProTest(Poznamky(), RenderSnapshot.Data());

        Assert.Equal(3, vysledek.Count);
    }

    /// <summary>
    /// Celý řetěz: engine doplní značky stránky do dat opakované sekce pod obsahem. Bez toho by
    /// legenda neměla podle čeho filtrovat, i kdyby filtr uměla.
    /// </summary>
    [Fact]
    public void EngineDoplniZnackyDoOpakovaneSekce()
    {
        var tabulka = Tabulka();
        var data = Linka(40, pocetZastavek: 12);
        var sablona = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 160 },
            Sections =
            [
                new Section
                {
                    Name = "Jízdní řád", PrintBehavior = SectionPrintBehavior.Once,
                    OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                    DesignerHeight = 10, Elements = [tabulka]
                },
                new Section
                {
                    Name = "Poznámky", PrintBehavior = SectionPrintBehavior.OnEveryPage,
                    Role = SectionRole.BelowContent,
                    DesignerHeight = 10,
                    Elements = [new NotesLegendElement { X = 0, Y = 0, Width = 100 }]
                }
            ]
        };

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        var poznamkove = vysledek.Pages
            .SelectMany(p => p.Sections)
            .Where(s => s.Source.Name == "Poznámky")
            .ToList();

        Assert.NotEmpty(poznamkove);
        Assert.All(poznamkove, s =>
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                s.Data!.GetValue(PageLayoutEngine.ZnackyStrankyKlic)));
    }
}
