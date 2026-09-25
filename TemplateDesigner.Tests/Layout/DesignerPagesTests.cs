using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Stránky dokladu v návrháři (`B184`): nová stránka začíná sekcí se „Začít na nové stránce",
/// hlavička a patička se opakují, list má pevnou výšku stránky.
/// </summary>
public class DesignerPagesTests
{
    private static readonly PageSize A5 = new()
    {
        Width = 148, Height = 210, MarginTop = 5, MarginBottom = 5, MarginLeft = 5, MarginRight = 5,
    };

    private static Section Obsah(string nazev, float vyska, bool novaStranka = false) => new()
    {
        Name = nazev, DesignerHeight = vyska, ZacitNaNoveStrance = novaStranka,
        PrintBehavior = SectionPrintBehavior.Once,
    };

    private static Section Hlavicka(float vyska, SectionPrintBehavior tisk = SectionPrintBehavior.OnEveryPage) => new()
    {
        Name = "Hlavička", DesignerHeight = vyska, PrintBehavior = tisk, Role = SectionRole.Header,
    };

    private static Section Paticka(float vyska) => new()
    {
        Name = "Patička", DesignerHeight = vyska, PrintBehavior = SectionPrintBehavior.OnEveryPage, Role = SectionRole.Footer,
    };

    private static Template Sablona(params Section[] sekce) => new() { PageSize = A5, Sections = [.. sekce] };

    [Fact]
    public void BezNoveStranky_JedenList()
    {
        var listy = DesignerPages.Rozloz(Sablona(Obsah("A", 50), Obsah("B", 50)));

        var list = Assert.Single(listy);
        Assert.Equal(210f, list.VyskaMm);
        Assert.Equal(0f, list.PresahMm(A5));
    }

    [Fact]
    public void NovaStranka_DruhyListSeJmenem()
    {
        var listy = DesignerPages.Rozloz(Sablona(Obsah("Líc", 190), Obsah("Rub", 190, novaStranka: true)));

        Assert.Equal(2, listy.Count);
        Assert.Equal("Rub", listy[1].Nazev);
        Assert.Equal(5f, Assert.Single(listy[1].Sekce).YMm);   // rub začíná nahoře svého listu
        Assert.All(listy, l => Assert.Equal(210f, l.VyskaMm));
    }

    /// <summary>⚠️ Jádro rozhodnutí Jan 25. 9.: papír se nenatahuje, obsah navíc je přesah.</summary>
    [Fact]
    public void ObsahVyssiNezStranka_PresahMistoNatazeni()
    {
        var list = Assert.Single(DesignerPages.Rozloz(Sablona(Obsah("A", 150), Obsah("B", 100))));

        Assert.Equal(50f, list.PresahMm(A5), precision: 2);
    }

    /// <summary>Hlavička je upravitelná na prvním listu, na dalších drží místo jako kopie.</summary>
    [Fact]
    public void Hlavicka_NaDalsimListuKopieKteraDrziMisto()
    {
        var hlavicka = Hlavicka(20);
        var rub = Obsah("Rub", 50, novaStranka: true);
        var listy = DesignerPages.Rozloz(Sablona(hlavicka, Obsah("Líc", 50), rub));

        Assert.False(listy[0].Sekce.Single(s => s.Section == hlavicka).Kopie);
        Assert.True(listy[1].Sekce.Single(s => s.Section == hlavicka).Kopie);
        Assert.Equal(25f, listy[1].Sekce.Single(s => s.Section == rub).YMm);   // pod kopií hlavičky
    }

    [Fact]
    public void HlavickaJenPrvni_NaDruhemListuNeni()
    {
        var hlavicka = Hlavicka(20, SectionPrintBehavior.OnFirstPageOnly);
        var listy = DesignerPages.Rozloz(Sablona(hlavicka, Obsah("Líc", 50), Obsah("Rub", 50, novaStranka: true)));

        Assert.DoesNotContain(listy[1].Sekce, s => s.Section == hlavicka);
    }

    [Fact]
    public void Paticka_UDolniHranyKazdehoListu()
    {
        var paticka = Paticka(15);
        var listy = DesignerPages.Rozloz(Sablona(Obsah("Líc", 50), Obsah("Rub", 50, novaStranka: true), paticka));

        Assert.All(listy, l => Assert.Equal(190f, l.Sekce.Single(s => s.Section == paticka).YMm));
    }

    /// <summary>
    /// ⚠️ Místo pro sekci se počítá po stránkách: rub nesmí brát místo líci. Dřív se sčítala
    /// celá šablona, jako by byla jedna stránka.
    /// </summary>
    [Fact]
    public void ZbyvajiciMisto_JenZeSveStranky()
    {
        var rub = Obsah("Rub", 50, novaStranka: true);
        var sablona = Sablona(Obsah("Líc", 190), rub);

        Assert.Equal(200f, SectionLayoutMath.ZbyvajiciMistoMm(sablona, rub), precision: 2);
    }

    /// <summary>Nová obsahová sekce jde na konec aktivní stránky, ne na konec šablony.</summary>
    [Fact]
    public void NovaSekce_NaKonecAktivniStranky()
    {
        var lic = Obsah("Líc", 100);
        var rub = Obsah("Rub", 100, novaStranka: true);
        var sablona = Sablona(lic, rub, Paticka(10));

        Assert.Equal(1, DesignerPages.PoziceNaKonciStranky(sablona, 0));   // hned za Líc, před Rub
        Assert.Equal(2, DesignerPages.PoziceNaKonciStranky(sablona, 1));
    }

    [Fact]
    public void IndexStranky_HlavickaPatriPrvniStranceKdeSeTiskne()
    {
        var hlavicka = Hlavicka(20, SectionPrintBehavior.OnLastPageOnly);
        var rub = Obsah("Rub", 50, novaStranka: true);
        var sablona = Sablona(hlavicka, Obsah("Líc", 50), rub);

        Assert.Equal(1, DesignerPages.IndexStranky(sablona, rub));
        Assert.Equal(1, DesignerPages.IndexStranky(sablona, hlavicka));   // jen poslední = druhá stránka
    }

    [Fact]
    public void NovaSekce_NezkratiPredchoziStranku()
    {
        var lic = Obsah("Líc", 190);
        var sablona = Sablona(lic, Obsah("Rub", 100, novaStranka: true));

        var misto = SectionLayoutMath.RozdelMistoProNovouSekci(sablona, 50);

        Assert.DoesNotContain(misto.Zkraceni, z => z.Sekce == lic);
        Assert.Equal(50f, misto.VyskaNoveSekce, precision: 2);
    }
}
