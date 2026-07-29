using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Barva se zadává jako volný text, takže do modelu může přijít cokoli. Dřív se čtla přes
/// <c>SKColor.Parse</c>, která na neplatné hodnotě vyhodí výjimku - padalo to až při kreslení
/// (tedy daleko od zadání) a protože se hodnota uložila, padalo to i při každém dalším otevření
/// šablony. Testy hlídají, že čtení barvy je fail-soft a že prázdno/nesmysl nikdy nevyhodí.
/// </summary>
public class ColorHexTests
{
    [Theory]
    [InlineData("#FF3366")]
    [InlineData("#80FF3366")]
    [InlineData("  #FF3366  ")]   // ořez okrajů
    [InlineData("FF3366")]        // bez mřížky
    [InlineData("#FF33")]         // zkrácený zápis #ARGB - čtení ho bere (vstupní pole ne, viz níže)
    public void PlatnaBarva_SePrecte(string hex)
    {
        Assert.True(ColorHex.IsValid(hex));
        Assert.True(ColorHex.TryParse(hex, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("#ZZZZZZ")]
    [InlineData("#12345")]
    [InlineData("modrá")]
    public void NeplatnaBarva_NevyhodiAJeRozpoznana(string? hex)
    {
        Assert.False(ColorHex.IsValid(hex));
        Assert.False(ColorHex.TryParse(hex, out _));
        Assert.Equal(SKColors.Black, ColorHex.ParseOrBlack(hex));
        Assert.Equal(SKColors.Transparent, ColorHex.ParseOrTransparent(hex));
        Assert.Null(ColorHex.ParseOrNull(hex));
    }

    [Fact]
    public void ZalozniBarvaSeBerePouzeKdyzHodnotaNeplati()
    {
        Assert.Equal(SKColor.Parse("#FF3366"), ColorHex.Parse("#FF3366", SKColors.White));
        Assert.Equal(SKColors.White, ColorHex.Parse("nesmysl", SKColors.White));
        Assert.Equal(SKColor.Parse("#FFE0E0E0"), ColorHex.Parse(null, "#FFE0E0E0"));
    }

    // ── Kreslení s rozbitou barvou v modelu (zákazníkova šablona) ────────────────
    // Prázdná barva se do šablon reálně uložila, takže je nutné umět takovou šablonu
    // otevřít a vykreslit - jinak vypadá jako nenávratně rozbitá.

    [Fact]
    public void ElementSPrazdnouBarvou_NevyhodiPriCteniBarev()
    {
        var text = new TextElement { BorderColor = "", BackgroundColor = "#ZZZ", Style = new TextStyle { TextColor = "" } };

        Assert.Equal(SKColors.Black, text.SkBorderColor);              // rámeček: záloha černá
        Assert.Equal(SKColors.Transparent, text.SkBackgroundColor);    // pozadí: záloha průhledná

        var resolved = text.Style.Resolve();
        Assert.Equal(SKColors.Black, resolved.ParsedTextColor);
        using var paint = resolved.CreateTextPaint();
        Assert.Equal(SKColors.Black, paint.Color);
    }

    [Fact]
    public void PrazdnaBarvaPozadi_NenakresliPozadi()
    {
        var resolved = new TextStyle { BackgroundColor = "" }.Resolve();
        Assert.Null(resolved.CreateBgPaint());   // stejné chování jako průhledná
    }

    [Theory]
    [InlineData("")]
    [InlineData("#ZZZZZZ")]
    public void NeplatnaBarvaNepovinneFeatury_SeChovaJakoNezadana(string hex)
    {
        // Střídání řádků / zvýraznění: „nenastaveno" už něco znamená (featura se nepoužije),
        // takže neplatná hodnota nesmí vypadat jako nastavená barva.
        Assert.Null(ColorHex.ParseOrNull(hex));
    }
}
