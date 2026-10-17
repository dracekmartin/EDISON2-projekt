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

    // ─── Rozdílový JŘ: dopravci podle verzí (`B192f`) ──────────────────────────

    private static LineTableElement.LineTripData SpojVerze(string cislo, string verze, string[] dopravci,
        string[]? puvodni = null) =>
        new() { CisloSpoje = cislo, Verze = verze, Dopravci = [.. dopravci], DopravciPuvodni = puvodni?.ToList() };

    [Fact]
    public void PodleVerzi_BezPorovnani_VsichniVObouVerzich()
    {
        Assert.Equal(("A", "", ""),
            LineTableElement.DopravciPodleVerzi([Spoj("1", "A"), Spoj("3", "A")]));
    }

    /// <summary>Na skutečných datech dopravce v jedné verzi chybí úplně (CB_CSADAD 0320167: GW BUS
    /// jen v původní, 0132400: Jan Kukla jen v nové).</summary>
    [Fact]
    public void PodleVerzi_DopravceJenVJedneVerzi()
    {
        Assert.Equal(("", "GW", ""),
            LineTableElement.DopravciPodleVerzi([SpojVerze("1", "", [], puvodni: ["GW"])]));
        Assert.Equal(("", "", "Kukla"),
            LineTableElement.DopravciPodleVerzi([SpojVerze("1", "", ["Kukla"], puvodni: [])]));
    }

    /// <summary>
    /// Vzor z CIS JŘ (ARRIVA 950904): dopravce, kterému ubyl zrušený spoj, je dvakrát - původní
    /// řádek se všemi spoji a nový bez zrušeného. Ostatní dopravci jsou v obou verzích jednou.
    /// </summary>
    [Fact]
    public void PodleVerzi_DopravceSJinymiSpoji_JeVKazdeVerzi()
    {
        var spoje = new[]
        {
            SpojVerze("1", "", ["Jeseník"]),
            SpojVerze("13", "puvodni", ["Olomouc"]), SpojVerze("13", "nova", ["Olomouc"]),
            SpojVerze("23", "", ["Přerov"]),
            SpojVerze("527", "puvodni", ["Olomouc"]),
            SpojVerze("627", "", ["Olomouc"]),
        };

        Assert.Equal(("Jeseník (spoje 1)\nPřerov (spoje 23)", "Olomouc (spoje 13, 527, 627)", "Olomouc (spoje 13, 627)"),
            LineTableElement.DopravciPodleVerzi(spoje));
    }

    [Fact]
    public void PodleVerzi_SpojeBezDopravcu_Nic()
    {
        Assert.Null(LineTableElement.DopravciPodleVerzi([Spoj("1"), Spoj("3")]));
    }

    /// <summary>
    /// Sekce s dopravci podle verzí má výšku podle obsahu a ta se počítá před stránkováním, kdy
    /// text za stránku ještě neexistuje. Bez textu za celá data by měla nulovou výšku a dopravce
    /// by se na stránce vytiskl přes tabulku.
    /// </summary>
    [Fact]
    public void SekceDopravcuPodleVerziMaVyskuPodleRadku()
    {
        var jeden = UmistiDopravce("GW BUS");
        var dva = UmistiDopravce("GW BUS", "ČSAD");

        Assert.Equal("GW BUS (spoje 1)\nČSAD (spoje 3)", dva.Dopravci.Data!.GetValue(PageLayoutEngine.DopravciPuvodniKlic)?.ToString());
        Assert.True(dva.Dopravci.Height > jeden.Dopravci.Height * 1.5f,
            $"dva řádky dopravců mají výšku {dva.Dopravci.Height} mm, jeden {jeden.Dopravci.Height} mm");
        Assert.True(dva.Tabulka.Y >= dva.Dopravci.Y + dva.Dopravci.Height - 0.01f, "tabulka začíná pod dopravci");
    }

    /// <summary>Sazba linky, jejíž spoje mají každý jiného dopravce jen v původní verzi.</summary>
    private static (PlacedSection Dopravci, PlacedSection Tabulka) UmistiDopravce(params string[] dopravci)
    {
        var zastavky = Enumerable.Range(1, 3)
            .Select(i => RenderSnapshot.Row(("ID", i.ToString()), ("TYP", "zastavka"),
                                            ("NAZEV_ZASTAVKY", $"Zastávka {i}"), ("PORADI", i.ToString())))
            .ToArray();
        var data = RenderSnapshot.Data(
            ("ZASTAVKY", zastavky),
            ("SPOJE", [.. dopravci.Select((d, i) => RenderSnapshot.Row(("CISLO_SPOJE", (i * 2 + 1).ToString()),
                ("SKUPINA", "1"), ("DOPRAVCI", new JsonArray()), ("DOPRAVCI_PUVODNI", new JsonArray(d))))]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

        var tabulka = ElementRenderFixtures.LineTable();
        tabulka.X = 0; tabulka.Y = 0; tabulka.Height = null; tabulka.Width = 190;
        var sablona = new Template
        {
            PageSize = new PageSize { Width = 210, Height = 160 },
            Sections =
            [
                new Section
                {
                    Name = "Dopravci původní verze", PrintBehavior = SectionPrintBehavior.OnEveryPage,
                    DesignerHeight = null,
                    Elements = [new TextElement { X = 0, Y = 0, Width = 190, Height = null, WordWrap = true,
                        Text = "{" + PageLayoutEngine.DopravciPuvodniKlic + "}" }]
                },
                new Section
                {
                    Name = "Jízdní řád", PrintBehavior = SectionPrintBehavior.Once,
                    OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
                    DesignerHeight = null, Elements = [tabulka]
                },
            ]
        };

        var stranka = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(sablona, data).Pages[0];
        return (stranka.Sections.Single(s => s.Source.Name == "Dopravci původní verze"),
                stranka.Sections.Single(s => s.Source.Name == "Jízdní řád"));
    }

    /// <summary>Spoj ve dvou skupinách dnů je v tabulce dvakrát - číslo se nesmí zopakovat.</summary>
    [Fact]
    public void TentyzSpojDvakrat_CisloJednou()
    {
        var spoje = new[] { Spoj("1", "A"), Spoj("2", "B"), Spoj("1", "A") };

        Assert.Equal("A (spoje 1), B (spoje 2)", LineTableElement.PopisDopravcuStranky(spoje));
    }
}
