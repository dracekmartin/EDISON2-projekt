using TemplateDesigner.Models.Elements;
using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Soupis toho, co import na prvku nastavil (oproti čerstvě vloženému). Je to jediná cesta, jak
/// po importu zkontrolovat výsledek proti předloze a jak porovnat dva importy mezi sebou -
/// hláška po importu se dá jen přečíst a zavřít.
/// </summary>
public class ImportProtocolTests
{
    [Fact]
    public void CerstvyPrvekNemaZadneOdchylky()
    {
        var diff = ElementSettingsFromImage.DiffFromDefault(new StopListElement());

        Assert.Empty(diff);
    }

    [Fact]
    public void ZmenaVlastnostiSeVProtokoluObjeviIsPuvodniHodnotou()
    {
        var el = new StopListElement { DirectionArrowPosition = DirectionArrowSide.Left };

        var diff = ElementSettingsFromImage.DiffFromDefault(el);

        Assert.Contains(diff, d => d.Contains("Pozice šipky") && d.Contains("Vlevo")
                                   && d.Contains("výchozí") && d.Contains("Vpravo"));
    }

    /// <summary>Do protokolu patří i to, co nenastavil model, ale změření z PDF (písma, výšky řádků).</summary>
    [Fact]
    public void ProtokolNeseIZmereneHodnoty()
    {
        var el = new DepartureGridElement { MinuteFontSize = 6.5f, DataRowHeight = 4.2f };

        var diff = ElementSettingsFromImage.DiffFromDefault(el);

        Assert.Contains(diff, d => d.Contains("6,5") || d.Contains("6.5"));
    }

    [Fact]
    public void PoziceAVelikostSeDoOdchylekNepocitaji()
    {
        var el = new StopListElement { X = 50f, Y = 60f, Width = 80f };

        var diff = ElementSettingsFromImage.DiffFromDefault(el);

        Assert.DoesNotContain(diff, d => d.StartsWith("X =") || d.StartsWith("Y ="));
    }
}
