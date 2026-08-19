using TemplateDesigner.PdfImport;

namespace TemplateDesigner.Tests.PdfImport;

/// <summary>
/// Pravidla, podle kterých import odečítá rozměry z předlohy. Jsou to obecné zásady, ne hodnoty
/// naladěné na jednu vývěsku: výška řádku se měří mezi řádky (ne mezi slovy) a písmem oblasti je
/// to, kterým je vysázená většina znaků (ne to početně nejčastější).
/// </summary>
public class ImportMeasurementTests
{
    private static ExtractedWord Word(string text, float y, float fontPt = 8f) =>
        new(text, X: 10f, Y: y, Width: 10f, Height: 3f, FontFamily: "Arial",
            FontSizePt: fontPt, IsBold: false, IsItalic: false, ColorHex: "#FF000000");

    /// <summary>Slova jednoho řádku nemají v PDF přesně stejnou Y - drobný rozdíl nesmí platit
    /// za další řádek, jinak vyjde výška řádku menší, než ve skutečnosti je.</summary>
    [Fact]
    public void RozkolisanaYVJednomRadkuNedelaZeDvouRadkuCtyri()
    {
        List<ExtractedWord> words =
        [
            Word("Zastávka", 20.0f), Word("x", 20.3f),
            Word("Zastávka", 24.0f), Word("x", 24.2f),
            Word("Zastávka", 28.0f), Word("x", 28.3f),
        ];

        float rowHeight = AiGuidedScheduleBuilder.MeasureRowHeight(words);

        Assert.InRange(rowHeight, 3.5f, 4.5f);
    }

    [Fact]
    public void VyskaRadkuJeVzdalenostMeziRadky()
    {
        List<ExtractedWord> words = [Word("a", 10f), Word("b", 15f), Word("c", 20f)];

        Assert.Equal(5f, AiGuidedScheduleBuilder.MeasureRowHeight(words), 1);
    }

    /// <summary>V pásu záhlaví bývá vedle nadpisu skupiny několik drobných popisků nad sloupci.
    /// Je jich početně víc, ale písmo záhlaví určuje ten nadpis - je ho na papíře víc vidět.</summary>
    [Fact]
    public void PismoZahlaviUrcujeNadpisNePocetnejsiDrobnePopisky()
    {
        List<ExtractedWord> words =
        [
            Word("pracovní dny", 20f, 11.3f),
            Word("soboty", 20f, 11.3f),
            Word("hod", 21f, 6.2f), Word("hod", 21f, 6.2f), Word("hod", 21f, 6.2f),
        ];

        Assert.Equal(11.3f, AiGuidedScheduleBuilder.PrevazujiciVelikostPisma(words), 1);
    }

    /// <summary>Při shodném počtu znaků vyhrává větší písmo - nadpis, ne pomocný popisek.</summary>
    [Fact]
    public void PriShodeVyhravaVetsiPismo()
    {
        List<ExtractedWord> words = [Word("abcd", 20f, 12f), Word("efgh", 20f, 6f)];

        Assert.Equal(12f, AiGuidedScheduleBuilder.PrevazujiciVelikostPisma(words), 1);
    }
}
