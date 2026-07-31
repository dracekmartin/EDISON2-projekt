using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Rozdělení seznamu zastávek na oddíly na pokračování (KOVED #13097): seznam se rozřeže na N
/// oddílů vedle sebe, každý nese celou tabulku znovu (záhlaví i všechny sloupce), jen s další
/// porcí zastávek. Smysl featury je nižší prvek, proto se hlídá hlavně výpočet výšky - ten
/// řídí i dynamickou výšku sekce v enginu.
/// </summary>
public class StopListPartsTests
{
    [Theory]
    [InlineData(3, 1, 3)]    // jeden oddíl = dosavadní stav
    [InlineData(14, 2, 7)]   // beze zbytku
    [InlineData(15, 2, 8)]   // se zbytkem - první oddíl je delší, poslední kratší
    [InlineData(2, 3, 1)]    // víc oddílů než řádků (max je 3)
    [InlineData(0, 2, 0)]    // bez dat
    public void RozdeleniRadkuNaOddily(int celkem, int oddilu, int cekano)
    {
        Assert.Equal(cekano, StopListElement.RowsPerPart(celkem, oddilu));
    }

    [Fact]
    public void NeplatnyPocetOddiluSeChovaJakoJeden()
    {
        Assert.Equal(5, StopListElement.RowsPerPart(5, 0));
        Assert.Equal(5, StopListElement.RowsPerPart(5, -3));
    }

    /// <summary>Dva oddíly = polovina řádků na sobě, takže prvek je nižší. Bez toho by featura
    /// nic nepřinesla - to je celý její smysl.</summary>
    [Fact]
    public void DvaOddilySnizujiVysku()
    {
        var data = ElementRenderFixtures.StopScheduleData();   // 3 zastávky, aktuální je první
        var jeden = new StopListElement { ShowPassedStops = true, Width = 80f };
        var dva = new StopListElement { ShowPassedStops = true, Width = 80f, PartCount = 2 };

        float vyskaJeden = jeden.CalculateHeightFromData(data);
        float vyskaDva = dva.CalculateHeightFromData(data);

        // 3 zastávky → oddíly po 2 a 1 řádku, takže ubude právě jeden řádek (záhlaví zůstává).
        Assert.True(vyskaDva < vyskaJeden, $"dva oddíly mají být nižší ({vyskaDva} vs {vyskaJeden})");
    }

    [Fact]
    public void JedenOddilJeVychoziANemeniVysku()
    {
        var data = ElementRenderFixtures.StopScheduleData();
        var vychozi = new StopListElement { ShowPassedStops = true, Width = 80f };

        Assert.Equal(1, vychozi.PartCount);
        Assert.Equal(vychozi.CalculateHeightFromData(data),
            new StopListElement { ShowPassedStops = true, Width = 80f, PartCount = 1 }.CalculateHeightFromData(data));
    }
}
