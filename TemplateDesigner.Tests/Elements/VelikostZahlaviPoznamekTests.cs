using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// `B159` - velikost písma záhlaví u prvku Poznámky. Kreslení ji četlo odjakživa, ale nebylo kde ji
/// nastavit: skupina Záhlaví měla text, pozadí, barvu, font i řez, velikost ne. Hlásil TRANSDEV 16. 9.
/// </summary>
/// <remarks>
/// Datová zóna ji má zvlášť jako „Velikost" v Datových buňkách (mimo sadu stylu) - proto tady přibyl jen
/// popisovač záhlaví, ne obě zóny jako u ostatních tabulkových prvků.
/// </remarks>
public class VelikostZahlaviPoznamekTests
{
    private static PropertyDescriptor VelikostZahlavi(NotesLegendElement el) =>
        el.GetPropertyDescriptors().Single(d => d.Label == "Velikost" && d.Group == "Záhlaví");

    [Fact]
    public void SkupinaZahlavi_MaVelikost()
    {
        Assert.IsType<FloatPropertyDescriptor>(VelikostZahlavi(new NotesLegendElement()));
    }

    [Fact]
    public void BezNastaveni_UkazeVychozichDevetBodu()
    {
        var d = (FloatPropertyDescriptor)VelikostZahlavi(new NotesLegendElement());
        Assert.Equal(9f, d.Get());
    }

    [Fact]
    public void Nastaveni_ZapiseSeDoStyluZahlavi()
    {
        var el = new NotesLegendElement();
        ((FloatPropertyDescriptor)VelikostZahlavi(el)).Set(14f);
        Assert.Equal(14f, el.HeaderStyle.FontSize);
    }

    [Fact]
    public void VetsiPismo_ZvysiPruhZahlavi()
    {
        var stranka = new DataContext(
            new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>>
            {
                ["POZNAMKY"] = [new DataContext(new Dictionary<string, object?>
                {
                    ["TYP"] = "text", ["ZNAK"] = "1", ["TEXT"] = "poznámka",
                })],
            });

        var male = new NotesLegendElement();
        var velke = new NotesLegendElement();
        velke.HeaderStyle.FontSize = 20f;

        Assert.True(velke.CalculateHeightFromData(stranka) > male.CalculateHeightFromData(stranka),
            "výška prvku se počítá i z pruhu záhlaví, takže větší písmo ho má zvýšit");
    }

    [Fact]
    public void VelikostPoznamek_ZustavaSamostatna()
    {
        var el = new NotesLegendElement();
        ((FloatPropertyDescriptor)VelikostZahlavi(el)).Set(14f);
        Assert.Equal(7f, el.NoteFontSize);
    }
}
