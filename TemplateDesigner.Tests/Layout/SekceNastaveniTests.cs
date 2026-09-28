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

/// <summary>`B177`: volba „Povolit rozdělení" zmizela - uložené šablony ji čtou jako „Rozdělit přes stránky".</summary>
public class PovolitRozdeleniTests
{
    [Fact]
    public void StaraHodnota_SeCteJakoRozdelitPresStranky()
    {
        var sekce = TemplateDesigner.Serialization.TemplateSerializer.DeserializeSection(
            """{ "name": "Poznámky", "overflowBehavior": "AllowSplit" }""")!;

        Assert.Equal(SectionOverflowBehavior.SplitAcrossPages, sekce.OverflowBehavior);
    }
}

/// <summary>`B177`: opakovaná sekce skrytá podmínkou si na té stránce nesmí vyhradit místo.</summary>
public class PodminkaOpakovaneSekceTests
{
    [Fact]
    public void SkrytaHlavicka_NenechaPrazdnyPruh()
    {
        var vysledek = new PageLayoutEngine(new SkiaHeightCalculator()).Layout(new Template
        {
            PageSize = new PageSize { Width = 210, Height = 100, MarginTop = 0, MarginBottom = 0 },
            Sections =
            [
                new Section { Name = "Hlavička", PrintBehavior = SectionPrintBehavior.OnEveryPage,
                              Role = SectionRole.Header, DesignerHeight = 20, VisibilityExpression = "{STRANA} > 1" },
                new Section { Name = "A", DesignerHeight = 90 },
                new Section { Name = "B", DesignerHeight = 50 },
            ],
        }, new DataContext(new Dictionary<string, object?>()));

        var strana1 = vysledek.Pages[0].Sections;
        Assert.DoesNotContain(strana1, s => s.Source.Name == "Hlavička");
        Assert.Equal(0f, strana1.Single(s => s.Source.Name == "A").Y);
        var strana2 = vysledek.Pages[1].Sections;
        Assert.Equal(20f, strana2.Single(s => s.Source.Name == "B").Y);
    }

    [Fact]
    public void KromePrvni_SePriNacteniPrevede()
    {
        var sekce = TemplateDesigner.Serialization.TemplateSerializer.DeserializeSection(
            """{ "name": "Hlavička", "printBehavior": "OnEveryPageExceptFirst", "visibilityExpression": "{A} == 1" }""")!;

        Assert.Equal(SectionPrintBehavior.OnEveryPage, sekce.PrintBehavior);
        Assert.Equal("{STRANA} > 1 && ({A} == 1)", sekce.VisibilityExpression);
    }
}

/// <summary>`B177`: formulář sekce ukazuje Typ + Stránky, do šablony jdou dál Tisk a Umístění.</summary>
public class TypSekceTests
{
    [Theory]
    [InlineData(SectionPrintBehavior.Once, SectionRole.Header, TypSekce.Obsah)]
    [InlineData(SectionPrintBehavior.OnEveryPage, SectionRole.Header, TypSekce.HlavickaStranky)]
    [InlineData(SectionPrintBehavior.OnLastPageOnly, SectionRole.BelowContent, TypSekce.PodObsahemStranky)]
    [InlineData(SectionPrintBehavior.OnFirstPageOnly, SectionRole.Footer, TypSekce.PatickaStranky)]
    public void Typ_SeCteZTiskuAUmisteni(SectionPrintBehavior tisk, SectionRole umisteni, TypSekce typ)
    {
        Assert.Equal(typ, new Section { PrintBehavior = tisk, Role = umisteni }.Typ);
    }

    [Fact]
    public void ZmenaTypuAStranek_ZapiseTiskAUmisteni()
    {
        var sekce = new Section { PrintBehavior = SectionPrintBehavior.Once };

        sekce.Typ = TypSekce.PatickaStranky;
        sekce.Stranky = StrankySekce.JenPosledni;
        Assert.Equal(SectionRole.Footer, sekce.Role);
        Assert.Equal(SectionPrintBehavior.OnLastPageOnly, sekce.PrintBehavior);

        sekce.Typ = TypSekce.PodObsahemStranky;   // pod obsahem vždy na všech stránkách
        Assert.Equal(SectionPrintBehavior.OnEveryPage, sekce.PrintBehavior);
        sekce.Stranky = StrankySekce.JenPosledni;
        Assert.Equal(SectionPrintBehavior.OnEveryPage, sekce.PrintBehavior);

        sekce.Typ = TypSekce.Obsah;
        Assert.Equal(SectionPrintBehavior.Once, sekce.PrintBehavior);
        sekce.Stranky = StrankySekce.JenPrvni;   // u obsahu se nepoužije
        Assert.Equal(SectionPrintBehavior.Once, sekce.PrintBehavior);
    }

    /// <summary>Opakovaná sekce „podle pořadí" se při otevření zařadí stejně jako v sazbě.</summary>
    [Fact]
    public void UmisteniPodlePoradi_SePriNacteniZapiseVyslovne()
    {
        var sekce = new List<Section>
        {
            new() { Name = "Hlavička", PrintBehavior = SectionPrintBehavior.OnEveryPage, Role = SectionRole.Body },
            new() { Name = "Obsah" },
            new() { Name = "Patička", PrintBehavior = SectionPrintBehavior.OnEveryPage, Role = SectionRole.Body },
        };

        TemplateDesigner.Serialization.TemplateSerializer.UrciUmisteniPodlePoradi(sekce);

        Assert.Equal(SectionRole.Header, sekce[0].Role);
        Assert.Equal(SectionRole.Body, sekce[1].Role);
        Assert.Equal(SectionRole.Footer, sekce[2].Role);
    }

    [Theory]
    [InlineData("OverflowBehavior", "Obsah", "0", true)]
    [InlineData("OverflowBehavior", "HlavickaStranky", "0", false)]
    [InlineData("Stranky", "Obsah", "0", false)]
    [InlineData("Stranky", "PatickaStranky", "0", true)]
    [InlineData("Stranky", "PodObsahemStranky", "0", false)]
    [InlineData("BorderWidth", "Obsah", "0", false)]
    [InlineData("BorderWidth", "Obsah", "1", true)]
    public void PoleBezUcinku_SeSchovaji(string klic, string typ, string ramecek, bool viditelne)
    {
        var hodnoty = new Dictionary<string, string> { ["Typ"] = typ, ["ShowBorder"] = ramecek };
        Assert.Equal(viditelne, TemplateDesigner.MainWindow.ViditelnostPoleSekce(klic, hodnoty));
    }
}

/// <summary>`B177`: „Začít na nové stránce" - poznámky na samostatném listu za tabulkou.</summary>
public class ZacitNaNoveStranceTests
{
    private static LayoutResult Vysad(params Section[] sekce) =>
        new PageLayoutEngine(new SkiaHeightCalculator()).Layout(new Template
        {
            PageSize = new PageSize { Width = 210, Height = 100, MarginTop = 0, MarginBottom = 0 },
            Sections = [.. sekce],
        }, new DataContext(new Dictionary<string, object?>()));

    private static Section Obsah(string nazev, float vyska, bool novaStrana = false, string? podminka = null) => new()
    {
        Name = nazev, DesignerHeight = vyska, ZacitNaNoveStrance = novaStrana, VisibilityExpression = podminka,
    };

    private static List<int> Stranky(LayoutResult r, string nazev) =>
        [.. r.Pages.Where(p => p.Sections.Any(s => s.Source.Name == nazev)).Select(p => p.PageNumber)];

    [Fact]
    public void Poznamky_ZacnouNaNovemListu_IKdyzByseVesly()
    {
        var r = Vysad(Obsah("Tabulka", 30), Obsah("Poznámky", 20, novaStrana: true));

        Assert.Equal([1], Stranky(r, "Tabulka"));
        Assert.Equal([2], Stranky(r, "Poznámky"));
        Assert.Equal(0f, r.Pages[1].Sections.Single(s => s.Source.Name == "Poznámky").Y);
    }

    [Fact]
    public void PrvniSekce_NevyrobiPrazdnyList()
    {
        var r = Vysad(Obsah("Poznámky", 20, novaStrana: true), Obsah("Další", 20));

        Assert.Single(r.Pages);
        Assert.Equal([1], Stranky(r, "Poznámky"));
    }

    [Fact]
    public void DveSekceZaSebou_KazdaNaSvemListu_BezPrazdneho()
    {
        var r = Vysad(Obsah("A", 20), Obsah("B", 20, novaStrana: true), Obsah("C", 20, novaStrana: true));

        Assert.Equal(3, r.Pages.Count);
        Assert.All(r.Pages, p => Assert.NotEmpty(p.Sections));
    }

    [Fact]
    public void SkrytaSekce_NovyListNezalozi()
    {
        var r = Vysad(Obsah("Tabulka", 30), Obsah("Poznámky", 20, novaStrana: true, podminka: "1 == 2"));

        Assert.Single(r.Pages);
    }

    /// <summary>Podmínka se stránkou počítá s novým listem, na kterém sekce opravdu začne.</summary>
    [Fact]
    public void PodminkaSeStrankou_ZnaNovyList()
    {
        var r = Vysad(Obsah("Tabulka", 30), Obsah("Poznámky", 20, novaStrana: true, podminka: "{STRANA} = 2"));

        Assert.Equal([2], Stranky(r, "Poznámky"));
    }

    [Fact]
    public void DlouhePoznamky_PokracujiNaDalsimListu()
    {
        var poznamky = Obsah("Poznámky", 150, novaStrana: true);
        poznamky.OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages;

        var r = Vysad(Obsah("Tabulka", 30), poznamky);

        Assert.Equal([2, 3], Stranky(r, "Poznámky"));
    }
}
