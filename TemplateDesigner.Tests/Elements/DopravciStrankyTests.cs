using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// <c>POPIS_DOPRAVCE</c> jen za spoje na stránce (#9417, KORID: u souhrnného JŘ text dopravců
/// přetékal pod tabulku, protože vyjmenoval spoje celé linky).
/// </summary>
public class DopravciStrankyTests
{
    private static LineTableElement.LineTripData Spoj(string cislo, params string[] dopravci) =>
        new() { CisloSpoje = cislo, Dopravci = [.. dopravci] };

    [Fact]
    public void JedenDopravce_JenNazev()
    {
        Assert.Equal("ČSAD Liberec",
            LineTableElement.PopisDopravcuStranky([Spoj("1", "ČSAD Liberec"), Spoj("3", "ČSAD Liberec")]));
    }

    /// <summary>Řady se slučují podle pořadí na stránce - lichá čísla jednoho směru jdou po sobě.</summary>
    [Fact]
    public void VicDopravcu_NazevASpojeVRadach()
    {
        var spoje = new[]
        {
            Spoj("1", "A"), Spoj("3", "A"), Spoj("5", "A"),
            Spoj("7", "B"),
            Spoj("9", "A"), Spoj("11", "B"), Spoj("13", "B"),
        };

        Assert.Equal("A (spoje 1-5, 9), B (spoje 7, 11-13)", LineTableElement.PopisDopravcuStranky(spoje));
    }

    /// <summary>
    /// Skok v číslech řadu přeruší, i když mezi spoji na stránce nic není - „15-105" by čtenář
    /// četl jako všechny spoje mezi nimi (`CB_GWBUS 402 / 0330530`, strana 1).
    /// </summary>
    [Fact]
    public void SkokVCislech_RaduPrerusi()
    {
        var spoje = new[]
        {
            Spoj("1", "GW"), Spoj("5", "CSAD"), Spoj("7", "GW"), Spoj("9", "CSAD"), Spoj("11", "CSAD"),
            Spoj("15", "GW"), Spoj("105", "GW"), Spoj("203", "CSAD"), Spoj("205", "CSAD"),
        };

        Assert.Equal("GW (spoje 1, 7, 15, 105), CSAD (spoje 5, 9-11, 203-205)",
            LineTableElement.PopisDopravcuStranky(spoje));
    }

    [Fact]
    public void SpojeBezDopravcu_TextZustaneCely()
    {
        Assert.Null(LineTableElement.PopisDopravcuStranky([Spoj("1"), Spoj("3")]));
        Assert.Null(LineTableElement.PopisDopravcuStranky([]));
    }

    /// <summary>
    /// List jen s přetečenými poznámkami nemá spoje - dostane jen názvy dopravců směru, bez čísel
    /// (nález Jan 23. 9., `CB_GWBUS 402 / 0330530`). Převzít čísla jedné stránky tabulky by bylo
    /// libovolné, jakmile má tabulka stránek víc.
    /// </summary>
    [Fact]
    public void StrankaBezSpoju_JenNazvyDopravcu()
    {
        var zastavky = Enumerable.Range(1, 5)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "zastavka"),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())))
            .ToArray();
        var spoje = Enumerable.Range(1, 6)
            .Select(i => RenderSnapshot.Row(
                ("CISLO_SPOJE", (i * 2 - 1).ToString()), ("SKUPINA", "1"),
                ("DOPRAVCI", new JsonArray(i <= 3 ? "A" : "B"))))
            .ToArray();
        var data = RenderSnapshot.Data(
                ("ZASTAVKY", zastavky), ("SPOJE", spoje),
                ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]))
            .WithExtraValues(new Dictionary<string, object?> { [PageLayoutEngine.PopisDopravceKlic] = "text za celou linku" });

        var tabulka = ElementRenderFixtures.LineTable();
        tabulka.X = 0; tabulka.Y = 0; tabulka.Height = null; tabulka.Width = 190;
        var sablona = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 160 },
            Sections =
            [
                new Section
                {
                    Name = "Záhlaví", PrintBehavior = SectionPrintBehavior.OnEveryPage,
                    Role = SectionRole.Header, DesignerHeight = 10,
                    Elements = [new TextElement { X = 0, Y = 0, Width = 190, Height = 5, Text = "{POPIS_DOPRAVCE}" }]
                },
                new Section
                {
                    Name = "Jízdní řád", PrintBehavior = SectionPrintBehavior.Once,
                    OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                    DesignerHeight = null, Elements = [tabulka]
                },
                new Section
                {
                    Name = "Poznámky", PrintBehavior = SectionPrintBehavior.Once,
                    OverflowBehavior = SectionOverflowBehavior.KeepTogether, DesignerHeight = 130
                }
            ]
        };

        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data);

        Assert.Equal(2, vysledek.Pages.Count);   // pojistka: poznámky opravdu odešly na další list
        Assert.DoesNotContain(vysledek.Pages[1].Sections, s => s.Source.Name == "Jízdní řád");
        var texty = vysledek.Pages
            .Select(p => p.Sections.Single(s => s.Source.Name == "Záhlaví").Data!
                          .GetValue(PageLayoutEngine.PopisDopravceKlic)?.ToString())
            .ToList();
        Assert.Equal("A (spoje 1-5), B (spoje 7-11)", texty[0]);
        Assert.Equal("A, B", texty[1]);
    }

    /// <summary>Spoj ve dvou skupinách dnů je v tabulce dvakrát - číslo se nesmí zopakovat.</summary>
    [Fact]
    public void TentyzSpojDvakrat_CisloJednou()
    {
        var spoje = new[] { Spoj("1", "A"), Spoj("2", "B"), Spoj("1", "A") };

        Assert.Equal("A (spoje 1), B (spoje 2)", LineTableElement.PopisDopravcuStranky(spoje));
    }
}
