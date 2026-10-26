using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Zápis vzhledu, který AI poznala z předlohy, do prvku. Otázka i zápis vznikají z vlastností
/// prvku, takže se import nemůže rozejít s tím, co prvek umí. Model ale odpovídá volným textem -
/// nesmyslná hodnota nesmí skončit v šabloně (rozbitá barva vypadá rozbitě i po každém otevření).
/// </summary>
public class ElementSettingsFromImageTests
{
    private static List<string> Apply(StopListElement el, params (string Key, string Value)[] settings) =>
        ElementSettingsFromImage.Apply(el, settings.ToDictionary(s => s.Key, s => s.Value));

    /// <summary>Popisek volby nese vysvětlivku („Vlevo (u levého okraje prvku)"), model odpoví
    /// holé „Vlevo" - musí projít obojí.</summary>
    [Theory]
    [InlineData("Vlevo (u levého okraje prvku)")]
    [InlineData("Vlevo")]
    public void VyberZNabidkySeZapisePlnymIZkracenymPopiskem(string answer)
    {
        var el = new StopListElement();

        Apply(el, ("Směr jízdy · Pozice šipky", answer));

        Assert.Equal(DirectionArrowSide.Left, el.DirectionArrowPosition);
    }

    [Fact]
    public void ZnackyPredNazvemSeDajiNastavit()
    {
        var el = new StopListElement();

        Apply(el, ("Zastávky · Pozice značek", "Před zastávkou"));

        Assert.Equal(StopMarkersSide.BeforeName, el.MarkersPosition);
    }

    [Theory]
    [InlineData("ano", true)]
    [InlineData("ne", false)]
    [InlineData("false", false)]
    public void AnoNeSeZapiseJakoPrepinac(string value, bool expected)
    {
        var el = new StopListElement();

        Apply(el, ("Záhlaví · Záhlaví sloupců", value));

        Assert.Equal(expected, el.ShowHeader);
    }

    [Fact]
    public void CisloSDesetinnouCarkouProjde()
    {
        var el = new StopListElement();

        Apply(el, ("Zastávky · Mezera mezi oddíly", "4,5"));

        Assert.Equal(4.5f, el.PartGap, 3);
    }

    [Fact]
    public void NesmyslnaBarvaSeDoSablonyNedostane()
    {
        var el = new StopListElement { DirectionArrowColor = "#FF000000" };

        var log = Apply(el, ("Směr jízdy · Barva šipky", "tmavě modrá"));

        Assert.Equal("#FF000000", el.DirectionArrowColor);
        Assert.Contains(log, l => l.Contains("nerozumím"));
    }

    [Fact]
    public void HodnotaMimoRozsahSeIgnoruje()
    {
        var el = new StopListElement();
        int original = el.PartCount;

        var log = Apply(el, ("Zastávky · Rozdělit na více oddílů", "999"));

        Assert.Equal(original, el.PartCount);
        Assert.Contains(log, l => l.Contains("nerozumím"));
    }

    [Fact]
    public void VymyslenaVlastnostSePreskoci()
    {
        var el = new StopListElement();

        var log = Apply(el, ("Zastávky · Kulaté rohy", "ano"));

        Assert.Contains(log, l => l.Contains("takovou vlastnost prvek nemá"));
    }

    /// <summary>Pozice a velikost se měří z PDF přesně - na ty se model ptát nemá.</summary>
    [Fact]
    public void PoziceAVelikostNejsouVNabidce()
    {
        var catalog = ElementSettingsFromImage.BuildCatalog(new StopListElement());

        Assert.DoesNotContain("Pozice a velikost", catalog);
        Assert.Contains("Pozice šipky", catalog);
    }

    [Fact]
    public void NabidkaNesePovoleneHodnoty()
    {
        var catalog = ElementSettingsFromImage.BuildCatalog(new StopListElement());

        Assert.Contains("Vlevo", catalog);
        Assert.Contains("\"ano\" nebo \"ne\"", catalog);
    }

    /// <summary>Ptáme se jen na to, co jde z předlohy POTVRDIT. Nepřítomnost nic nedokazuje:
    /// chybějící značky přestupů můžou znamenat vypnutou volbu i linku bez přestupů.</summary>
    [Theory]
    [InlineData("Zastávky · Přestupy (vlak/autobus)")]
    [InlineData("Zastávky · Poznámky zastávky")]
    [InlineData("Zastávky · Více stanovišť")]
    [InlineData("Aktuální a projeté zastávky · Projeté: Čas")]
    public void NaVeciNeodvoditelneZObrazkuSeNeptame(string klic)
    {
        var catalog = ElementSettingsFromImage.BuildCatalog(new StopListElement());

        Assert.DoesNotContain(klic, catalog);
    }

    /// <summary>Rozměry a písma se z PDF měří přesně - hádat je od modelu by výsledek zhoršilo.</summary>
    [Fact]
    public void NaRozmeryAPismaSeNeptame()
    {
        var catalog = ElementSettingsFromImage.BuildCatalog(new DepartureGridElement());

        Assert.DoesNotContain("Velikost záhlaví", catalog);
        Assert.DoesNotContain("Min. výška řádku", catalog);
    }

    [Theory]
    [InlineData("Pozice šipky")]
    [InlineData("Rozdělit na více oddílů")]
    public void NaJednoznacneVizualniVeciSeNaopakPtameStale(string popisek)
    {
        var catalog = ElementSettingsFromImage.BuildCatalog(new StopListElement());

        Assert.Contains(popisek, catalog);
    }

    [Fact]
    public void MrizkaOdjezduILegendaMajiSvojiNabidku()
    {
        Assert.Contains("Střídání řádků", ElementSettingsFromImage.BuildCatalog(new DepartureGridElement()));
        Assert.Contains("Počet sloupců", ElementSettingsFromImage.BuildCatalog(new NotesLegendElement()));
    }

    [Fact]
    public void ProtokolVypiseCoSeNastavilo()
    {
        var el = new StopListElement();

        var log = Apply(el, ("Směr jízdy · Pozice šipky", "Vlevo"));

        Assert.Contains(log, l => l.Contains("Pozice šipky") && l.Contains("Vlevo"));
    }
}
