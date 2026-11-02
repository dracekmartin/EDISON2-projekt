using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Panel, lišta i formulář ukazují u nenastavené vlastnosti stylu hodnotu, se kterou se prvek vytiskne.
/// Do 30. 10. si ji na 40 místech dosazovaly samy
/// (`?? "Arial"`, `?? 9f`): lišta u textu ukazovala Arial, i když šablona měla jiné výchozí písmo,
/// Minuty ukazovaly velikost minut ze skalárního zálohy, i když ji přebíjelo písmo datových buněk.
/// Jediný zdroj je <see cref="TemplateElement.ResolvedStyleZones"/>.
/// </summary>
public class VychoziHodnotyZKaskadyTests
{
    private static T Hodnota<T>(TemplateElement prvek, string skupina, string popisek)
    {
        var d = prvek.GetPropertyDescriptors().Single(d => d.Group == skupina && d.Label == popisek);
        return d switch
        {
            StringPropertyDescriptor s => (T)(object)s.Get(),
            FloatPropertyDescriptor f => (T)(object)f.Get(),
            BoolPropertyDescriptor b => (T)(object)b.Get(),
            ColorPropertyDescriptor c => (T)(object)c.Get(),
            EnumPropertyDescriptor e => (T)e.Get(),
            _ => throw new InvalidOperationException(d.GetType().Name),
        };
    }

    [Fact]
    public void Text_UkazeVychoziPismoSablony()
    {
        var puvodni = DocumentDefaults.Style;
        try
        {
            DocumentDefaults.Style = new TextStyle { FontFamily = "Verdana" };
            var text = new TextElement { Style = new TextStyle() };

            Assert.Equal("Verdana", Hodnota<string>(text, "Font", "Font"));
            Assert.Equal("Verdana", ElementFormBinder.InheritedValuesFor(text)!["Style.FontFamily"]);
        }
        finally { DocumentDefaults.Style = puvodni; }
    }

    [Fact]
    public void TabulkaSRadkyZDat_UkazeBarvyZahlaviZKaskady()
    {
        var tabulka = new DynamicTableElement();
        var zahlavi = tabulka.ResolveColumnHeaderParent();

        Assert.Equal(zahlavi.BackgroundColor, Hodnota<string>(tabulka, "Záhlaví", "Barva pozadí záhlaví"));
        Assert.Equal(zahlavi.Bold, Hodnota<bool>(tabulka, "Záhlaví", "Tučné záhlaví"));
        Assert.Equal(zahlavi.BackgroundColor, ElementFormBinder.InheritedValuesFor(tabulka)!["HeaderStyle.BackgroundColor"]);
    }

    [Fact]
    public void PevnaTabulka_UkazeBarvyZKaskady()
    {
        var tabulka = TableElement.CreateGrid(3, 3);

        Assert.Equal(tabulka.ResolveColumnHeaderParent().TextColor, Hodnota<string>(tabulka, "Záhlaví", "Barva textu záhlaví"));
        Assert.Equal(tabulka.ResolveColumnDataParent().TextColor, Hodnota<string>(tabulka, "Datové buňky", "Barva textu buněk"));
    }

    [Fact]
    public void SeznamZastavek_VelikostAktualniZastavkyDediZDatovychBunek()
    {
        var seznam = new StopListElement { DataStyle = new TextStyle { FontSize = 11f } };

        Assert.Equal(11f, Hodnota<float>(seznam, StopListElement.AktualniGroup, "Aktuální: Velikost"));
    }

    [Fact]
    public void MinutyOdjezdu_VelikostMinutyZPismaDatovychBunek()
    {
        // Skalární záloha MinuteFontSize platí, jen když velikost nenastavil DataStyle.
        var minuty = new DepartureGridElement { DataStyle = new TextStyle { FontSize = 12f } };

        Assert.Equal(12f, Hodnota<float>(minuty, "Datové buňky", "Minuta: Velikost"));
        Assert.Equal("12", ElementFormBinder.InheritedValuesFor(minuty)!["MinuteStyle.FontSize"]);
    }

    [Fact]
    public void Poznamky_PismoZahlaviBezNastaveniJakoPismoPoznamek()
    {
        // Tak to kreslí Poznámky: záhlaví bez vlastního písma vezme písmo poznámek.
        var poznamky = new NotesLegendElement { DataStyle = new TextStyle { FontFamily = "Verdana" } };

        Assert.Equal("Verdana", Hodnota<string>(poznamky, "Záhlaví", "Font"));
        Assert.Equal("Verdana", Hodnota<string>(poznamky, "Datové buňky", "Font"));
    }

    /// <summary>Každý prvek s textem má části stylu, ze kterých formulář vezme hodnoty; prvek bez textu žádné.</summary>
    [Fact]
    public void ZdedeneHodnotyFormulare_ProKazdyPrvekSTextem()
    {
        string[] bezTextu = [ElementTypes.Rectangle, ElementTypes.Line, ElementTypes.Image, ElementTypes.Barcode];
        foreach (var prvek in ElementCatalog.All)
        {
            var hodnoty = ElementFormBinder.InheritedValuesFor(ElementCatalog.Create(prvek.Type, 0, 0));
            if (bezTextu.Contains(prvek.Type)) Assert.Null(hodnoty);
            else Assert.True(hodnoty is { Count: > 0 }, $"{prvek.Type} nemá části stylu");
        }
    }
}
