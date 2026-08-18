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

    /// <summary>Popisek volby nese vysvětlivku („Vlevo (u levého okraje elementu)"), model odpoví
    /// holé „Vlevo" - musí projít obojí.</summary>
    [Theory]
    [InlineData("Vlevo (u levého okraje elementu)")]
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
    [InlineData("true", true)]
    public void AnoNeSeZapiseJakoPrepinac(string value, bool expected)
    {
        var el = new StopListElement();

        Apply(el, ("Zastávky · Přestupy (vlak/autobus)", value));

        Assert.Equal(expected, el.ShowTransferAttributes);
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

    [Fact]
    public void ProtokolVypiseCoSeNastavilo()
    {
        var el = new StopListElement();

        var log = Apply(el, ("Směr jízdy · Pozice šipky", "Vlevo"));

        Assert.Contains(log, l => l.Contains("Pozice šipky") && l.Contains("Vlevo"));
    }
}
